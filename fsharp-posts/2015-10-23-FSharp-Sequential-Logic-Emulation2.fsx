
(*** hide ***)
module FsEmulation

let Nand a b =
    match a, b with
    | 1s, 1s -> 0s
    | _, _ -> 1s

let Not = function
    | 1s -> 0s
    | _ -> 1s

(**
This post concludes the work from chapter three of the book 'The Elements of Computing Systems'.  
As with the [other posts] in the series, I have emulated the sequential logic chips defined in the book using F#.

In the [previous post] I sidetracked from th ebook in order to investigate what constituted a Data Flip Flop.
The result, was a greater understanding of the sequential ordering of combinatorial chips in order to achieve state retention.

From the DFF, we can now look towards building the constructs that the book outlines.  
These include:

-Registers - Simple 1 bit stores
-RAM chips - In various sizes
-Counters - Counts sequentially per execution and can be reset or seeded as required

As always, I will outline the representation of these chips using chips from previous stages of the book, but also define a more idiomatic F# approach as well.  

<!-- more -->

#Revisiting the Flip Flop

We can simply declare a DFF as follows in an F# way that make sit clear what it does.

    type DFF() = 
        let mutable state = 0s
        member x.execute d clk = 
            let pState = state
            match clk with //Only set the state on a tock 
            | true -> state <- d
            | _ -> ()
            pState


#Registers


##The BIT Store

//Stores a single bit.
//The Mux chip acts as a selector on whether to store a new value or keep hold of the old DFF value
type Bit() =
    inherit Chip()
    let dff = new DFF()
    let mutable state = 0s
    override x.doWork clk inputs =
        state <-([|Mux inputs.[0] state inputs.[1]|]
                |> dff.execute clk).[0]
        [|state|]

##The Registers

//A 16 bit register 
type Register() = 
    inherit Chip()
    let bits = [|for i in 1 .. 16 -> new Bit()|]
    override x.doWork clk inputs =
        let inBits = inputs.[0] |> toBinary
        bits |> Array.mapi (fun i b -> ([|inBits.[i]; inputs.[1]|] 
                                        |> b.execute clk).[0])



#RAM Chips

RAM chips have both a width and a sizes.
The width relates to the amount of bits held in each register. We stick to the recommended 16 bits here.
Size is the number of bits size of the RAM in terms of number of registers.

//An 16 bit wide 8 bit size, register array.
//Utilises Mux and DMux to select the correct register to store the value in
type RAM8() =
    inherit Chip()
    let registers = [|for i in 1 .. 8 -> new Register()|]
    override x.doWork clk inputs =
        let (inBits, load, address) = (inputs.[0], inputs.[1], inputs.[2])
        let loadArray = DMux8Way load (address |> toBinary)
        let state = registers 
                    |> Array.mapi (fun i r -> [|inBits; loadArray.[i]|]
                                              |> r.execute clk)
        Mux8Way16 state.[0] state.[1] state.[2] state.[3] state.[4] state.[5] state.[6] state.[7] (address |> toBinary) 

The RAM chips are built succesively in order to utilise the previous.
We use blocks of 8 as our standard form. In case of a 64 register RAM block, it can be made from 8, 8 register RAM blocks as below:

//TODO - Discuss addressing

type RAM64() =
    inherit Chip()
    let ramArray = [|for i in 1 .. 8 -> new RAM8()|]
    override x.doWork clk inputs =
        let (inBits, load, address) = (inputs.[0], inputs.[1], inputs.[2])
        let ramLoad = DMux8Way load  (address |> toBinary).[0..2]
        let state = ramArray |> Array.mapi (fun i r -> r.execute clk [|inBits; ramLoad.[i]; (toDecimal 16 (address |> toBinary).[3..5]) |])
        Mux8Way16 state.[0] state.[1] state.[2] state.[3] state.[4] state.[5] state.[6] state.[7] (address |> toBinary)

This pattern then continues for as many as we like.  
The book calls for up to a 16 thousand Register RAM block.

type RAM512() =
   let ramArray = [|for i in 1 .. 8 -> new RAM64()|]
   member x.execute (inBits: int16 array) clk load (address: int16 array) =
       let ramLoad = DMux8Way load address.[0..2]
       let state = ramArray |> Array.mapi (fun i r -> r.execute inBits clk ramLoad.[i] address.[3..8])
       Mux8Way16 state.[0] state.[1] state.[2] state.[3] state.[4] state.[5] state.[6] state.[7] address

type RAM4k() =
   let ramArray = [|for i in 1 .. 8 -> new RAM512()|]
   member x.execute (inBits: int16 array) clk load (address: int16 array) =
       let ramLoad = DMux8Way load address.[0..2]
       let state = ramArray |> Array.mapi (fun i r -> r.execute inBits clk ramLoad.[i] address.[3..11])
       Mux8Way16 state.[0] state.[1] state.[2] state.[3] state.[4] state.[5] state.[6] state.[7] address

type RAM16k() =
   let ramArray = [|for i in 1 .. 4 -> new RAM4k()|]
   member x.execute (inBits: int16 array) clk load (address: int16 array) =
       let ramLoad = DMux8Way load address.[0..2]
       let state = ramArray |> Array.mapi (fun i r -> r.execute inBits clk ramLoad.[i] address.[3..13])
       Mux8Way16 state.[0] state.[1] state.[2] state.[3] state.[4] state.[5] state.[6] state.[7] address

#The Counter

The counter is a chip that wither increments each execution, is set to a particular value, or reset.

type Counter() = 
    inherit Chip()
    let register = new Register()
    override x.doWork clk inputs = //(inBits: int16 array) clk inc load reset =
        let next = Increment (register.execute clk [| inputs.[0]; 0s |]) //Increment the current value
        let mux1 = MultiMux inputs.[2] next (inputs.[0] |> toBinary) 
        let mux2 = MultiMux inputs.[3] mux1 [|for i in 1..16 -> 0s|]
        let or1 = Or inputs.[2] inputs.[1]
        let or2 = Or or1 inputs.[3]
        register.execute clk [| mux2 |> toDecimal 16; or2|]
        
type CounterPM() =
    inherit Chip()
    let register = new Register()
    override x.doWork clk inputs =
        let (inBits, inc, load, reset) = (inputs.[0], inputs.[1], inputs.[2], inputs.[3])
        let toSet = 
            match reset, load, inc with
            | 1s,_,_ -> [|for i in 1..16 -> 0s|]
            | 0s,1s,_ -> inBits |> toBinary
            | 0s,0s,1s -> Increment (register.execute clk [|inBits; 0s|])
            |_,_,_ -> register.execute clk [|inBits; 0s|]
        register.execute clk [|toSet |> toDecimal 16; (load ||| reset ||| inc)|]

#An Idiomatic F# approach

type RamSize =
    | Bit8 
    | Bit64 
    | Bit512
    | KB4   
    | KB16  

let getSize = function 
    | Bit8   -> 8
    | Bit64  -> 64
    | Bit512 -> 512
    | KB4    -> 4096
    | KB16   -> 16384
    
    
//More F# approach
type RAM(size) = 
    inherit Chip()
    let regArray = [|for i in 1 .. size |> getSize -> new Register()|]
    override x.doWork clk inputs =
        let (inBits, load, address) = (inputs.[0],inputs.[1],inputs.[2])
        regArray.[int address].execute clk [|inBits; load|] //Need to handle index out of range

#Testing

(*** hide ***)
---
layout: post
title: Sequential Logic Emulation with F#
date: 22/09/2015
comments: true
tags: ["fsharp","Emulation"]
catagories: ["Exploration","examples"]
seriesId: FSharpLogic
series-post-number: 2
meta: The elements of computing systems using FSharp for Boolean Logic and Arithmetic Emulation
---
