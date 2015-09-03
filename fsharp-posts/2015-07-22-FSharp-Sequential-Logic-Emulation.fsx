
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
            state


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

(*** hide ***)

type SRLatch() = 
    inherit Chip()
    let mutable state = (0s,0s)
    override x.doWork clk inputs = 
        let (s,r) = (inputs.[0], inputs.[1])
        state <- (Nand s (snd state),
                  Nand (fst state) r)
        [|fst state; snd state;|]

type ClockedSRLatch() =
    inherit Chip()
    let srLatch = SRLatch()
    override x.doWork clk inputs =
        let (s,r,clk2) = (inputs.[0], inputs.[1], clk |> int16 )
        [|Nand s clk2; Nand r clk2|] |> srLatch.execute clk

(**

As you can see, there's not much to it, but it gets the job done.  
It will also allow me to knock up a quick test harness to test my chips. Let's look at this next.

###Testing the chips

TODO - Show tests 

TODO - discuss test harness

*)

(**

###The Set-Reset Flip Flop

*)

type RsFlipFlop() =
    inherit Chip()
    let mutable state = (0s,0s)
    let master = new ClockedSRLatch()
    let slave = new ClockedSRLatch()
    override x.doWork clk inputs = 
        inputs
        |> master.execute clk
        |> slave.execute (clk |> flip)

(**

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