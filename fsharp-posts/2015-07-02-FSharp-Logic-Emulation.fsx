
(*** hide ***)
module FsEmulation


(**
So, I've had this book (The Elements of Computing Systems) on my shelf for a good few years and never got round to reading it, yet alone completing the exercises within.

<img src="/content/images/post-images/tecs_cover.jpg" alt="Drawing" style="width:220px; float:right; margin:20px;"/>

Therefore I thought I'd finally read it.  
I also decided I would complete the exercises in a different way than suggested in the book. By using F#!  

Rather than utilising the Hardware Definition Language and the given emulator as suggested. I will model all of the parts of the computer in F#. 

Doing so, I hope to utilise various functional programming techniques, alongside F# specific features to keep the solutions as simple and easy too understand as possible.   
Hopefully learning something new on the way.

As I work my way through the book I'll be adding to this series.  
I aim to cover one collection of exercises (roughly a chapter) per post, however this initial post will cover two, just to get me going.

The book starts with the building blocks of the CPU, Boolean logic gates.
Let's dive straight in.

<!-- more -->

#Logic Gates

The approach outlined in the book is to create a number of simple logic gates, composed of previously defined gates. 
It starts you off with an initial NAND gate implementation that you can then utilise to create the next gate, which can then also be used in subsequent gates, and so on..  

I kind of like this approach from an academic point of view. It makes for some interesting problem solving activities.  
Therefore, I'll follow the approach in the book, even though I know it won't result in the cleanest, most idiomatic F# code. (At least not that I can currently create. Please feel free to give me some pointers) 

That being said, all of the gates I create have a much cleaner implementation by utilising pattern matching instead of making use of the previously defined gates.
I will also provide these for most of the implementations.

Right then, first up we need to replicate that NAND gate the book provided for us.

##Starting with NAND

As you may know, a NAND gate is a binary gate that returns false if both of it's inputs are true or returns true otherwise.  
A truth table for the gate is as follows.

    [lang=output]
    |   a   |   b   |  out  |
    |   0   |   0   |   1   |
    |   0   |   1   |   1   |
    |   1   |   0   |   1   |
    |   1   |   1   |   0   |


One great thing about using F# and Pattern Matching for modelling these gates is that they end up mirroring the truth tables.

Therefore our NAND gate can be defined like so.

*)

let Nand a b = 
    match a, b with
    | false, false -> true
    | false, true -> true
    | true, false -> true
    | true, true -> false

(**
This can, of course, be further simplified to the following. (albeit no longer mirroring the truth table above)
*)

(***hide***)
module PatternMatched =
(***)

    let Nand a b = 
        match a, b with
        | true, true -> false
        | _, _ -> true

(** 
Now that we have our NAND implementation, the book instructs us to tackle the following gates in the order given.  
The order is only important so that we can use each of the implementations in subsequent gates.

- NOT
- AND
- OR
- XOR
- Multiplexor (MUX)
- Demultiplexor (DMUX)

In addition there are some Multi bit versions of these gates, and a multi way OR. We'll look at these in more detail when we get to them.

The following are implementations of the first six gates, made up of only previously defined gates (as per the books instructions).  
I have included a truth table and circuit diagram for each of them. The circuit diagrams are created using only previously defined gates, as the book intended.   
In order to save space and keep the post concise (well, shorter at least), they are initially hidden, just hover or click the link to view them.
*)

(**
<a class="expandPrompt">NOT Gate Details</a>
<div class="hoverPopup" >

    [lang=output]
    NOT GATE
    |   a   |  out  |
    |   0   |   1   |
    |   1   |   0   |

<img src="/content/images/post-images/NOT.png" alt="NOT Gate" style="float:right; margin:20px;width:300px"/>

</div>
*)

let Not a = 
    Nand a a

(**
<a class="expandPrompt">AND Gate Details</a>
<div class="hoverPopup" >

    [lang=output]
    AND GATE
    |   a   |   b   |  out  |
    |   0   |   0   |   0   |
    |   1   |   0   |   0   |
    |   0   |   1   |   0   |
    |   1   |   1   |   1   |

<img src="/content/images/post-images/AND.png" alt="AND Gate" style="float:right; margin:20px;width:300px"/>

</div>
*)

let And a b = 
    Nand a b 
    |> Not

(**
<a class="expandPrompt">OR Gate Details</a>
<div class="hoverPopup" >

    [lang=output]
    OR GATE
    |   a   |   b   |  out  |
    |   0   |   0   |   0   |
    |   1   |   0   |   1   |
    |   0   |   1   |   1   |
    |   1   |   1   |   1   |

<img src="/content/images/post-images/OR.png" alt="OR Gate" style="float:right; margin:20px;width:300px"/>

</div>
*)

let Or a b =
    Nand (Nand a a) (Nand b b)

(**
<a class="expandPrompt">XOR Gate Details</a>
<div class="hoverPopup" >

    [lang=output]
    AND GATE
    |   a   |   b   |  out  |
    |   0   |   0   |   0   |
    |   1   |   0   |   1   |
    |   0   |   1   |   1   |
    |   1   |   1   |   0   |

<img src="/content/images/post-images/XOR.png" alt="XOR Gate" style="float:right; margin:20px;width:300px"/>

</div>
*)

let Xor a b = 
    Or (And a (Not b)) (And (Not a) b) 

(**
<a class="expandPrompt">MUX Gate Details</a>
<div class="hoverPopup" >

    [lang=output]
    MUX GATE
    |  sel  |   a   |   b   |  out  |
    |   0   |   0   |   0   |   0   |
    |   0   |   0   |   1   |   0   |
    |   0   |   1   |   0   |   1   |
    |   0   |   1   |   1   |   1   |
    |   1   |   0   |   0   |   0   |
    |   1   |   0   |   1   |   1   |
    |   1   |   1   |   0   |   0   |
    |   1   |   1   |   1   |   1   |

<img src="/content/images/post-images/MUX.png" alt="MUX Gate" style="float:right; margin:20px;width:300px"/>

</div>
*)

let Mux sel a b =
    Nand (Nand a sel) (Nand (Not sel) b)    

(**
<a class="expandPrompt">DMUX Gate Details</a>
<div class="hoverPopup" >

    [lang=output]
    DMUX GATE
    |  sel  |   in   |  outA  |  outB  |
    |   0   |   0    |   0    |   0    |
    |   1   |   0    |   0    |   0    |
    |   0   |   1    |   1    |   0    |
    |   1   |   1    |   0    |   1    |

<img src="/content/images/post-images/DMUX.png" alt="DMUX Gate" style="float:right; margin:20px;width:300px"/>

</div>
*)

let DMux a sel =
    (And a (Not a), And a sel)



(**

Clearly they aren't the prettiest of functions due to the needed parentheses. They don't tend to suite the functional style. (or maybe that's just my lack of experience)  

Perhaps there is a better, more functional way of creating these functions from the previously defined ones. I'd be interested in seeing some similar solutions or techniques, so feel free to give me some pointers if you wish. 

That being said, It is clear that just like before and by ignoring the guidelines in the book (where the purpose is to learn how all other logic gates can be created using NAND as a starting point), we can again utilise pattern matching to clean things up.

The pattern matched versions again, more closely reflect the corresponding truth tables for the logic gates. 
The implementations are given below.

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
This is all we require at this stage of working through the book, but i expect we will need some more at a latter stage.

*)

let unaryArray gate bits =
    bits |> Array.map gate

let binaryArray gate aBits bBits =
    Array.zip aBits bBits 
    |> unaryArray (fun (a,b) -> gate a b)


(**

As you can see these functions both make use some useful array functions and are overall nice and succinct.

Some noteworthy points from the above.

- I chose to use Zip to combine the two input arrays into corresponding pairs and pass that into the unaryArray function.
This allows for a nice, clean, binaryArray function but does mean the gate function we pass in, needs to work with tuples, not individual arguments.
This means we cannot simply use our previously created functions in.  
Fortunately, we can use a simple lambda expression combined with pattern matching to extract the tuple parts succinctly within this binary gate function.

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

- It's also worth pointing out that I have completely diverged from the book here.
To implement these gates in the way the book expects we would need to iteratively call the simple gates, passing in the appropriate pairs of input bits and collecting the results into a new array.  
This is exactly what we get for free from `Array.zip` and `Array.map`!

*)


(**
##Multi Way Gates

Next up, we can tackle the multi way gates.  
Multi way gates have an arbitrary number of inputs and outputs, as opposed to a fixed number like our previous gates.  
These inputs can also be multi bit arrays.

First, we can tackle that Multi Way OR gate I mentioned earlier.  
It's a simple chip that consists of a series of OR Gates.  

The first two bits are passed to the first OR gate, and then the third bit plus the result of the first OR operation are fed into the next gate.
This pattern continues for all bits provided.

Hmmm, this sounds familiar, I think we'll skip the explicit definition and jump straight to a concise F# specific one.
*)

let MultiWayOr bits = 
    bits |> Array.reduce Or

(**
As you can see, the MultiWayOr function above maps perfectly to F#s built in Reduce function.  

Next up is Multi Way MUX.  

This gate is specified in the book as having 4 inputs, all 16 bit wide.  
We don't need to restrict ourselves to this, but doing so simplifies the solutions due to not having to handle errors with incorrect input amounts, so I will follow that approach.

The number of control bits (the `sel` param) is equal to log<sub>2</sub>m where m is the number of inputs.  
In this case, that means 2 bits. Again it would be nice to add some error handling around this, but that will have to wait for another time. 

The gate is basically just three calls to our MultiMux gate as follows.
*)

let Mux4Way16 a b c d (sel:bool array) = 
    let m1 = MultiMux sel.[0] a b 
    let m2 = MultiMux sel.[0] c d
    MultiMux sel.[1] m1 m2 

(**
The book states we also need a 4 way, 16 bit version of this gate.  
We can easily create this by using the 4 way version from above.
*)

let Mux8Way16 a b c d e f g h (sel:bool array) =
    let m1 = Mux4Way16 a b c d sel.[0..1]
    let m2 = Mux4Way16 e f g h sel.[0..1]
    MultiMux sel.[2] m1 m2 

(**
Up next, 4 way and 8 way DMUX.  

These gates have the same amount of selection bits as there MUX counter parts and a single input.  
The multi way in this case, relates to the varying number of output bits.

The purpose of multi way DMUX is to 'pipe' the input into a particular output based on the control bits.
*)

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

There are five chips included in the arithmetic section outlined in the book. These are:

- HalfAdder
- FullAdder
- MulitBitAdder (16 Bit)
- MultiBitIncrementer (16 Bit)
- ALU (Arithmetic Logic Unit)

A half adder is designed to add 2 bits.  
The 2 bits returned can be thought of as the sum and the carry resulting from the addition.
(The least significant bit (LSB) is the sum and the most significant bit (MSB) is the carry.  

It turns out that the truth table for a half adder highlights that the sum and carry can be implemented with an XOR and an AND gate respectively (This can be seen by hovering the link below).  

<a class="expandPrompt">Half Adder Details</a>
<div class="hoverPopup" >

    [lang=output]
    Half Adder
    |   a   |   b   | carry |  sum  |
    |   0   |   0   |   0   |   0   |
    |   0   |   1   |   0   |   1   |
    |   1   |   0   |   0   |   1   |
    |   1   |   1   |   1   |   0   |

<img src="/content/images/post-images/HalfAdder.png" alt="Half Adder" style="float:right; margin:20px;width:300px"/>

</div>
*)

let HalfAdder a b = 
    let sum = Xor a b
    let carry = And a b
    (sum,carry)

(**
A full adder is designed to add 3 bits. 
It is implemented by adding pairs of bits using the half adder we just created.  
  
The second pair of bits added consists of the result of the first addition.  
The function then returns the LSB and the MSB as the sum and carry as before.

<a class="expandPrompt">Full Adder Details</a>
<div class="hoverPopup" >

    [lang=output]
    Full Adder
    |   a   |   b   |   c   | carry |  sum  |
    |   0   |   0   |   0   |   0   |   0   |
    |   0   |   0   |   1   |   0   |   1   |
    |   0   |   1   |   0   |   0   |   1   |
    |   0   |   1   |   1   |   1   |   0   |
    |   1   |   0   |   0   |   0   |   1   |
    |   1   |   0   |   1   |   1   |   0   |
    |   1   |   1   |   0   |   1   |   0   |
    |   1   |   1   |   1   |   1   |   1   |

<img src="/content/images/post-images/FullAdder.png" alt="Full Adder" style="float:right; margin:20px;width:400px"/>
</div>

*)

let FullAdder a b c = 
    let (s1,c1) = HalfAdder a b
    let (sum,c2) = HalfAdder s1 c
    (sum, Or c1 c2)

(**
Next up, we have the fully fledged Adder chip.  
The adder is responsible for adding integers represented as bits.  
It can therefore take bit arrays of any size (we will most likely only need up to 32 bits).  

The chip outlined in the book is an example of a ripple carry adder. 
This means that the bits are added together in order, starting from the LSB and any carry is carried over into the next addition.

This obviously means each stage of the addition (except the first) has 3 bits. This fits perfectly to our Full Adder from above.  

So, all we need to do is implement a recursive function that adds the given bits starting from the end of the arrays.

*)
let Adder aBits bBits =
    let rec addBits aBits bBits carry accu = 
        match aBits, bBits with
        | aHead :: aTail, bHead :: bTail -> 
            let (sum,c) = FullAdder aHead bHead carry
            addBits aTail bTail c (sum :: accu)
        | [],_
        | _,[] -> accu
    addBits (aBits |> Array.rev |> Array.toList) (bBits |> Array.rev |> Array.toList) false List.empty
    |> List.toArray

(**
The adder function allows us to add together any pair of 2's compliment binary integers.

Lastly, the increment function is simply a partially applied Adder function with one of the parameters fixed to a binary representation of 1.
*)

//In plus one
let Increment aBits = Adder aBits [| for i in 1 .. 16 -> match i with | 16 -> true | _ -> false |]

(**
##The Arithmetic Logic unit

Finally, we can bring all of our work together in the implementation of the Arithmetic Logic Unit (ALU).  

This ALU is specified in the book. It has 8 inputs consisting of two 16 bit data inputs, and 6 control bits.  
These control bits are used to select the function that the ALU executes; They are.

- zx - Zeros the xBits input
- nx - Negates the xBits input
- zy - Zeros the yBits input
- ny - Negates the yBits input
- f - The function selecter. If 1 Addition is performed else AND is performed.
- no - Negates the output

The ALU provides three outputs.

- The result of the function (Addition or AND)
- zr - Indicates whether the output is equal to zero
- ng - Indicates whether the output is negative

The truth table for the ALU is far to big for me to copy out, but I'm sure you could found it with some quick google-foo if desired.

My implementation of the ALU is shown below. As you can see, it's pretty straight forward.
*)

let ALU xBits yBits zx nx zy ny f no = 
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
    let o4 = Adder ox2 oy2 //addition would be

    //Output
    let o5 = MultiMux f o3 o4 //Choose and or addition
    let no5 = MultiNot o5 //Negated out would be
    let out = MultiMux no o5 no5 //Choose to negate or not

    let zr = Not (MultiWayOr out)
    let ng = MultiWayOr (MultiAnd out [|for i in 1..16 -> match i with | 16 -> true | _ -> false|] )
    
    (out, zr, ng)

(**
 
Clearly though, this implementation does have some wasteful processing.  

By abiding to the books rules we have to execute individual logic paths to produce their respective results before switching between them based on a specific control bit.

Therefore, we can immediately refactor this as follows:

*)
(***hide***)
module AlternateALU =
(***)

    let ALU xBits yBits zx nx zy ny f no = 
        let zero c b = if c then [| for i in 1..16 -> false |] else b 
        let negate c b = if c then MultiNot b else b
        let x = xBits |> zero zx |> negate nx 
        let y = yBits |> zero zy |> negate ny 

        //Apply function and negate if needed
        let out = if f then MultiAnd x y else Adder x y
                  |> negate no
         
        (out, MultiWayOr out |> Not, MultiAnd out [| for i in 1..16 -> match i with | 16 -> true | _ -> false |] |> MultiWayOr)

(**

Much better!  

All we need to do now is get testing.

##Testing

Finally, it's time to get some tests executed. The book provides comparison files in order to check our output when using the recommended Hardware simulator and Hardware Definition Language (HDL) code.

I decided it would be a fun exercise to utilise these within my tests.  

So, what we need is a way to convert a string and/or integer representation of bits in to arrays of Boolean values for use with my Gates (I could have implemented my gates using integers of course).  

In addition we need to read the provided comparison file and compare it to our results.  
This means we need to return our results in the correct format (Also a string representation of bits!) 

Lets get to it.

First up, converting a string (A sequence of chars) to an array of integers.  
In actual fact, what we need is a bit different to that as we only care about ones and zeros, obviously.
Therefore I will simply pattern match the char '1', to the integer 1 and anything else to zero.  

This may not be the most robust implementation, as we would really want to trigger an error, or return nothing if the string is invalid (eg. if it contained characters other than '1' or '0').

I have also created a string version of the function (as opposed to working on chars). 
*)

(***hide***)
module Utilities =

    open System
    open System.IO

(***)

    let stringToInt = function 
        | "1" -> 1
        | _ -> 0

    let charToInt = function 
        | '1' -> 1
        | _ -> 0

(**

Next up, we create a quick helper to convert an integer into a Boolean.
I've done this in the same manner as the string converter. I have purposefully ignored the fact we could have incorrect input values.

*)

    let intToBool = function 
        | 1 -> true
        | _ -> false

(**
We will also need the inverse of these functions, Boolean to integer and integer to string.
*)

    let boolToInt = function 
        | true -> 1
        | _ -> 0

    let intToString = function 
        | 1 -> "1"
        | _ -> "0"

    


(**
To complete the set we then compose these together to create straight string to Boolean and Boolean to string functions.  
I also defined two additional functions to convert arrays of integers and Boolean values to a single string.

*)

    let stringToBool = stringToInt >> intToBool
    let boolToString = boolToInt >> intToString

    let intsToString = Array.map intToString >> String.concat ""
    let boolsToString = Array.map boolToInt >> intsToString


(**

All of these simple functions can then be used with the built in Array/List module functions to convert entire collections as needed.

To do the actual testing I will use the provided comparison files supplied with the books software.

These files, are basically truth tables.  
An example of the one supplied for AND is as follows:

    [lang=output]
    |   a   |   b   |  out  |
    |   0   |   0   |   0   |
    |   0   |   1   |   0   |
    |   1   |   0   |   0   |
    |   1   |   1   |   1   |

So, I need to parse this, create the inputs and then compare the given output with our functions output. 
Sounds simple, right?

First off, lets read a file in and create a list of the rows from the given table.

*)

    let parseFile path = 
        File.ReadAllLines path
        |> Seq.map (fun s -> 
               s.Split([| "|" |], StringSplitOptions.RemoveEmptyEntries)
               |> Array.map (fun s -> s.Trim())
               |> Array.toList)
        |> Seq.toList

(**
    [lang=output]
    val x : string list list =
      [["a"; "b"; "out"]; ["0"; "0"; "0"]; ["0"; "1"; "0"]; ["1"; "0"; "0"];
       ["1"; "1"; "1"]]

As you can see this gives us a list of lists representing the values per column per row.  
We now need to convert this into a list of maps. So that the arguments can be accessed by name.  

This is purely a convenience for me as I have renamed and rearranged the functions and there parameters in some cases.
If I had stuck with the same naming conventions and argument order as the book, I likely wouldn't need to do this.

*)

    let rec createMap matrix (cols : string list) = 
        match matrix with
        | row :: rest -> 
            [ row
              |> List.mapi (fun i x -> (cols.[i], x))
              |> Map.ofSeq ]
            @ createMap rest cols
        | _ -> []

(**
Finally we need a function to act as our test runner.  
This will take a file name, parse the file, create a list of test case data and then call a provided test function.

The test function provided will need to map the test case data (using the map keys) to the actual parameters and compare the result in a specific manner to determine success (or failure).

*)

    let executeTests path func = 
        let data = parseFile path
        let testData = createMap (List.tail data) (List.head data)
        let rec execute func testData num = 
            match testData with
            | case :: rest -> 
                sprintf "Test number %i - %s \n" num  (if func case then "Success" else "Failure") + execute func rest (num+1)
            | [] -> "All tests complete"
        execute func testData 0

(**
Lets put it to the test.  
I'll start simple and run the test for the And functions test file shown above.  

Here is my test function and the use of it.
*)

    let andTest (case : Map<string, string>) = 
        And (stringToBool case.["a"]) (stringToBool case.["b"]) = stringToBool case.["out"]

    executeTests @"content\post-files\Emulation\And.cmp" andTest

(**
    [lang=output]
    val it : string =
      "Test number 0 - Success 
       Test number 1 - Success 
       Test number 2 - Success 
       Test number 3 - Success 
       All tests complete"

I could further extend this to log each parameter value as the test function is called, thus giving us full visibility of the variables.  
For now though I'll leave it as is. (The post is already long enough! :) )

Another example is given below; It is the test case for the Increment function.  
The partial truth table (test file) is as follows.

    [lang=output]
    |        in        |       out        |
    | 0000000000000000 | 0000000000000001 |
    | 1111111111111111 | 0000000000000000 |
    | 0000000000000101 | 0000000000000110 |
    | 1111111111111011 | 1111111111111100 |
*)

    let IncTest (case : Map<string, string>) = 
        case.["in"]
        |> Seq.map (charToInt >> intToBool)
        |> Seq.toArray
        |> Increment
        |> boolsToString = case.["out"]

(**
I also thought it would be useful (and more importantly it was fun!) to make some actual decimal integer to binary (so base 10 to base 2) converters and vice versa.

The following converts a decimal to binary (it doesn't handle negatives, we will get to that later).
*)

    let toBinary i = 
        let rec convert i acc = 
            match i with
            | _ when i > 0 -> (i % 2) :: convert (i / 2) acc
            | _ -> acc
        convert i [] |> List.rev |> List.toArray 

(**
Next we require a function to flip each bit in a sequence. This is required for converting an integer into a two's compliment binary representation when the number is negative.
*)

    let flipBits b = 
        let rec convert b acc = 
            match b with
            | h :: t -> 
                match h with
                | 1 -> 0 :: convert t acc
                | _ -> 1 :: convert t acc
            | [] -> acc
        convert (b |> List.ofSeq) []
        |> List.toArray

(**
As part of that conversion to two's compliment, we also need a function to pad a binary array with zeros depending on the given maximum bit length.
*)

    let padBits length (bits : int array) =
        let padding = [| for i in 1..(length - bits.Length) -> 0 |]
        Array.concat [|padding; bits|]

(**
Finally, we make use of the Increment function we have already defined as part of our Boolean arithmetic functions in order to complete what we need to fully represent a decimal in twos compliment binary form.  

The basic steps to the function are:

1. Take in a decimal integer and the target bit length.
2. Determine whether the integer is negative.
3. If it is:
    1. Get the absolute value
    2. Convert that value to binary
    3. Pad the binary with zeros up until the bit length
    4. flip all the bits
    5. Add one
4. If it is not:
    1. Simply convert to binary and pad the bits
 
*)

    let toTwosCompliment i b = 
        match i with
        | _ when i < 0 -> 
            abs i
            |> toBinary
            |> padBits b
            |> flipBits |> Array.map intToBool
            |> Increment |> Array.map boolToInt
        | _ -> 
            i
            |> toBinary
            |> padBits b

(**
this will now allow us to get the binary representation of any integer returned in an array of Boolean values for use in our arithmetic functions.

The functions to convert back from binary to base 10 are just as simple.

To convert to a positive decimal we do the following steps:

1. Reverse the bits (As we need to work right to left)
2. For each bit:
    1. Multiply the value by the base (2)
    2. Raise it to it's index in the binary array.

And that's all there is to it.  

For a negative number we first need to convert to two's complement.  
This involves padding, flipping the bits and adding one (Exactly as when converting to a negative binary representation), before converting to base 10 as described above.

*)
    let toBase10 b = 
        let rec convert b i acc = 
            match b with
            | h :: t -> float h * 2.0 ** i + convert t (i + 1.0) acc
            | [] -> acc
        convert (b |> Array.rev |> Array.toList) 0.0 0.0 |> int

    let toDecimal b (binary : int array) =
        match binary.[0] with
        | 0 -> binary |> toBase10
        | _ -> 
            -(binary
            |> padBits b
            |> flipBits |> Array.map intToBool
            |> Increment |> Array.map boolToInt 
            |> toBase10)

(**
Phew!, let's give these new functions a test.  
*)

(***hide***)
module tests = 

    open Utilities
(***)
    //Adding 100 and 320.

    let a = 
        toTwosCompliment 100 16 
        |> Array.map intToBool

    let b = 
        toTwosCompliment 320 16 
        |> Array.map intToBool

    let result = 
        Adder a b
        |> Array.map boolToInt
        |> toDecimal 16

(**
    [lang=output]

    val a : bool [] =
      [|false; false; false; false; false; false; false; false; false; true; true;
        false; false; true; false; false|]

    val b : bool [] =
      [|false; false; false; false; false; false; false; true; false; true; false;
        false; false; false; false; false|]

    val result : int = 420

    
*)
    //Incrementing a negative number.

    let negative = 
        toTwosCompliment -112 16 
        |> Array.map intToBool

    let negativePlus1 = 
        Increment negative
        |> Array.map boolToInt
        |> toDecimal 16

(**
    [lang=output]

    val negative : bool [] =
      [|true; true; true; true; true; true; true; true; true; false; false; true;
        false; false; false; false|]

    val negativePlus1 : int = -111
*)

(**

Wow that turned out to be a long post. Thanks for hanging on in!  

I hope some of you find something useful to take from this post, be it F#, Boolean logic/arithmetic or book related.  

I'll try and keep the future posts in the series shorter, however I'm already thinking that I need to revisit some of the functions in this post to make them more robust.

As always, the code and any supporting material can be found on [GitHub] and I would love for people to share there advice or experience in F# to boost my understanding.

[GitHub]: https://github.com/sjpemberton/FSharpEmulator

*)

(*** hide ***)
---
layout: post
title: Boolean Logic and Arithmetic with F#
date: 02/07/2015
comments: true
tags: ["fsharp","Emulation"]
catagories: ["Exploration","examples"]
series: F# and The Elements of Computing Systems
seriesId: FSharpLogic
series-post-number: 1
meta: The elements of computing systems using FSharp for Boolean Logic and Arithmetic Emulation
---