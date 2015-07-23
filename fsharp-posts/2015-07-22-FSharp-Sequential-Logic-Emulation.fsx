
(*** hide ***)
module FsEmulation


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