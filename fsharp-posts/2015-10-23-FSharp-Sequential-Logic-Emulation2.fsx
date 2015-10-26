
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

- Registers - Starting from simple 1 bit stores
- RAM chips - In various sizes
- Counters - Counts sequentially per execution and can be reset or loaded as required

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
        let inBits = inputs.[0] |> toTwosCompliment 16
        bits |> Array.mapi (fun i b -> ([|inputs.[1]; inBits.[i]|] 
                                        |> b.execute clk).[0])

(**

I chose to utilise Array.map in order to process the output of each registers execution into the resulting array.  
If we were sticking to the Hardware Definition Language (HDL) used in the book, this would have to be 16 individual statements.  
I will stick to this approach going forward as it reduces code and is more inline with standard F#, however it brings with it the consequence of detracting from the book.

Well, creating the register was easy.  
Onto, the RAM!

#RAM Chips

Random Access Memory (RAM) chips have both a width and a size.  

As we have just seen, the width is the amount of bits held in each register. (We will stick to the books requirement of 16 bits here)  
Size is the number of registers in the the array that makes up the RAM chip.  

The tricky part of creating a RAM chip comes when providing random access to any word (registers content) held in the chip, at equal speed.  
To accomplish this, each register needs to be given an address in the form of an integer value.  
The RAM then provides some access logic that can select the required register and either output or set its value.

Therefore RAM chips have three inputs, The value to store, an address and a load bit.  

The logic for the addressing works as follows:

- First we run the load bit and the binary representation of the integer address through a DMux8Way chip.
This has the effect of creating an 8 bit array with the load bit in the correct position.
- Next we push the input value to be stored into each of the registers along with the corresponding load bit from the previously created array. This results in the correct array being set.
- Finally we run the value of each of the registers through a Mux8WayChip, along with the address in order to select the correct word to return.

Below is the implementation of an 8 Register RAM chip.

 *)

//A 16 bit wide 8 register array.
//Utilises Mux and DMux to select the correct register to store the value in
type RAM8() =
    inherit Chip()
    let registers = [|for i in 1 .. 8 -> new Register()|]
    override x.doWork clk inputs =
        let (inBits, load, address) = (inputs.[0], inputs.[1], inputs.[2] |> toTwosCompliment 3)
        let loadArray = DMux8Way load address
        let state = registers 
                    |> Array.mapi (fun i r -> [|inBits; loadArray.[i]|]
                                              |> r.execute clk)
        Mux8Way16 state.[0] state.[1] state.[2] state.[3] state.[4] state.[5] state.[6] state.[7] address

(**

The RAM chips are built sequentially to utilise the previous implementations (As with most of the chips we have created).  
We use blocks of 8 registers as our standard. So in the case of a 64 register RAM block, we can make it from 8, 8 register RAM blocks.

The 64 register RAM chip can be seen below.  
It is much the same as the 8 register chip except that it now requires a larger binary address. To be exact it now requires 6 bits, where as the 8 register RAM chip required 3 bits.  
This follows the rule `bits = log2n` where n is the number of registers.

Therefore we need to repeat a little bit of the selection logic from the previous chip.  
We take the 3 most significant bits (MSB) and use them to select one of the RAM8 chips.    
The remaining 3 bits are then passed on to this chip in order to select one of the registers within it. Nice and recursive!

So here it is, the 64 register RAM chip:

*)

type RAM64() =
    inherit Chip()
    let ramArray = [|for i in 1 .. 8 -> new RAM8()|]
    override x.doWork clk inputs =
        let (inBits, load, address) = (inputs.[0], inputs.[1], inputs.[2] |> toTwosCompliment 6)
        let ramLoad = DMux8Way load address.[0..2]
        let state = ramArray |> Array.mapi (fun i r -> r.execute clk [|inBits; ramLoad.[i]; (toDecimal 16 address.[3..5]) |])
        Mux8Way16 state.[0] state.[1] state.[2] state.[3] state.[4] state.[5] state.[6] state.[7] address


(**

This pattern then continues as many times as we like.    
The book calls for up to a 16 thousand Register RAM block. You can see the implementations below.

*)

type RAM512() =
    inherit Chip()
    let ramArray = [|for i in 1 .. 8 -> new RAM64()|]
    override x.doWork clk inputs =
        let (inBits, load, address) = (inputs.[0], inputs.[1], inputs.[2] |> toTwosCompliment 9)
        let ramLoad = DMux8Way load address.[0..2]
        let state = ramArray |> Array.mapi (fun i r -> r.execute clk [|inBits; ramLoad.[i]; (toDecimal 16 address.[3..8]) |])
        Mux8Way16 state.[0] state.[1] state.[2] state.[3] state.[4] state.[5] state.[6] state.[7] address

type RAM4k() =
    inherit Chip()
    let ramArray = [|for i in 1 .. 8 -> new RAM512()|]
    override x.doWork clk inputs =
        let (inBits, load, address) = (inputs.[0], inputs.[1], inputs.[2] |> toTwosCompliment 12)
        let ramLoad = DMux8Way load address.[0..2]
        let state = ramArray |> Array.mapi (fun i r -> r.execute clk [|inBits; ramLoad.[i]; (toDecimal 16 address.[3..11]) |])
        Mux8Way16 state.[0] state.[1] state.[2] state.[3] state.[4] state.[5] state.[6] state.[7] address

type RAM16k() =
    inherit Chip()
    let ramArray = [|for i in 1 .. 4 -> new RAM4k()|]
    override x.doWork clk inputs =
        let (inBits, load, address) = (inputs.[0], inputs.[1], inputs.[2] |> toTwosCompliment 16)
        let ramLoad = DMux8Way load address.[0..2]
        let state = ramArray |> Array.mapi (fun i r -> r.execute clk [|inBits; ramLoad.[i]; (toDecimal 16 address.[3..13]) |])
        Mux8Way16 state.[0] state.[1] state.[2] state.[3] state.[4] state.[5] state.[6] state.[7] address

(**

As is all too clear to see, there is a lot of repetition here.  
We'll look at cleaning that up later on. 

But first, let's look at the final chip needed for this chapter, the counter.

#The Counter

The counter is a chip that contains a register alongside some combinatorial logic in order to increment its value each execution.  
In addition, this logic should also allow us to set the register to a particular value, or reset it to zero.  
In other words, we need a loadable, resettable register that can increment its value per clock cycle.

All of the building blocks for the counters logic have been created in the previous chapters of the book.  
What follows is a step by step breakdown of what is needed:

1. Get the next value by incrementing the current register value using our Increment chip.
2. Select whether we want to use the bits passed in or our next value by checking the load control bit (via a MultiMux).
3. Select whether we want to use the value chosen in the last step, or reset to 0 by checking the reset control bit (via a MultiMux).
4. Determine whether we are performing an increment, load, or reset. (This determines whether we are loading the register or not)
5. Pass the selected value and the new load bit to the underlying register.

Translated into code, this becomes the following.

*)

type Counter() = 
    inherit Chip()
    let register = new Register()
    override x.doWork clk inputs = 
        let (inBits, load, inc, reset) = (inputs.[0], inputs.[1], inputs.[2], inputs.[3])
        let current = register.execute clk [|0s; 0s|]
        let next = Increment (current) //Increment the current value
        let mux1 = MultiMux load next (inBits |> toTwosCompliment 16) 
        let mux2 = MultiMux reset mux1 [|for i in 1..16 -> 0s|]
        let regLoad = Or inc load
                      |> Or reset
        register.execute clk [| mux2 |> toDecimal 16; regLoad|]

(**

This is quite a nice chip.  
The logic is simple and clear to understand, yet it is powerful in its function.  

We will look at testing this chip later on, but first, let's think about utilising a more F# centric approach.   

#A slightly more idiomatic F# approach

While all the above is perfectly good, there are many things I would like to change.  
First up, we need to get rid of all the repetition, mostly in the RAM chips.

To achieve this, we could make a simple Enum style Discriminated Union (DU) to specify the available RAM sizes, and then just create a flat array of registers in our RAM chip like so:

*)

type RamSize =  
    | R8 = 8
    | R64 = 64
    | R512 = 512
    | R4KB = 4096
    | R16KB = 16384
    
    
type RAM(size:RamSize) = 
    inherit Chip()
    let regArray = [|for i in 1 .. int size -> new Register()|]
    override x.doWork clk inputs =
        let (inBits, load, address) = (inputs.[0], inputs.[1], inputs.[2])
        regArray.[int address].execute clk [|inBits; load|]

(**
This makes addressing the correct register much simpler to understand.

Another place we could really make use of some F# goodness, is the Counter.  
By utilising our old friend pattern matching, we remove the need to pre-calculate and can simply switch to the needed functionality.
        
*)

type Counter2() =
    inherit Chip()
    let register = new Register()
    override x.doWork clk inputs =
        let (inBits, load, inc, reset) = (inputs.[0], inputs.[1], inputs.[2], inputs.[3])
        let current = register.execute clk [|0s; 0s|]
        let toSet = 
            match reset, load, inc with
            | 1s,_,_ -> [|for i in 1..16 -> 0s|]
            | 0s,1s,_ -> inBits |> toTwosCompliment 16
            | 0s,0s,1s -> Increment (current)
            |_,_,_ -> register.execute clk [|inBits; 0s|]
        register.execute clk [|toSet |> toDecimal 16; (load ||| reset ||| inc)|]

(**

As with every other post so far, it is clear that F# provides a much more concise way of expressing the required chips.  
It does however, skip over the logical building blocks by doing so.  

I think I will continue both approaches, but will aim to take a more drastic detour in the coming posts.  
This will allow me to focus more on the power of the F# language.

#Testing

For our testing needs I will utilise the test harness from the last post.  
This allows for some quick familiar tests to be run.

First up, the Counter.  
I chose to test this as it nicely shows the use of various previous chips, drawing together a lot of past work.

*)

let TestCounter = 
    let harness = {inputs = [|0s; 0s; 1s; 0s;|]; outputs = Array.empty; chips = [|new Counter()|]}
    let result = harness
                |> cycle 3  3
                |> setInputs [|0s; 0s; 0s; 1s;|]
                |> cycle 2 3
                |> setInputs [|17s; 1s; 0s; 0s;|]
                |> cycle 1 3
                |> setInputs [|17s; 0s; 1s; 0s;|]
                |> cycle 5 3
                |> setInputs [|0s; 0s; 0s; 0s;|]
                |> cycle 2 3
    result.outputs |> toDecimal 16

(**

    [lang=output]
    //Output:
    Executing 3 cycles with inputs = [|0s; 0s; 1s; 0s|]
       Cycle 1 - clk: Tick - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s|]
       Cycle 1 - clk: Tock - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s|]
       Cycle 2 - clk: Tick - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 1s|]
       Cycle 2 - clk: Tock - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 1s|]
       Cycle 3 - clk: Tick - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 1s; 0s|]
       Cycle 3 - clk: Tock - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 1s; 0s|]
    Executing 2 cycles with inputs = [|0s; 0s; 0s; 1s|]
       Cycle 1 - clk: Tick - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 1s; 1s|]
       Cycle 1 - clk: Tock - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 1s; 1s|]
       Cycle 2 - clk: Tick - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s|]
       Cycle 2 - clk: Tock - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s|]
    Executing 1 cycles with inputs = [|17s; 1s; 0s; 0s|]
       Cycle 1 - clk: Tick - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s|]
       Cycle 1 - clk: Tock - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s|]
    Executing 5 cycles with inputs = [|17s; 0s; 1s; 0s|]
       Cycle 1 - clk: Tick - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 1s; 0s; 0s; 0s; 1s|]
       Cycle 1 - clk: Tock - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 1s; 0s; 0s; 0s; 1s|]
       Cycle 2 - clk: Tick - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 1s; 0s; 0s; 1s; 0s|]
       Cycle 2 - clk: Tock - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 1s; 0s; 0s; 1s; 0s|]
       Cycle 3 - clk: Tick - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 1s; 0s; 0s; 1s; 1s|]
       Cycle 3 - clk: Tock - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 1s; 0s; 0s; 1s; 1s|]
       Cycle 4 - clk: Tick - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 1s; 0s; 1s; 0s; 0s|]
       Cycle 4 - clk: Tock - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 1s; 0s; 1s; 0s; 0s|]
       Cycle 5 - clk: Tick - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 1s; 0s; 1s; 0s; 1s|]
       Cycle 5 - clk: Tock - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 1s; 0s; 1s; 0s; 1s|]
    Executing 2 cycles with inputs = [|0s; 0s; 0s; 0s|]
       Cycle 1 - clk: Tick - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 1s; 0s; 1s; 1s; 0s|]
       Cycle 1 - clk: Tock - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 1s; 0s; 1s; 1s; 0s|]
       Cycle 2 - clk: Tick - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 1s; 0s; 1s; 1s; 0s|]
       Cycle 2 - clk: Tock - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 1s; 0s; 1s; 1s; 0s|]


    val TestCounter : int16 = 22s


Note that the printed value is always one clock cycle behind.  
This is because the register in the counter only commits to holding the value on the falling edge of the clock. 
Hence, we have to probe the register in the *subsequent* clock cycle to ensure the value was set.

Finally, we will test a 64 register RAM array.  
To do this, I have bent the use of test harness slightly in order to store some values during execution of a set of functions.  

I will test the following steps:

1. Store two binary representations of integers in arbitrary locations in RAM
2. Extract these integers storing them locally (we actually store an entire snapshot of the test harness state)
3. Add these two integers together and store them at position one of the RAM array
4. Retrieve the value back out of RAM and print the output

This translates to the following code.
*)

let TestRam64 = 
    let mutable harness = {inputs = [|15s; 1s; 5s;|]; outputs = Array.empty; chips = [|new RAM64()|]}
                        |> cycle 1  3
                        |> setInputs [|33s;1s;7s|]
                        |> cycle 1  3

    harness <- harness
            |> setInputs [|0s;0s;5s|]
            |> cycle 1 3
    let a = harness.outputs

    harness <- harness
            |> setInputs [|0s;0s;7s|]
            |> cycle 1 3
    let b = harness.outputs

    printfn "Adding a (%i) to b (%i) and storing at position 1." (a |> toDecimal 16) (b |> toDecimal 16)
    harness <- harness
            |> setInputs [| (Adder a b) |> toDecimal 16  ;1s; 1s|]
            |> cycle 1 3
            |> setInputs [|0s;0s;1s|]
            |> cycle 1 3

    printfn "The value was %i" (harness.outputs |> toDecimal 16)

(**

Overall, not the best looking test case as my harness wasn't really thought through enough to be useful in this situation.  
It does show the use of our RAM chip quite nicely though.

The output of running this is shown below:

    [lang=output]
    //Output:
    Executing 1 cycles with inputs = [|15s; 1s; 5s|]
       Cycle 1 - clk: Tick - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s|]
       Cycle 1 - clk: Tock - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s|]
    Executing 1 cycles with inputs = [|33s; 1s; 7s|]
       Cycle 1 - clk: Tick - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s|]
       Cycle 1 - clk: Tock - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s|]
    Executing 1 cycles with inputs = [|0s; 0s; 5s|]
       Cycle 1 - clk: Tick - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 1s; 1s; 1s; 1s|]
       Cycle 1 - clk: Tock - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 1s; 1s; 1s; 1s|]
    Executing 1 cycles with inputs = [|0s; 0s; 7s|]
       Cycle 1 - clk: Tick - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 1s; 0s; 0s; 0s; 0s; 1s|]
       Cycle 1 - clk: Tock - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 1s; 0s; 0s; 0s; 0s; 1s|]
    Adding a (15) to b (33) and storing at position 1.
    Executing 1 cycles with inputs = [|48s; 1s; 1s|]
       Cycle 1 - clk: Tick - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s|]
       Cycle 1 - clk: Tock - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s|]
    Executing 1 cycles with inputs = [|0s; 0s; 1s|]
       Cycle 1 - clk: Tick - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 1s; 1s; 0s; 0s; 0s; 0s|]
       Cycle 1 - clk: Tock - outputs: [|0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 0s; 1s; 1s; 0s; 0s; 0s; 0s|]
    The value was 48

#Conclusion

Utilising the work from my previous posts, it has been nice and easy to create the chips needed and test them as required.  
However, there are some major drawbacks that I wish to fix. The biggest being the use of the int16 array to pass state around.  

Doing this bypasses one of the strongest features of F#, it's type system.  
It adds far too many possibilities for errors into the code and so it must be removed.
  
Therefore, before venturing into the next chapter of the book, I'm going to see how I can go about refactoring my current work to take full advantage of the features of F#.  
That should help with reducing the possibility of errors and working towards the goal of making invalid state unrepresentable.  

*)


(**
[previous post]: http://stevenpemberton.net/blog/2015/07/02/FSharp-Logic-Emulation/
[other posts]: http://stevenpemberton.net/archive/tags/Emulation.html
*)

(*** hide ***)
---
layout: post
title: Sequential Logic Emulation with F# - Part 2
date: 26/10/2015
comments: true
tags: ["fsharp","Emulation"]
catagories: ["Exploration","examples"]
seriesId: FSharpLogic
series-post-number: 3
meta: The elements of computing systems using FSharp for Boolean Logic and Arithmetic Emulation
---
