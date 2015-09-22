
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
This post (and the next) relate to chapter three of the book 'The Elements of Computing Systems' and revolve around emulating sequential logic chips in F#.

Everything in this post can be taken separately from my [previous post], however, I do make use of some code/concepts discussed there.  

The main goal of chapter three of the book is to create some constructs that can store state.  
Starting with registers and building up into RAM arrays.

There are various parts the book skips, relying on the hardware emulator to fill in the gaps.  
This required me to do some research and teach myself the basics of the areas not discussed in the book.

This post is dedicated to those areas, my learning outside of the book and how we can emulate simple sequential logic constructs using F#.

<!-- more -->

#Flip Flops, Propagation Delay and State

Well now, it's not every day those words become a heading.  
So what do we mean by Flip Flops!?

A Flip Flop is a mechanism for storing state in an electronic circuit. 
It does this through the use of various techniques, including latches (We'll get to these shortly) and the use of a 'clock' to add some control.

In a real-world, analogue electronic circuit, things do not happen instantly. It takes time for the combinational logic gates to 'settle' into their result.  
This is known as Propagation Delay.  

This delay plays a big part in logic circuits and I will aim to model this as best as my brain can.  
Ultimately, it will relate to function (our chips) execution and clock cycles, with a low-high (tick-tock) oscillation and the effect this has on some of our chips. Most importantly, the flip flop!


##The Flip Flop

In the book, the flip flop is provided for us.  
However, this left a void in my understanding that I couldn't live with and so I went about looking up how to implement a Data Flip Flop (DFF) from combinatorial components.

I have decided to separate this investigation from the actual work in chapter three of the book to prevent another mammoth post (and make things simpler to follow).
Therefore this post is focused solely around creating a representation of a Data Flip Flop using F# while drawing on the Boolean logic from the previous post. 

So let's take a look at what constitutes a flip flop.  

The basic principle is to create a *series* of gates that have the side effect of returning the state of the previous clock cycle. obeying the formula `out(t) = in(t - 1)`  
This allows us to store state for a single cycle and thus indefinitely until either the current stops, or the inputs change (which control when and what state to store).

*That's a very simplistic way of looking at it but it accurately describes what the DFF does and how it is the building block of volatile memory.*

The first step toward creating a DFF, is to create a Set-Reset Latch (SRLatch).

##The Set-Reset Latch

The Set-Reset latch is the base of the DFF implementation and can be implemented using NAND chips alone (There are other implementations but using NAND fits well with the books teachings).  

A Set-Reset latch is created by feeding the output from two NAND gates back into one of the inputs of the other, creating a 'latched' circuit.  
This has the effect that a low (0) output from one of the NAND gates forces the others output to be high (1). Meaning that the outputs should always be inverse.  
*A circuit diagram will be included shortly*

The inputs to this latch should be considered active low, meaning setting either of the inputs to zero (and other to one) will cause it's output to be high.  
In addition, both inputs should not be set active at the same time as this will cause the output to be indeterminate! 
One gate would likely win in the real world but we wouldn't be able to tell without testing it.

Now, we have a problem representing a latch in code due to one input of each NAND gate being fed from the output of the other, hence giving us an inherent race condition.  
In the real-world of electronic circuits, this isn't a problem as we just wait for the current to flow - Propagation delay can clearly be observed in this situation.

In code, we need to implement this delay ourselves. 
The simplest way I concluded to do this was to wrap the Latch in a type which holds the state as a mutable variable.  
The state is initialised to 0,0 (no current - I could have gone with an option but it really doesn't matter here), and on each execution of the Latch function, the state is used to feed the NAND gates with the required inputs.  
Likewise, the corresponding outputs are stored back in the state variable for next time.

This gives us just what we need. A way to mimic propagation delay per execution of a function.

So, now that we have the basic concept, let's look at some code.

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

<a class="expandPrompt">Set-Reset Latch Diagram</a>
<div class="hoverPopup" >

<img src="/content/images/post-images/Set-Reset-Latch.png" alt="Set-Reset Latch Diagram" style="float:right; margin:20px;width:300px"/>

</div>

There we have it. It's a beautifully simple design considering what it achieves.

One problem in real Set-Reset Latches is that they are susceptible to signal glitches which can cause the output to change when we don't want it to.  
In order to get around these glitches, we need to enforce *when* the latch can be updated. 

##The Clocked Set-Reset Latch

We can control when the latch state is updated by introducing an additional input.  
This input is the used to set whether or not the latch is enabled. Hence, it is often called the 'enable pin'.  

In our system, this input will eventually be controlled by the system clock which is an oscillating signal, providing alternating high(`tock`) and low(`Tick`) values (this can be generated ourselves; Maybe I'll do that in another post).
Using the clock will ensure that the latch is only ever set when the clock is 'high' (AKA a tock).

To achieve the clocked (or gated) latch, we simply insert two more NAND gates into our design.  
These two gates effectively invert the S and R inputs passing them onto our latch. However, it will only do this when the clock signal is high, thus giving us the enabling control.  
When the clock is low, the first two NAND gates will always produce a result of 'true', resulting in the output of the clocked latch staying stable.

Here is the implementation in F#.

*)

    type ClockedSRLatch() =
        let mutable state = (0s,0s)
        member x.execute s r clk =
            let (ns, nr) = (Nand s clk, Nand r clk)
            state <- (Nand ns (snd state),
                      Nand (fst state) nr)
            

(**

<a class="expandPrompt">Clocked Set-Reset Latch Diagram</a>
<div class="hoverPopup" >

<img src="/content/images/post-images/Clocked-Set-Reset-Latch.png" alt="Clocked Set-Reset Latch Diagram" style="float:right; margin:20px;width:300px"/>

</div>


To further reduce glitches and control exactly when the gates output changes, we can introduce a master-slave latch configuration.  
This is known as a Set-Reset Flip Flop.  
 
The master-slave configuration causes the gate to have the entire clock cycle to settle into its new state.

Before we get to that though, lets make things a little simpler.

##Introducing a base class

At this point, I thought it would be useful to create an abstract base class for our chips.  
This type will represent our simple electronic chips API.  
It will have inputs, outputs and a single public member, execute. The unique logic of each chip is supplied by overriding the doWork function of the base chip.

At the same time, I decided to alter my previous chips to all use `int16` instead of `Boolean` parameters.  
This allows me to specify every parameter (even those that were expecting Boolean arrays) as an integer, therefore allowing me to specify the inputs and outputs for the base chip as an int16 array.

The great thing about the object oriented side of F# is how concise it is.  
This conciseness brings with it the benefit of rapid development and prototyping. Just what we need!

Below is the code for our representation of the clock and the new abstract base class, Chip.
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
I have included the re-worked version of the Set Reset Latch below so you can see it's usage.  
*)

type SRLatch() = 
    inherit Chip()
    let mutable state = [|0s; 0s|]
    override x.doWork clk inputs =
        state <- [|Nand inputs.[0] state.[1];
                   Nand inputs.[1] state.[0]|]
        state

(*** hide ***)

type ClockedSRLatch() =
    inherit Chip()
    let srLatch = SRLatch()
    override x.doWork clk inputs =
        let s,r,clk2 = (inputs.[0], inputs.[1], clk |> int16 )
        [|Nand s clk2; Nand r clk2|] |> srLatch.execute clk

(**

This common base class will also allow me to knock up a quick test harness to test my chips. Let's look at this next.

##Testing the chips

In order to test these chips and any future chips in an easy way, I decided to create a record type to act as a crude test harness.  
It will simply hold a collection of chips, current inputs to pass to the first chip, and the outputs from the last. For simplicity, I have declared the in and out arrays as arrays of `int16` as discussed above.
*)

type TestHarness = 
    { inputs : int16 array
      outputs : int16 array
      chips : Chip array }

(**

Next up, we need to declare some functions to utilise the test harness.

*)

let setInputs i harness = 
    {harness with inputs = i;}

let executeChips harness clk =
    harness.chips |> Array.fold (fun state (chip: Chip) -> chip.execute clk state) harness.inputs

(**

And finally, we need a function to simulate applying a current to the chips for a period of time.  
The use of the word *time* here is a bit of a stretch as what we are actually going to do is execute the function iteratively in order to mimic the chips constantly being applied with a current.
 
*)

let rec iterate i clk state = 
    match i with
    | 0 -> state
    | _ -> 
        let result = { state with outputs = executeChips state clk }
        printfn "clk: %A - outputs: %A" clk result.outputs
        iterate (i - 1) clk result



(**

So, now that we are in a position to test some latches, let's see what they can do.  
First off, let's look at the Set-Reset latch.  

This chip ignores the clk input, but does visibly show the propagation delay when changing the input values.  
As we know, propagation delay in the real world is caused by the physical current applied to the chips and means we have to wait (very very briefly!) for the chips to settle into a consistent state.  
In our simulation however, it shows up as simply an extra execution or two.

To reset the latch and ensure a consistent state we execute the chip a few times with the reset pin set (Remember, the SRLatch pins are active low).  
After which, we flip the inputs and again witness the propagation delay.
*)

{inputs = [|1s;0s|]; outputs = Array.empty; chips = [|new SRLatch()|]}
|> iterate 3 clk.Tick
|> setInputs [|0s;1s|]
|> iterate 3 clk.Tick

(**

    [lang=output]
    //FSI Output
    clk: Tick - outputs: [|1s; 1s|] //Propagation delay
    clk: Tick - outputs: [|0s; 1s|]
    clk: Tick - outputs: [|0s; 1s|]
    clk: Tick - outputs: [|1s; 1s|] //Propagation delay
    clk: Tick - outputs: [|1s; 0s|]
    clk: Tick - outputs: [|1s; 0s|]

Simples, the latch behaves as needed.

Next up, The clocked Set-Reset latch.  
The output for this latch is much like the former with one difference, the inputs are only applied when the clock tocks.  
This can be seen below when the inputs are set to `[|1s; 0s|]` at the start of our second set of iterations.  
*Note that we see the now familiar propagation delay.*

*)

{inputs = [|0s;1s|]; outputs = Array.empty; chips = [|new ClockedSRLatch()|]}
|> iterate 3 clk.Tock
|> setInputs [|1s;0s|]
|> iterate 3 clk.Tick
|> iterate 3 clk.Tock

(**

    [lang=output]
    //FSI Output
    clk: Tock - outputs: [|1s; 1s|] //Propagation delay
    clk: Tock - outputs: [|0s; 1s|]
    clk: Tock - outputs: [|0s; 1s|] //Inputs set after this iteration
    clk: Tick - outputs: [|0s; 1s|] //However the output doesn't change until the tock
    clk: Tick - outputs: [|0s; 1s|]
    clk: Tick - outputs: [|0s; 1s|]
    clk: Tock - outputs: [|1s; 1s|] //Output changes as clock is now high
    clk: Tock - outputs: [|1s; 0s|]
    clk: Tock - outputs: [|1s; 0s|]

Above we can clearly see that the output only changes when the clk is high (Tock).  

Now that we are sure out initial latches are correct, we can move on to creating the Set-Reset Flip Flop.

###The Set-Reset Flip Flop

This chip is basically two Set-Reset Latches in a sequence (master-slave), with the clock value negated in between.  
This has the outcome of forcing the overall output to only change on the falling edge of the clock cycle. That is, when the clock changes from high to low (Tock to Tick).

Effectively this means that the master latch has an entire clock cycle to settle into a stable value before the slave picks it up. 
The composition of this chip is straight forward, like so:
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

<a class="expandPrompt">Set-Reset Flip Flop Diagram</a>
<div class="hoverPopup" >

<img src="/content/images/post-images/Set-Reset-Flip-Flop.png" alt="Set-Reset Flip Flop Diagram" style="float:right; margin:20px;width:300px"/>

</div>

The below code highlights how the output only changes on the falling edge of the clock.  
It is worth noting however that the initial propagation delay to settle into a stable state shows the output oscillating. This is an artefact of the simulation due to the way I modelled the use of the previous state in the execution of our NAND chips.
We can see the built in delay in both the initial settling and the second change of state.

*)

{inputs = [|0s;1s|]; outputs = Array.empty; chips = [|new RsFlipFlop()|]}
|> iterate 3 clk.Tock
|> iterate 3 clk.Tick
|> setInputs [|1s;0s|]
|> iterate 3 clk.Tock
|> iterate 3 clk.Tick

(**

    [lang=output]
    //FSI Output
    clk: Tock - outputs: [|1s; 1s|]
    clk: Tock - outputs: [|0s; 0s|]
    clk: Tock - outputs: [|1s; 1s|] 
    clk: Tick - outputs: [|0s; 1s|] //Slave picks up the now settled state
    clk: Tick - outputs: [|0s; 1s|]
    clk: Tick - outputs: [|0s; 1s|]
    clk: Tock - outputs: [|0s; 1s|] //Inputs set prior to this iteration
    clk: Tock - outputs: [|0s; 1s|]
    clk: Tock - outputs: [|0s; 1s|]
    clk: Tick - outputs: [|1s; 1s|] //Slave again picks up the changes as the clock falls
    clk: Tick - outputs: [|1s; 0s|]
    clk: Tick - outputs: [|1s; 0s|]


## The clock cycle

The previous tests have shown that our latches successfully mimic propagation delay, and eventually settle into their stable states.  

The next step is to create a function that handles the oscillation of the system clock in order to facilitate testing at a slightly higher level.  

To do this we simply call our iterate function, supplying a suitable amount of iterations in order to not flip the clock before the propagation delay has had time to expire.
This is a very crude version of clock speed. In a real world digital circuit, the clock period must be greater than that of the max propagation delay.   

For our current purposes, four iterations is enough to settle between ticks.

My cycle function is shown below. Note it calls the iterate function a given number of times per cycle and alternates the clk frequency appropriately.  
This simulates the tick-tock of a system clock.  
*)

let cycle iterations clkIters harness = 
    printfn "Executing %i cycles with inputs = %A" iterations harness.inputs
    let rec doCycle i clk harness =
        match i with
        | 0 -> harness
        | _ ->
            let result = iterate clkIters clk harness
            printfn "   Cycle %i - clk: %A - outputs: %A" (iterations - i + 1) clk result.outputs 
            match clk with
                | clk.Tick -> doCycle i (flip clk) result
                | _ -> doCycle (i-1) (flip clk) result
    doCycle iterations clk.Tick harness

(**

Now we have the clock cycle simulated (albeit incredibly crudely) lets re-test our Set-Reset Flip Flop.  

*)

{inputs = [|0s;1s|]; outputs = Array.empty; chips = [|new RsFlipFlop()|]}
|> cycle 2 4
|> setInputs [|0s;0s|]
|> cycle 2 4
|> setInputs [|1s;0s|]
|> cycle 2 4

(**

The above code shows again how the output only changes when on the falling edge of the clock, but the propagation delay is now removed (more accurately - controlled).  
In the output below we can clearly see two state changes with a hold period in between.

    [lang=output]
    //FSI Output
    Executing 2 cycles with inputs = [|0s; 1s|]
       Cycle 1 - clk: Tick - outputs: [|0s; 0s|] //State has this entire cycle to settle
       Cycle 1 - clk: Tock - outputs: [|0s; 0s|]
       Cycle 2 - clk: Tick - outputs: [|0s; 1s|] //Change in state picked up
       Cycle 2 - clk: Tock - outputs: [|0s; 1s|]
    Executing 2 cycles with inputs = [|0s; 0s|]
       Cycle 1 - clk: Tick - outputs: [|0s; 1s|] //State held
       Cycle 1 - clk: Tock - outputs: [|0s; 1s|]
       Cycle 2 - clk: Tick - outputs: [|0s; 1s|]
       Cycle 2 - clk: Tock - outputs: [|0s; 1s|]
    Executing 2 cycles with inputs = [|1s; 0s|]
       Cycle 1 - clk: Tick - outputs: [|0s; 1s|]
       Cycle 1 - clk: Tock - outputs: [|0s; 1s|]
       Cycle 2 - clk: Tick - outputs: [|1s; 0s|] //Change picked up again
       Cycle 2 - clk: Tock - outputs: [|1s; 0s|]

Nearly there now!  
We have one more chip left, the Data Flip Flop itself.  

In fact, a data flip flop is just an RS Flip Flop with a single input negated to supply both pins.

*)

type DFlipFlop() =
    inherit Chip()
    let ff = new RsFlipFlop()
    override x.doWork clk inputs =
         [|inputs.[0]; (Not inputs.[0])|]
         |> ff.execute clk

(**
If we run the same test as for the RS Flip Flop (albeit with one input) on the DFF, we get exactly the same output.  

*)

{inputs = [|0s|]; outputs = Array.empty; chips = [|new DFlipFlop()|]}
|> cycle 2 4
|> setInputs [|0s|]
|> cycle 2 4
|> setInputs [|1s|]
|> cycle 2 4

(**

    [lang=output]
    //FSI Output
    Executing 2 cycles with inputs = [|0s|]
       Cycle 1 - clk: Tick - outputs: [|0s; 0s|]
       Cycle 1 - clk: Tock - outputs: [|0s; 0s|]
       Cycle 2 - clk: Tick - outputs: [|0s; 1s|]
       Cycle 2 - clk: Tock - outputs: [|0s; 1s|]
    Executing 2 cycles with inputs = [|0s|]
       Cycle 1 - clk: Tick - outputs: [|0s; 1s|]
       Cycle 1 - clk: Tock - outputs: [|0s; 1s|]
       Cycle 2 - clk: Tick - outputs: [|0s; 1s|]
       Cycle 2 - clk: Tock - outputs: [|0s; 1s|]
    Executing 2 cycles with inputs = [|1s|] m
       Cycle 1 - clk: Tick - outputs: [|0s; 1s|]
       Cycle 1 - clk: Tock - outputs: [|0s; 1s|]
       Cycle 2 - clk: Tick - outputs: [|1s; 0s|]
       Cycle 2 - clk: Tock - outputs: [|1s; 0s|]


Success!  
The DFF can be used to facilitate the storage of a single bit of data by feeding its output back into its input with the addition of some control logic.   
This is how registers are created and we will explore this in the next post.

For now my curiosity has been satisfied. Hopefully you found this post as interesting as I did writing it.  

All code from this post can be found on [GitHub]. 

I hope you'll come back for the next post which will see me back on track and tackling the chips discussed in the book.  

*)
(**
[previous post]: http://stevenpemberton.net/blog/2015/07/02/FSharp-Logic-Emulation/
[GitHub]: https://github.com/sjpemberton/FSharpEmulator
*)

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