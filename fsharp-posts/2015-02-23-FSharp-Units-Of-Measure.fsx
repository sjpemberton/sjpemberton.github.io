(*** raw ***)
---
layout: post
title: F# Units of Measure - A Worked Example
date: 23/02/2015
comments: true
tags: ["fsharp"]
meta: Utilising FSharp Units of Measure for type safe calculations
---


(*** hide ***)
    [<Measure>] type gp

(**

#F# Units of Measure

Have you ever wished that you could have type safe calculations throughout your application?  
Or have you wasted time hunting down errors due to using the wrong unit in a calculation?

If so then `FSharp Units of Measure` (UoM) will be right up your street.

In this post I will explore the various ways of using `F# Units of Measure` and the benefits this brings.  
I decided I would put `UoM` to the test by using them in a real world example and what could be a better application then that of brewing beer.  
Therefore the following is a worked example of various calculations used at various stages of the brewing process.

<!-- more -->

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

Here is an example of a function that can only take values that have a dimension specified.

*)

    ///Converts a points per gal (gp / usGal) and volume into total gravity points in that volume
    let totalGravityPoints (potential:float<gp / usGal>) (vol : float<usGal>) =  
        potential * vol

(**

As you can see, this function is declared with explicit type annotations specifying the dimensions of the parameters.  
The FSharp compiler will now stop you from attempting to use this function with either dimensionless floats, or floats with the the wrong dimension.

Consider the following example where we attempt to call the function with dimensionless values:

*)

    let totalGravPoints = totalGravityPoints 240.0 5.0

(**
Attempting to compile this line of code produces the following error (of many) 
notifying us that we haven't satisfied the type constraints and preventing us from introducing possible errors in our code.


    [lang=output]
    error FS0001: This expression was expected to have type
    float<gp/usGal>    
      but here has type
    float 

Likewise the compiler will stop us from passing different `UoM` to the function. 
Suppose we attempted to pass a Litre value to the previous function instead of the expected Gallons.  

We receive a similar error as before.

*)
    let totalGravPoints = TotalGravityPoints 240.0<gp / usGal> 5.0<L>
(**
   

    [lang=output]
    error FS0001: Type mismatch. Expecting a
        float<usGal>    
    but given a
        float<L>    
    The unit of measure 'usGal' does not match the unit of measure 'L'

This example may be quite contrived, but imagine if that litre value came from the result of some other calculation.  
If we allowed any old float through when calculating our beers gravity points we could well end up with an extremely strong or weak beer and nobody wants that.

This brings us onto the second use of units of measure; As the value returned by a function.

Lets take the following function as an example;
*)

    ///Calculates the maximum potential gravity points for a given weight of grain with the given extract potential, divided by the target volume
    let MaxPotentialPoints (grainPotential:float<gp/lb>) (grain:float<lb>) (vol:float<usGal>) = 
        (grainPotential * grain) / vol

(**

The F# compiler correctly infers that the result of this function is of the type float<gp/usGal>  
    
    [lang=output]
    val MaxPotentialPoints :
      grainPotential:float<gp/lb> ->
        grain:float<lb> -> vol:float<usGal> -> float<gp/usGal>
  
So in this case, we do not need to do anything special in order to get the correct result.  
This is not always the case however.  

Sometimes we may need to take a few further steps in order to help the compiler infer the correct type.  
For example if we have a function that works on dimensionless values, but we want it to return one with a specific dimension, we have a few options.  

1. We can multiply the resulting unit by 1, where 1 is given the dimension type we want as the result.

*)
        let TotalGravityPoints potential vol =  
            (potential * vol) * 1.0<gp / usGal> 

(**
        [lang=output]
        val totalGravityPoints : potential:float -> vol:float -> float<gp/usGal>

2. We can explicitly declare the return type of the function and then use one of the helper functions `LanguagePrimitives.FloatWithMeasure` or 'LanguagePrimitives.IntWithMeasure'.
*)

        let TotalGravityPoints potential vol : float<gp / usGal> =  
            LanguagePrimitives.FloatWithMeasure (potential * vol);;

(**
        [lang=output]
        val totalGravityPoints : potential:float -> vol:float -> float<gp/usGal>


We can also alter the returned type when UoM are passed into the function by calculating out the units we do not want.

*)
       
       
(**
It really is down to personal preference which direction you take here. I prefer to let the type system do the hard work and so only specify the minimum required.
*) 

(**

A few things need to be mentioned when working with `UoM`.

1. You can declare a unit of measure to be made up of other units of measure.

2. You can interchange UoM as long as they are equivalent. For example we could pass a ppg into the above function.

3. In order to remove units from a measure you must either calculate them out or cast to float (effectively removing them).

4. You can use generics with units of measure.

Lets look at these points in more detail while staying in our domain of brewing.

##Composing measures from measures

    [<Measure>] type ppg = gp / usGal

##Units of measure equivalence

##Converting to and from units of measure

PPG is a value representing Gravity Points (gp - Also in the example) per lb of grain, per US Gallon of extract. 
When we multiply this by the volume we are saying that, for the given volume of extract with the given PPG, we would have the resulting value as total `gravity points`.

In order to convert between PPG and GP (Gravity Points) we either need to calculate out the units we don't need (ie: the lb and gallons in the `PPG` and `usGal` units),
or we simply remove the units of measure by casting to float as seen above.  
We could of cause treat gravity points as a `<ppg usGal>` unit of measure itself, but in this particular case, when this unit would only serve to complicate things, it is much clearer to do away with the units during the calculation.  
After all, the compiler has already done its job and prevented any incorrect units being passed to the function.

Finally we multiply by `1.0<gp>`. This simply converts the output of the function to a `float<gp>`. Just what we need.

Note: You can also use LanguagePrimitives.FloatWithMeasure to return a float with a given measure. This measure can either be specified explicitly or inferred by the type system.




*)