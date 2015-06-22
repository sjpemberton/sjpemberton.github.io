
(*** hide ***)
module FsEmulation

(**
So, I've had this book on my shelf for a good few years and never got round to reading it, yet alone completing the exercises within.

<img src="/content/images/post-images/tecs_cover.jpg" alt="Drawing" style="width:220px; float:right; margin:20px;"/>

Therefore I thought why not complete the exercises in a different way than suggested in the book.  
Rather than utilising the Hardware Definition Language and the given emulator as suggested. I will model all of the parts of the computer, in F#. 

Doing so, I hope to utilise various functional programming techniques, alongside F# specific features to keep the solutions as simple and easy too understand as possible.   

The book starts with the building blocks of the CPU, Boolean logic gates, and that is what I'll cover in this post.


<!-- more -->

#Logic Gates

The approach outlined in the book is to create a number of simple logic gates, from an initial NAND gate implementation that is provided for you.  

All of the gates I create here could have a much nicer implementation using pure pattern matching instead of making use of the previously defined gates.
In some cases I will provide both and have done so on GitHub if anyone is interested.

Therefore my first step was to create a NAND gate.

##Starting with NAND

As you may know, a NAND gate is a binary gate that returns false if both of it's inputs are true and true otherwise.  
A truth table for the gate is as follows:  

A|B|Output  
0|0|1  
0|1|1  
1|0|1  
1|1|0  

One great thing about using F# for modelling these gates is that we can utilise Pattern Matching to mirror the truth tables.

Therefore our NAND gate can be defined like so.

*)

let Nand a b = 
    match a, b with
    | false, false -> true
    | false, true -> true
    | true, false -> true
    | true, true -> false

(**
This can, of course, be further simplified to the following. (albeit no longer mirroring the truth table)
*)

let Nand a b = 
    match a, b with
    | true, true -> false
    | _, _ -> true

(** 
Now that we have our NAND implementation, we need to tackle the following gates

- NOT
- AND
- OR
- XOR
- Multiplexor (MUX)
- Demultiplexor (DMUX)

As well as some Multi bit versions and a multi way OR.

The following are implementations of the first six gates, made up of only previously defined gates (as per the books instructions).  

*)

let Not a = 
    Nand a a

let And a b = 
    Nand a b 
    |> Not

let Or a b =
    Nand (Nand a a) (Nand b b)

let Xor a b = 
    Or (And a (Not b)) (And (Not a) b) 

let Mux sel a b =
    Nand (Nand a sel) (Nand (Not sel) b)    

let DMux x sel =
    (And x (Not x), And x sel)



(**

Clearly they aren't the prettiest of functions due to the needed parentheses.  
Perhaps there is a better way of defining these functions when when creating them from the previously defined ones and I'd be interested in seeing the technique. 
However, by ignoring the guidelines in the book (where the purpose is to learn how all other logic gates can be created using NAND as a starting point), we can utilise pattern matching to clean things up.

The pattern matched versions more closely reflect the corresponding truth tables for the logic gates. 

*)

let Not a = function
    | true -> false
    | false -> true

let And a b = 
    match a, b with
    | true, true -> true
    | _, _ -> false

let Or a b =
    match a, b with
    | true, _ -> true
    | _, true -> true
    | _, _ -> false 

let Xor a b =
    match a, b with
    | true, false -> true
    | false, true -> true
    | _, _ -> false

let Mux sel a b  =
    match sel with
    | false -> a
    | _ -> b

let DMux sel x =
    match sel with
    | false -> (x,false)
    | _ -> (false,x)

(**
Now that they're out of the way, we can move on to some more interesting gates.

##Multi Bit Gates

Multi Bit gates are effectively arrays of the previous gates we have defined. We can therefore utilise some functional programming techniques in order to make implementing these quick and simple.

First we define a couple of functions to handle Unary and Binary gate arrays.

*)

let unaryArray gate bits =
    bits |> Array.map gate

let binaryArray gate aBits bBits =
    Array.zip aBits bBits 
    |> unaryArray (fun (a,b) -> gate a b)


(**

As you can see these functions both make use some useful array functions to help out and are overall nice and succinct.

Some noteworthy points from the above.

- I chose to use Zip to combine the two input arrays into corresponding pairs and pass that into the unaryArray function.
This allows for a nice clean binaryArray function but does mean the gate function we pass in, needs to work with tuples, not individual arguments.
This means we cannot just pass our previously created functions in. Fortunately we can simply use a lambda and pattern matching to extract the tuple parts succinctly within this binary gate function.

Now we can use partial application to create our multi bit versions of the simple gates.  

*)

let MultiNot = unaryArray Not

let MultiAnd = binaryArray And

let MultiOr = binaryArray Or

let MultiMux sel = 
    Mux sel
    |> binaryArray

let MultiDMux sel = 
    DMux sel
    |> unaryArray



(**
The functions above show how clean and concise these new multi bit versions of the gates become.  
One thing that isn't handled however is ensuring that the amount of bits is equal in both cases.

This is not something that I worried about at this stage, but I would like to make it more robust in the future.  


*)


(**
##Multi Way Gates
*)

let MultiWayOr bits = 
    bits |> Array.reduce Or

(**
The MultiWayOr function above maps perfectly to the Reduce function.  
It simply calls OR sequentially passing the result of the previous OR into the next -- TODO - re word and be precise
*)

(*** hide ***)
---
layout: post
title: Boolean Logic and Arithmetic with F#
date: 21/06/2015
comments: true
tags: ["fsharp","Emulation"]
catagories: ["Exploration","examples"]
series: F# and The Elements of Computing Systems
series-post-number: 1
meta: The elements of computing systems using FSharp
---