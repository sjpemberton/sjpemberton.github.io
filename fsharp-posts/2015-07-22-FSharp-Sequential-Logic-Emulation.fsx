
(*** hide ***)
module FsEmulation

let Nand a b = 
    match a, b with
    | true, true -> false
    | _, _ -> true

(**
This post relates to chapter three of the book 'The Elements of Computing Systems' and revolves around emulated sequential logic chips in F#.

Everything in this post can be taken separately from my [previous post], however, I do make use of some code/concepts discussed there.  

The main goal of chapter three of the book is to create some constructs that can store state.  
Starting with registers and building up into RAM arrays.

There are various parts the book skips, relying on the emulator it suggests using to fill in the gaps.  
This required me to fill these gaps in by emulating some areas not discussed in detail in the book.

Fun!

<!--more-->

#Flips Flops, Propagation Delay and State

Well now, it's not every day those words become a heading.  
So what do I mean by Flip Flops!?

A Flip Flop is a mechanism for storing state in a electronic circuit. 
It, does this through the use of various techniques, including latches (We'll get to these shortly) and the use of a 'clock'.

In a real-world electronic, analogue circuit, things do not happen instantly. It takes time for the combinational logic gates to 'settle' into their result.  
This is known as Propagation Delay.  

This delay plays a big part in logic circuits and I will aim to model this as best as my brain can.  
Ultimately, it will relate to Clock cycles, with a low-high (tick-tock) oscillation and the effect this has on some of our chips. Most importantly, the flip flop!


##The Flip Flop

In the book, the flip flop is provided for us.  
However, this left a void in my understanding that I couldn't live with and so I went about looking up how to implement a Data Flip Flop (DFF) from combinatorial components.

The basic principle is to create a *series* of gates that have the side effect of returning the state of the previous clock cycle.  
This, allows us to store state for a single cycle, and as the current is constant, this state will hold indefinitely until either the current stops, or the inputs change.

###The Set-Reset Latch

The SR latch is the base point of the DFF implementation and can be implement using NAND chips alone (There are other implementations; This seems to be the most common).  

Now, we have a problem representing a latch in code as one input of each NAND gate, comes from the output of the other, hence giving us an inherent race condition.  
In the real-world (electronic circuitry), this isn't a problem as we just wait for the current to flow - The propagation delay is clearly observed here.

In code, we need to implement this delay ourselves. 
The simplest way I concluded to do this, was to wrap the Latch in a type which holds the state as a mutable variable.  
The state is initialised to 0,0 (no current), and on each execute of the Latch function, the state is used to feed the NAND gates with the required inputs.  
Likewise, the corresponding outputs are stored back in the state variable for next time.

This gives us just what we need. A way to mimic a propagation delay per execution of a function.

*)

type SRLatch() =
    let mutable state = (false,false)
    member x.execute s r = 
        state <- (Nand s (snd state),
                  Nand (fst state) r)
        state

(**


*)

(**
[previous post]: http://stevenpemberton.net/blog/2015/07/02/FSharp-Logic-Emulation/
*)

(*** hide ***)
---
layout: post
title: Sequential Logic and RAM Emulation with F#
date: 02/07/2015
comments: true
tags: ["fsharp","Emulation"]
catagories: ["Exploration","examples"]
seriesId: FSharpLogic
series-post-number: 2
meta: The elements of computing systems using FSharp for Boolean Logic and Arithmetic Emulation
---