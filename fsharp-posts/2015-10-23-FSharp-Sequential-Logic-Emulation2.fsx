
(*** hide ***)
module FsEmulation

let Mux sel a b  =
    match sel with
    | 0s -> a
    | _ -> b

type clk =
    | Tick = 0s
    | Tock = 1s
    
let flip = function
    | clk.Tick -> clk.Tock
    | _ -> clk.Tick

[<AbstractClass>]
type Chip() =
    member val outputs : int16 array = Array.empty with get, set
    abstract member doWork: clk -> int16 array -> int16 array
    member x.execute clk inputs = 
        let outcome = x.doWork clk inputs
        x.outputs <- outcome
        outcome

let toBinary i = 
    let rec convert i acc = 
        match i with
        | _ when i > 0s -> (i % 2s) :: convert (i / 2s) acc
        | _ -> acc
    convert i [] |> List.rev |> List.toArray

(**
This post concludes the work from chapter three of the book 'The Elements of Computing Systems'.  
As with the [other posts] in the series, I have emulated the sequential logic chips defined in the book using F#.

In the [previous post] I sidetracked from the book in order to investigate what constituted a Data Flip Flop.
The result, was a greater understanding of the sequential ordering of combinatorial chips in order to achieve state retention.

From the DFF, we can now look towards building the constructs that the book outlines.  
These include:

- Registers - Simple 1 bit stores
- RAM chips - In various sizes
- Counters - Counts sequentially per execution and can be reset or seeded as required

As always, I will outline the representation of these chips using chips from previous stages of the book, but also define a more idiomatic F# approach as well.  

<!-- more -->

#Revisiting the Flip Flop

For simplicities sake and to avoid people having to refer back, I will first redefine the Data Flip Flop from the [previous post].  
While I'm at it, I'll remove all its dependencies on other gates/chips so it is clear it functions precisely as expected. 

As stated by the book, the DFFs intention is to take and return a single bit, where the bit returned is the state from the previous clock cycle.

*)

type DFF() =
    inherit Chip()
    let mutable state = 0s
    let mutable pState = 0s
    override x.doWork clk inputs =
        match clk with //Only set the state on a tick - The falling edge
        | clk.Tick -> state <- pState
        | _ -> pState <- inputs.[0]
        [|state|]

(**

Now that we have our DFF implementation to work with, lets move on to the first of the chips required in the book, the register.  


#Registers

Registers vary in size from a single bit to multi bit stores.  
The amount of bits that a register can store is known as its width. The contents of a register are referred to as a word.
Hence a registers width is also the word size.

In order to create multi bit registers we must first start with a binary cell.


##The Binary Cell

A binary cell is a single bit register. 
It is created by making use of a DFF, a multiplexor and a pair of inputs.  

The basic premise is that we feed the DFFs output into one of the multiplexors inputs, while the other comes from an input to the chip.

The other chip input is a load bit. This bit is used to control whether or not to replace the value held in the store.  
When load is set, the incoming value to the chip is selected by the multiplexor and passed to the DFF.
When load is not set, the DFF value is passed back to itself, hence holding its state.

It's a beautifully simple design, and also easy to represent like so.

*)

//Binary Cell
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

(**

##Multi Bit Registers

Now we have our binary cell, it's an incredibly easy task to create n-bit registers. We simply create an n size array of binary cells.  
In order to use an n-bit register we pass each input bit to the registers in turn, along with the load bit.

The book calls for a 16 bit register, an implementation of which can be seen below.  

*)


//A 16 bit register 
type Register() = 
    inherit Chip()
    let bits = [|for i in 1 .. 16 -> new Bit()|]
    override x.doWork clk inputs =
        let inBits = inputs.[0] |> toBinary
        bits |> Array.mapi (fun i b -> ([|inBits.[i]; inputs.[1]|] 
                                        |> b.execute clk).[0])

(**

I chose to utilise Array.map in order to process the output of each registers execution into the resulting array.  
If we were sticking to the HDK used in the book, this would have to be 16 individual statements.

Well, that was easy.  
Onto, the RAM!

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


*)

(**
[previous post]: http://stevenpemberton.net/blog/2015/07/02/FSharp-Logic-Emulation/
*)

(*** hide ***)


(**
---
layout: post
title: Sequential Logic Emulation with F# - Part 2
date: 23/10/2015
comments: true
tags: ["fsharp","Emulation"]
catagories: ["Exploration","examples"]
seriesId: FSharpLogic
series-post-number: 3
meta: The elements of computing systems using FSharp for Boolean Logic and Arithmetic Emulation
---
*)