
(*** hide ***)
module FsEmulation

#load "Dependencies/FSLogic/ArithmeticLogicUnit.fs"
#load "Dependencies/FSLogic/Utils.fs"
#load "Dependencies/FSLogic/Sequential.fs"

open ArithmeticLogicUnit
open Sequential
open Utils

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
I have chosen to take this approach going forward so as to write more idiomatic F# but with the consequence of detracting from the book.

Well, that was easy.  
Onto, the RAM!

#RAM Chips

RAM (Random Access Memory) chips have both a width and a size.  

As we have just seen, the width is the amount of bits held in each register. (We will stick to the books requirement of 16 bits here)
Size is the number of registers in the the array that makes up the RAM chip.  

The tricky part of creating a RAM chip comes when providing random access to any word (registers content) held in the chip, at equal speed.  
To accomplish this, each register needs to be given an address in the form of an integer value.  
The RAM then provides some access logic that can select the required register and either output or set its value.

Therefore RAM chips have three inputs, The value to store, an address and a load bit.  

The logic for the addressing works as follows:

- First we run the load bit and the binary representation of the integer address through a DMux8Way chip.
This has the effect of creating an 8 bit array with the load bit in the correct position.
- Next we push the input value to be set into the register to each of the registers along with the corresponding load bit from the previously created array. This results in the correct array being set if required.
- Finally we run the value of each of the registers through a Mux8WayChip, along with the address in order to select the correct word to return.

Below is the implementation of an 8 Register RAM chip.

 *)

//A 16 bit wide 8 register array.
//Utilises Mux and DMux to select the correct register to store the value in
type RAM8() =
    inherit Chip()
    let registers = [|for i in 1 .. 8 -> new Register()|]
    override x.doWork clk inputs =
        let (inBits, load, address) = (inputs.[0], inputs.[1], inputs.[2] |> toBinary)
        let loadArray = DMux8Way load address
        let state = registers 
                    |> Array.mapi (fun i r -> [|inBits; loadArray.[i]|]
                                              |> r.execute clk)
        Mux8Way16 state.[0] state.[1] state.[2] state.[3] state.[4] state.[5] state.[6] state.[7] address

(**

The RAM chips are built sequentially to utilise the previous implementations (As with most of the chips we have created).
We use blocks of 8 as our standard. So in the case of a 64 register RAM block, we can make it from 8, 8 register RAM blocks as below:

The 64 register RAM chip can be seen below.  
It is much the same as the 8 register chip except that it now requires a larger binary address. To be exact it now requires 6 bits, where as the 8 register RAM chip required 3 bits.  
This follows the rule `bits = log2n` where n is the number of registers.

Therefore we need to repeat a little bit of the selection logic from the previous chip.  
We take the 3 MSB (most significant bits) and use them to select one of the RAM8 chips.    
The remaining 3 bits are then passed on to this chip in order to select one of the registers within it. Nice and recursive!

So here it is, the 64 register RAM chip:

*)

type RAM64() =
    inherit Chip()
    let ramArray = [|for i in 1 .. 8 -> new RAM8()|]
    override x.doWork clk inputs =
        let (inBits, load, address) = (inputs.[0], inputs.[1], inputs.[2] |> toBinary)
        let ramLoad = DMux8Way load address.[0..2]
        let state = ramArray |> Array.mapi (fun i r -> r.execute clk [|inBits; ramLoad.[i]; (toDecimal 16 address.[3..5]) |])
        Mux8Way16 state.[0] state.[1] state.[2] state.[3] state.[4] state.[5] state.[6] state.[7] address


(**

This pattern then continues for as many powers of 2 as we like.  
The book calls for up to a 16 thousand Register RAM block and you can see the implementations below.

*)

type RAM512() =
    inherit Chip()
    let ramArray = [|for i in 1 .. 8 -> new RAM64()|]
    override x.doWork clk inputs =
        let (inBits, load, address) = (inputs.[0], inputs.[1], inputs.[2] |> toBinary)
        let ramLoad = DMux8Way load address.[0..2]
        let state = ramArray |> Array.mapi (fun i r -> r.execute clk [|inBits; ramLoad.[i]; (toDecimal 16 address.[3..8]) |])
        Mux8Way16 state.[0] state.[1] state.[2] state.[3] state.[4] state.[5] state.[6] state.[7] address

type RAM4k() =
    inherit Chip()
    let ramArray = [|for i in 1 .. 8 -> new RAM512()|]
    override x.doWork clk inputs =
        let (inBits, load, address) = (inputs.[0], inputs.[1], inputs.[2] |> toBinary)
        let ramLoad = DMux8Way load address.[0..2]
        let state = ramArray |> Array.mapi (fun i r -> r.execute clk [|inBits; ramLoad.[i]; (toDecimal 16 address.[3..11]) |])
        Mux8Way16 state.[0] state.[1] state.[2] state.[3] state.[4] state.[5] state.[6] state.[7] address

type RAM16k() =
    inherit Chip()
    let ramArray = [|for i in 1 .. 4 -> new RAM4k()|]
    override x.doWork clk inputs =
        let (inBits, load, address) = (inputs.[0], inputs.[1], inputs.[2] |> toBinary)
        let ramLoad = DMux8Way load address.[0..2]
        let state = ramArray |> Array.mapi (fun i r -> r.execute clk [|inBits; ramLoad.[i]; (toDecimal 16 address.[3..13]) |])
        Mux8Way16 state.[0] state.[1] state.[2] state.[3] state.[4] state.[5] state.[6] state.[7] address

(**

As is all to clear to see, there is a lot of repetition here.  
We'll look at cleaning that up later on. 

But first, let's look at the final chip needed for this chapter, the counter.

#The Counter

The counter is a chip that contains a register alongside some combinatorial logic in order to increment its value each execution.  
In addition the logic should also allow us to set the register to a particular value, or reset it.  
In other words, we need a loadable, resettable register that can increment its value per clock cycle.

All of the building blocks for the counters logic have been created in the previous chapters of the book.  
What follows is a step by step breakdown of what is needed:

1. Get the next value by incrementing the current register value using our Increment chip.
2. Select whether we want to use the bits passed in or our next value by checking the load control bit (via a MultiMux).
3. Select whether we want to use the value chosen in the last step, or rest to 0 by checking the reset control bit (via a MultiMux).
4. determine whether we are performing an increment, load, or reset. (This determines whether we are loading the register or not)
5. pass the selected value and the new load bit to the underlying register.

Translated into code, this becomes the following.

*)

type Counter() = 
    inherit Chip()
    let register = new Register()
    override x.doWork clk inputs = 
        let (inBits, load, inc, reset) = (inputs.[0], inputs.[1], inputs.[2], inputs.[3])
        let next = Increment (register.execute clk [| inBits; 0s |]) //Increment the current value
        let mux1 = MultiMux load next (inBits |> toBinary) 
        let mux2 = MultiMux reset mux1 [|for i in 1..16 -> 0s|]
        let regLoad = Or inc load
                      |> Or reset
        register.execute clk [| mux2 |> toDecimal 16; regLoad|]
        

(**

#An Idiomatic F# approach

*)

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

(**

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