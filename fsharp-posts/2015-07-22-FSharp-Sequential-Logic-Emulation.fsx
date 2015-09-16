
(*** hide ***)
module FsEmulation

let Nand a b = 
    match a, b with
    | 1s, 1s -> 0s
    | _, _ -> 1s

(**
This post relates to chapter three of the book 'The Elements of Computing Systems' and revolves around emulated sequential logic chips in F#.

Everything in this post can be taken separately from my [previous post], however, I do make use of some code/concepts discussed there.  

The main goal of chapter three of the book is to create some constructs that can store state.  
Starting with registers and building up into RAM arrays.

There are various parts the book skips, relying on the hardware emulator to fill in the gaps.  
This required me to fill these gaps in myself by emulating some areas not discussed in detail in the book.

Fun!

<!--more-->

#Flips Flops, Propagation Delay and State

Well now, it's not every day those words become a heading.  
So what do I mean by Flip Flops!?

A Flip Flop is a mechanism for storing state in a electronic circuit. 
It, does this through the use of various techniques, including latches (We'll get to these shortly) and the use of a 'clock'.

In a real-world analogue, electronic circuit, things do not happen instantly. It takes time for the combinational logic gates to 'settle' into their result.  
This is known as Propagation Delay.  

This delay plays a big part in logic circuits and I will aim to model this as best as my brain can.  
Ultimately, it will relate to Clock cycles, with a low-high (tick-tock) oscillation and the effect this has on some of our chips. Most importantly, the flip flop!


##The Flip Flop

In the book, the flip flop is provided for us.  
However, this left a void in my understanding that I couldn't live with and so I went about looking up how to implement a Data Flip Flop (DFF) from combinatorial components.

The basic principle is to create a *series* of gates that have the side effect of returning the state of the previous clock cycle.  
This allows us to store state for a single cycle, and as the current is constant, this state will hold indefinitely until either the current stops, or the inputs change.

###The Set-Reset Latch

The SR latch is the base point of the DFF implementation and can be implement using NAND chips alone (There are other implementations; This seems to be the most common).  

TODO - describe the latch

Now, we have a problem representing a latch in code as one input of each NAND gate, comes from the output of the other, hence giving us an inherent race condition.  
In the real-world (electronic circuitry), this isn't a problem as we just wait for the current to flow - The propagation delay is clearly observed here.

In code, we need to implement this delay ourselves. 
The simplest way I concluded to do this, was to wrap the Latch in a type which holds the state as a mutable variable.  
The state is initialised to 0,0 (no current), and on each execution of the Latch function, the state is used to feed the NAND gates with the required inputs.  
Likewise, the corresponding outputs are stored back in the state variable for next time.

This gives us just what we need. A way to mimic a propagation delay per execution of a function.

*)

(*** hide ***)
module Chips1 =

(***)

    type SRLatch() =
        let mutable state = (0s,0s)
        member x.execute s r = 
            state <- (Nand s (snd state),
                      Nand (fst state) r)
            state

(**

The biggest problem in real SRLatches is that they are susceptible to signal glitches.  
In order to get around these glitches, we need to enforce *when* the latch can be updated. 

###The Clocked Set-Reset Latch

We do this by introducing the system clock. In particular, an oscillating clock.  
We can then ensure that the latch is only ever set when the clock is 'high' (AKA tock).

To do this we simply insert two more NAND gates into our design.  
These gates effectively invert the S and R inputs passing them onto our latch. However it will only do this when the clock is high.  
When it is low, the two NAND gates will always produce a result of 'true'.

Here is the implementation in F#

*)

    type ClockedSRLatch() =
        let mutable state = (0s,0s)
        member x.execute s r clk =
            let (ns, nr) = (Nand s clk, Nand r clk)
            state <- (Nand ns (snd state),
                      Nand (fst state) nr)
            

(**

To further reduce glitches, we can introduce a master-slave latch configuration.  
This results in a Set-Reset Flip Flop, that has the entire clock cycle to settle in to it's new state.

###Introducing a base class

At this point, I thought it would be useful to create an abstract base class for our chips.  
This type will represent our simple electronic chips API.  
It will have inputs, outputs and a single public member, execute. The unique logic of each chip is supplied by overriding the doWork function of the base chip.

At the same time, I decided to alter my previous chips to all use `int16` instead of `Boolean` parameters.  
This allows me to specify every parameter (even those that were expecting Boolean arrays) as an integer, therefore allowing me to specify the inputs and outputs for the base chip as an int16 array 

The great thing about the object oriented side of F# is how concise it is.  
Below is the code for our new abstract base class, Chip.
*)

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


(**

As you can see, there's not much to it, but it gets the job done.  
It will also allow me to knock up a quick test harness to test my chips. Let's look at this next.

I have included the re-worked version of the Set Reset Latch below.  
This implementation includes a bit of extra logic in order to force the execution of one of the NAND gates before the other.  
This aims to alleviate two problems.

1. When first using an un initialised latch, it is entirely possible to get the output stuck in an oscillation cycle.  
This wouldn't happen in reality as the chips state would eventually settle (assuming that both of the inputs were not true)

2. When both of the inputs are true, which is an invalid state, the output oscillates. 
Again, in the real world it is likely that one NAND would execute first, but the output would be indeterminate.
This point wasn't such a problem as we will want to avoid this invalid state anyway.

 We will see this in action shortly.

*)

type SRLatch() = 
    inherit Chip()
    let state = [|0s; 0s|]
    override x.doWork clk inputs = 
        let rand = new System.Random()
        match rand.Next(2) with
        | 0 -> state.[0] <- Nand inputs.[0] state.[1]
               state.[1] <- Nand inputs.[1] state.[0]
        | _ -> state.[1] <- Nand inputs.[1] state.[0]
               state.[0] <- Nand inputs.[0] state.[1]
        state 

(*** hide ***)

type ClockedSRLatch() =
    inherit Chip()
    let srLatch = SRLatch()
    override x.doWork clk inputs =
        let s,r,clk2 = (inputs.[0], inputs.[1], clk |> int16 )
        [|Nand s clk2; Nand r clk2|] |> srLatch.execute clk

(**

###Testing the chips

In order to test these chips and any future chips, in an easy way, I decided to create a type to act as a crude test harness.  
It will simply hold a collection of chips, current inputs to pass to the first chip, and the outputs from the last. For simplicity, I have declared the in and out arrays as arrays of `int16`.
*)

type TestHarness = 
    { inputs : int16 array
      outputs : int16 array
      chips : Chip array }

(**

Next up, we need to declare some functions to utilise the test harness, returning new state of course.

*)

let setInputs i harness = 
    {harness with inputs = i;}

let executeChips harness clk =
    harness.chips |> Array.fold (fun state (chip: Chip) -> chip.execute clk state) harness.inputs

(**

And finally, we need a function to simulate applying a current to the chips for a period of time.  
I chose to model this as clock cycles as opposed to time. This way, we can execute a chip (or set of chips) for a set amount of cycles, change the inputs and then continue executing.

My cycle function is shown below. Note it contains logic to call the chips twice per cycle, alternating the clk frequency.  
This simulates the tick-tock of a digital clk cycle.  
 
*)

let cycle iterations (harness : TestHarness) = 
    printfn "Executing %i cycles with inputs = %A" iterations harness.inputs
    let rec iterate i clk state = 
        match i with
        | 0 -> state
        | _ -> 
            let result = { state with outputs = executeChips state clk }
            printfn "Cycle %i - clk: %A - outputs: %A" (iterations - i + 1) clk result.outputs
            match clk with
            | clk.Tick -> iterate i (flip clk) result
            | _ -> iterate (i - 1) (flip clk) result
    iterate iterations clk.Tick harness

(**

So, now that we are in a position to test some latches, let's see what they can do.  
First off, let's look at the Set-Reset latch.  

This chip ignores the clk input, but does visibly show the propagation delay when changing the input values. 

We need to reset the latch first to ensure a consistent state so cycle with the reset pin set (Remember, the SRLatch pins are active low).
We then hold the state for 2 cycles.
Finally we change the inputs again to activate the set pin and witness the outputs change correctly.  

*Note that we may not see the propagation delay due to the way I added the logic to the chip to mimic it.  
 There is a chance the correct NAND chip wins first time. Either way, this highlights the fact that not being able to control when the change occurs, is a problem*
*)

let harness = {inputs = [|1s;0s|]; outputs = Array.empty; chips = [|new SRLatch()|]}
harness 
|> cycle 3 
|> setInputs [|1s;1s|]
|> cycle 2
|> setInputs [|0s;1s|]
|> cycle 3


(**
    [lang=output]
    //FSI Output
    Executing 3 cycles with inputs = [|1s; 0s|]
       Cycle 1 - clk: Tick - outputs: [|1s; 1s|]
       Cycle 1 - clk: Tock - outputs: [|0s; 1s|]
       Cycle 2 - clk: Tick - outputs: [|0s; 1s|]
       Cycle 2 - clk: Tock - outputs: [|0s; 1s|]
       Cycle 3 - clk: Tick - outputs: [|0s; 1s|]
       Cycle 3 - clk: Tock - outputs: [|0s; 1s|]
    Executing 2 cycles with inputs = [|1s; 1s|]
       Cycle 1 - clk: Tick - outputs: [|0s; 1s|]
       Cycle 1 - clk: Tock - outputs: [|0s; 1s|]
       Cycle 2 - clk: Tick - outputs: [|0s; 1s|]
       Cycle 2 - clk: Tock - outputs: [|0s; 1s|]
    Executing 3 cycles with inputs = [|0s; 1s|]
       Cycle 1 - clk: Tick - outputs: [|1s; 1s|]
       Cycle 1 - clk: Tock - outputs: [|1s; 0s|]
       Cycle 2 - clk: Tick - outputs: [|1s; 0s|]
       Cycle 2 - clk: Tock - outputs: [|1s; 0s|]
       Cycle 3 - clk: Tick - outputs: [|1s; 0s|]
       Cycle 3 - clk: Tock - outputs: [|1s; 0s|]

The above output happens to show the propagation delay twice.

Next up, The clocked Set-Reset latch.  
The output for this latch is much like the former with one difference, the inputs are only applied when the clock tocks.  
This can be seen below when the inputs are set to `[|1s; 0s|]` at the start of our third set of iterations.  
*Note that the initial settling into a stable state happens regardless of clk value*

    [lang=output]
    //FSI Output
    Executing 3 cycles with inputs = [|0s; 1s|]
       Cycle 1 - clk: Tick - outputs: [|1s; 0s|]
       Cycle 1 - clk: Tock - outputs: [|1s; 1s|]
       Cycle 2 - clk: Tick - outputs: [|0s; 1s|]
       Cycle 2 - clk: Tock - outputs: [|0s; 1s|]
       Cycle 3 - clk: Tick - outputs: [|0s; 1s|]
       Cycle 3 - clk: Tock - outputs: [|0s; 1s|]
    Executing 2 cycles with inputs = [|0s; 0s|]
       Cycle 1 - clk: Tick - outputs: [|0s; 1s|]
       Cycle 1 - clk: Tock - outputs: [|0s; 1s|]
       Cycle 2 - clk: Tick - outputs: [|0s; 1s|]
       Cycle 2 - clk: Tock - outputs: [|0s; 1s|]
    Executing 3 cycles with inputs = [|1s; 0s|]
       Cycle 1 - clk: Tick - outputs: [|0s; 1s|]
       Cycle 1 - clk: Tock - outputs: [|1s; 0s|]
       Cycle 2 - clk: Tick - outputs: [|1s; 0s|]
       Cycle 2 - clk: Tock - outputs: [|1s; 0s|]
       Cycle 3 - clk: Tick - outputs: [|1s; 0s|]
       Cycle 3 - clk: Tock - outputs: [|1s; 0s|]

The clocked Set-Reset Latch can be further improved by creating a Set-Reset Flip Flop.

###The Set-Reset Flip Flop

This chip is basically two Set-Reset Latches in a sequence (master-slave), with the clock value negated in between.  
This has the outcome of forcing the overall output to only change on the falling edge of the clock cycle. That is, when the clock changes from high to low (Tock to Tick).

Effectively this means that the master latch has an entire clock cycle to settle into a stable value before the slave picks it up. 

*)

type RsFlipFlop() =
    inherit Chip()
    let master = new ClockedSRLatch()
    let slave = new ClockedSRLatch()
    override x.doWork clk inputs = 
        inputs
        |> master.execute clk
        |> slave.execute (clk |> flip)

(**

The below is output of running the same sequence of functions as before.  
We can see the built in delay in both the initial settling and the second change of state.

    [lang=output]
    //FSI Output
    Executing 3 cycles with inputs = [|0s; 1s|]
       Cycle 1 - clk: Tick - outputs: [|1s; 0s|]
       Cycle 1 - clk: Tock - outputs: [|1s; 0s|]
       Cycle 2 - clk: Tick - outputs: [|1s; 1s|]
       Cycle 2 - clk: Tock - outputs: [|0s; 1s|]
       Cycle 3 - clk: Tick - outputs: [|0s; 1s|]
       Cycle 3 - clk: Tock - outputs: [|0s; 1s|]
    Executing 2 cycles with inputs = [|0s; 0s|]
       Cycle 1 - clk: Tick - outputs: [|0s; 1s|]
       Cycle 1 - clk: Tock - outputs: [|0s; 1s|]
       Cycle 2 - clk: Tick - outputs: [|0s; 1s|]
       Cycle 2 - clk: Tock - outputs: [|0s; 1s|]
    Executing 3 cycles with inputs = [|1s; 0s|]
       Cycle 1 - clk: Tick - outputs: [|0s; 1s|]
       Cycle 1 - clk: Tock - outputs: [|0s; 1s|]
       Cycle 2 - clk: Tick - outputs: [|1s; 0s|]
       Cycle 2 - clk: Tock - outputs: [|1s; 0s|]
       Cycle 3 - clk: Tick - outputs: [|1s; 0s|]
       Cycle 3 - clk: Tock - outputs: [|1s; 0s|]

*)

(**
[previous post]: http://stevenpemberton.net/blog/2015/07/02/FSharp-Logic-Emulation/
*)

(*** hide ***)
//---
//layout: post
//title: Sequential Logic and RAM Emulation with F#
//date: 02/07/2015
//comments: true
//tags: ["fsharp","Emulation"]
//catagories: ["Exploration","examples"]
//seriesId: FSharpLogic
//series-post-number: 2
//meta: The elements of computing systems using FSharp for Boolean Logic and Arithmetic Emulation
//---