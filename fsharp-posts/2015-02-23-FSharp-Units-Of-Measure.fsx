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
Well through the use of `FSharp Units of Measure` (UoM), now you can!

In this post I will explore the various ways of using [FSharp Units of Measure] and the benefits they bring. 
To do this, I've decided to put `UoM` to the test by applying them in a real world example; The calculations required to brew some beer.  

What follows is a worked example of some of the calculations used at various stages of the brewing process, highlighting how, through the use of 'Units of Measure', we can help to ensure correctness and brew some good beer.

<!-- more -->

##Units of Measure - An introduction

Units of measure in F# are a type of metadata that can be associated with floating point or signed integer values.  
By associating a `UoM` with a quantity value it allows the F# compiler to perform additional type checking on the use of these units, enforcing relationships between units in arithmetic and reducing potential errors.

To declare a `Unit of Measure` we use the `[<Measure>]` attribute, followed by the type keyword and the name we want to give the measure.

For example, we can declare `Units of measure` for some of the measures of volume we will need when calculating the ingredients in our beer recipes.

*)

    ///Litre (or Liter in the US)
    [<Measure>] type L

    ///Us Gallon
    [<Measure>] type usGal

(**

Using these measures is as simple as annotating a float literal.
    
    //Volume in litres
    let volume = 120<L>

After defining some of the measures we need, they can be utilised in a number of ways.
 
- For defining new units of measure in terms of the original
- To constrain the values involved in calculations to particular measures
- To provide type safe conversions between measures
- In types to create associations between different measures

These a re just a few of the ways I regularly utilise `measures` in my own code.  
If anyone has any other useful applications I would love to hear about them.

##Defining units of measure in terms of others

Sometimes, it can be a useful technique to define a unit of measure in terms of other, previously defined measures.  
Doing so allows us to use the 'derived' measures in place of inferred results of calculations and can increase code clarity.

Let's take an example from our brewing process.  
We often need to associate something called gravity points, with a volume of liquid.  
Gravity points are a very simplified definition of the amount of sugar in liquid. Obviously, this liquid could be in any number of units of measure, and it is paramount that we ensure we do not mix measures of volume (or related measures) during recipe planning.

 The most common measurement used in home brewing circles is that of points per gallon (or points per pound per gallon - PPG) so let's define a measure for that.  
 Firstly, we need to define a measure for gravity points.

 *)
    ///Gravity Point - A Simplified brewing unit for amount of sugar dissolved in solution
    [<Measure>] type gp

(**
Next up, we define the association between gravity points and US gallons.
*)

    [<Measure>] type ppg = gp / usGal

(**
Our ppg measure can now be used in our calculations, which we'll get to in a minute.  
But first, a few quick points on this type of measure.

 - The formulas that represent the measures can be written in various equivalent ways. This sometimes manifests in the results of expressions - more on this later.
 - Equivalent formulas are compiled into a common representation and can therefore be substituted freely.
 - You cannot use numeric values in these formulae. However, we can declare conversion constants which we will also see later.

These points and others are explained in detail on the [MSDN] page for Units of measure.

##Using Units of Measure for error prevention

Units of measure come in extremely handy for preventing us introducing errors into our code due to passing the wrong value to a function.  
As an example taken from the world of brewing, we wouldn't want to mix up the units when making calculations about how much grain we need.

Below is an example of a function that can only take values that have a dimension specified.

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
            LanguagePrimitives.FloatWithMeasure (potential * vol)

(**
        [lang=output]
        val totalGravityPoints : potential:float -> vol:float -> float<gp/usGal>


Elsewhere, where units of measure are needed to be converted into others, you can either cast to float/int to remove the dimensions or calculate out the units you don't want.  
Personally if I need to take one of these approaches, I opt for casting to remove the dimensions before using the multiply by one trick in order to return the result as the unit of measure I want as it tends to make code clearer than having to multiply/divide by multiple different units of measure.
The inner code would not be type safe, but we know that the passed in values will be checked by the compiler anyway.

This is also a good approach when the units being used don't have a direct, or easy to express, relation.
*)

    let ToGravity (gravityPoints:float<gp>) =
        ((float gravityPoints / 1000.0) + 1.0) * 1.0<sg>

(**
It really is down to personal preference which direction you take here. However I generally take whichever approach results in the most readable code.
*) 

(**

##Converting to and from units of measure

Converting between units of measure couldn't be simpler and it gives us a beautiful way of expressing common unit conversions for our brewing calculations.  
For instance we can declare two constants that represent the conversion factors between  Litres/Gallons and Pound/Kg.  
This then allows us to use 2 simple functions to convert between the respective `UoM`.

    //Constants
    let litresPerUsGallon = 3.78541<L/usGal>
    let poundPerKg = 2.20462<lb/kg>

    //Conversion Funcs
    let ToPound (kg:float<kg>) = poundPerKg * kg
    let ToKilograms (lb:float<lb>) = lb / poundPerKg
    let ToLitres (litres:float<usGal>) = litres * litresPerUsGallon
    let ToUsGallons (gallons:float<L>) = gallons / litresPerUsGallon

##Generics and Units of Measure and common pit falls



#Important points

1. Units of measure do not exist at runtime and cannot be used by non F# sharp assemblies.

2. You can interchange UoM as long as they are equivalent. For example we could pass a ppg, (which is lb/usGal) into the a function expecting a lb / usGal.

*)

(**

[FSharp Units of Measure]: https://msdn.microsoft.com/en-us/library/dd233243.aspx
[MSDN]: https://msdn.microsoft.com/en-us/library/dd233243.aspx
*)