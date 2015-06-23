
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

(***hide***)
module PatternMatched =

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

(***hide***)
module PatternMatched2 =

(***)

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

This is not something that I am worried about at this stage, but I would like to make it more robust in the future.  

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

let Mux4Way16 a b c d (sel:bool array) = 
    let m1 = MultiMux sel.[0] a b 
    let m2 = MultiMux sel.[0] c d
    MultiMux sel.[1] m1 m2 

let Mux8Way16 a b c d e f g h (sel:bool array) =
    let m1 = Mux4Way16 a b c d sel.[0..1]
    let m2 = Mux4Way16 e f g h sel.[0..1]
    MultiMux sel.[2] m1 m2 

let DMux4Way x (sel:bool array) = 
    let (d1,d2) = DMux x sel.[1]
    let (a,b) = DMux d1 sel.[0]
    let (c,d) = DMux d2 sel.[0]
    (a,b,c,d)

let DMux8Way x (sel:bool array) = 
    let (d1,d2) = DMux x sel.[2]
    let (a,b,c,d) = DMux4Way d1 sel.[0..1]
    let (e,f,g,h) = DMux4Way d2 sel.[0..1]
    (a,b,c,d,e,f,g,h)

(**

The multi way gates above all utilise previous ones we've created.  
I chose to use some simple inline pattern matching to reduce the fluff by decomposing the results of the functions in turn before then returning the results in a large tuple.

Of course, we can again use pattern matching instead to do the selection. I won't bother with those implementations though, as I would rather get onto the fun bits.

So, next up, we finally get to some Boolean Arithmetic!

##Boolean Arithmetic

There are five parts to the arithmetic section outlined in the book. These are:

- HalfAdder
- FullAdder
- MulitBitAdder (16 Bit)
- MultiBitIncrementer (16 Bit)
- ALU (Arithmetic Logic Unit)

*)

let HalfAdder a b = 
    let sum = Xor a b
    let carry = And a b
    (sum,carry)

let FullAdder a b c = 
    let (s1,c1) = HalfAdder a b
    let (sum,c2) = HalfAdder s1 c
    (sum, Or c1 c2)

//Ripple Carry Adder Implementation
let Adder aBits bBits =
    let rec addBits aBits bBits carry accu = 
        match aBits, bBits with
        | aHead :: aTail, bHead :: bTail -> 
            let (sum,c) = FullAdder aHead bHead carry
            addBits aTail bTail c (sum :: accu)
        | [],_
        | _,[] -> accu
    addBits (aBits |> List.rev) (bBits |> List.rev) false List.empty
    |> List.toArray

//In plus one
let Incrementer aBits = Adder (aBits |> List.ofArray) [ for i in 1 .. 16 -> match i with | 16 -> true | _ -> false ]

let ALU xBits yBits nx zx ny zy f no = 
    //handle x    
    let ox1 = MultiMux zx xBits [|for i in 1..16 -> false |]  //Zero all X bits if zx
    let nox1 = MultiNot ox1 //What would this be if negated
    let ox2 = MultiMux nx ox1 nox1 //Select based on nx

    //handle y
    let oy1 = MultiMux zy yBits [|for i in 1..16 -> false |]  //Zero all X bits if zy
    let noy1 = MultiNot oy1 //What would this be if negated
    let oy2 = MultiMux ny oy1 noy1 //Select based on ny

    //handle & / +
    let o3 = MultiAnd ox2 oy2 //an and would be
    let o4 = Adder (List.ofArray ox2) (List.ofArray oy2) //addition would be

    //Output
    let o5 = MultiMux f o3 o4 //Choose and or addition
    let no5 = MultiNot o5 //Negated out would be
    let out = MultiMux no o5 no5 //Choose to negate or not

    let zr = Not (MultiWayOr out)
    let ng = MultiWayOr (MultiAnd out [|for i in 1..16 -> match i with | 16 -> true | _ -> false|] )
    
    (out, zr, ng)

(**

As we can see, the half and full adder implementations are nice and simple.  
Then the actual Ripple Carry Adder implementation draws on some nice functional techniques to provide a clean alternative to the way that I assume is expected in the book.
That is, a 16 Bit implementation is actually 16 calls to FullAdder (As the Adder Chip is effectively an N bit array of full adder chips), working from Least significant bit, to the most (right to left) with the carry seeded into subsequent calls as required. 

Finally the ALU is straight forward too.  
It does however have some wasteful logic. By abiding to the books rules we have to execute to logic paths to produce their respective results before switching between them based on a specific control bit.

Therefore, we can immediately refactor this as follows:

*)

//TODO - F# Style ALU implementation

(**

##Testing

Finally, it's time to get some tests executed. The book provides comparison files in order to check our output when using the recommended Hardware simulator and HDL code.

I decided it would be a fun exercise to utilise these within my tests.  

What we need is a way to convert a string and/or integer representation of bits in to arrays of Boolean values for use with my Gates.  
I therefore created some util functions.

In addition we need to read the provided comparison files and compare to our results.  
This means we need to return our results in the correct format (Also string representation of bits!) 

Lets get to it.





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