(*** raw ***)
---
layout: post
title: F# Units of Measure - A Working Example
date: 23/02/2015
comments: true
tags: ["fsharp"]
meta: Utilising FSharp Units of Measure for type safe calculations
---

(**

#F# Units of Measure

Have you ever wished that you could have type safe calculations throughout your application?  
Or have you wasted time hunting down errors due to using the wrong unit in a calculation?

If so then `FSharp Units of Measure` (UoM) will be right up your street.

In this post I will explore the various ways of using `F# Units of Measure` and the benefits this brings.  
I decided I would put `UoM` to the test by using them in a real world example and what could be a better application then that of brewing beer.  
Therefore the following is a worked example of various calculations used at various stages of the brewing process.

##Units of Measure - An introduction

Units of measure in F# are types that can be associated with floating point or signed integer values.  
By associating a `UoM` with a value it allows the F# compiler to perform additional type checking on the use of these units.

For example, we can declare a `UoM` for some of the measures of volume we will need when calculating our beer recipes.

*)

    ///Litre (or Liter in the US)
    [<Measure>] type L

    ///Us Gallon
    [<Measure>] type usGal

(**
Once we have these measures defined we can use them in a number of ways

- To restrict the parameters to a function - typically a calculation
- To return a specific unit of measure from a function
- For defining new units of measure in terms of the original

Here is an example of a function that can only take a value that is in `usGal`

*)

    ///Converts a potential extract value (PPG) and volume into total gravity points (Total sugar extraction)
    let totalGravityPoints (potential:float<ppg>) (vol : float<usGal>) =  
        (float potential * float vol) * 1.0<gp>

(**
The above example highlights a few things.

1. We have defined further units of measure to fully restrict the functions input and output.
2. In this case, it was more simple to cast the units to plain floats to do the calculation.


The reason for casting to float here is this.  
PPG is a value representing Gravity Points (gp - Also in the example) per lb of grain, per US Gallon of extract. 
When we multiply this by the volume we are saying that, for the given volume of extract with the given PPG, we would have the resulting value as total `gravity points`.

In order to convert between PPG and GP (Gravity Points) we either need to calculate out the units we don't need (ie: the lb and gallons in the `PPG` and `usGal` units),
or we simply remove the units of measure by casting to float as seen above.  
We could of cause treat gravity points as a `<ppg usGal>` unit of measure itself, but in this particular case, when this unit would only serve to complicate things, it is much clearer to do away with the units during the calculation.  
After all, the compiler has already done its job and prevented any incorrect units being passed to the function.

Finally we multiply by `1.0<gp>`. This simply converts the output of the function to a `float<gp>`. Just what we need.

Note: You can also use LanguagePrimitives.FloatWithMeasure to return a float with a given measure. This measure can either be specified explicitly or inferred by the type system.
*)