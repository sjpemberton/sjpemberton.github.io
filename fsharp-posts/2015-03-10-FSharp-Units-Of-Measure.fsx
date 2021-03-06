

(*** hide ***)
module BrewCalculations 

open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open LanguagePrimitives

[<Measure>] type sg
[<Measure>] type lb
[<Measure>] type percentage


(**

#F# Units of Measure

Have you ever wished that you could have type safe calculations throughout your application?  
Well through the use of F# Units of Measure (UoM), now you can!

In this post I will explore the various ways of using F# Units of Measure and the benefits they bring.  
Before I started writing this article, I had not used units of measure before. I therefore thought it would be a good idea to aid the learning process by applying them in a small project.
To do this, I decided to put UoM to the test by applying them in a real world example; The calculations required to brew beer.  

What follows is a thorough walkthrough of using Units of Measure based upon my experiences while implementing a library of calculations for use in the various stages of brewing beer.  
I aim to highlight how, through the use of units of measure, we can increase the robustness of our code and hopefully eliminate potential runtime errors.

<!-- more -->

##Units of Measure - An introduction

Units of measure in F# are a type of metadata that can be associated with floating point or signed integer values.  
By associating a UoM with a quantity value it allows the F# compiler to perform additional type checking on the use of these units, enforcing relationships between units in arithmetic and reducing potential errors.

To declare a unit of measure you use the `[<Measure>]` attribute, followed by the `type` keyword and the name we want to give the measure.

For example, we can declare units of measure for some of the measures of volume we will need when calculating the ingredients in beer recipes.

*)

///Litre (or Liter in the US)
[<Measure>] type L

///Us Gallon
[<Measure>] type usGal

(**
Using a measure is as simple as annotating a float literal.
*)
//Volume in litres
let volume = 120.0<L>

(**
A value that has a unit of measure associated with it is said to have a *dimension* or be *dimensioned*.  

Units of measure can be utilised in a number of ways.
 
- To constrain the values involved in calculations to particular measures
- To provide type safe conversions between measures
- For defining new units of measure in terms of the original
- In types to create associations between different measures

This is in no way a definitive list. They are just a few of the ways I found measures particularly beneficial during my time exploring there usage.  
If anyone has any other useful applications I would love to hear about them.

##Defining units of measure in terms of others

Sometimes, it can be a useful technique to define a unit of measure in terms of other previously defined measures.  
Doing so allows us to use the *derived* measures in place of inferred results of calculations which can increase code clarity.

Let's take an example from the brewing process.  

We often need to associate something called gravity points, with a volume of liquid.  
Gravity points are a very simplified definition of the amount of sugar in liquid. Obviously, this liquid could be in any number of units, and it is paramount that we ensure we do not mix measures of volume, for example, during recipe planning.

One common measurement used in home brewing circles is that of points per gallon (or points per pound per gallon - PPG) so let's define a measure for that.  
Firstly, we need to define a measure for gravity points.

*)
///Gravity Point - A Simplified brewing unit for amount of sugar dissolved in solution
[<Measure>] type gp

(**
Next up, we define the *association* between gravity points and US gallons.
*)

[<Measure>] type ppg = gp / usGal

(**
Our `ppg` measure can now be used in our calculations, which we'll get to in a minute.  
But first, a few quick points on this type of measure.

- The formulas that represent the measures can be written in various equivalent ways. This sometimes manifests in the results of expressions being inferred differently than we would expect - more on this later.
- Equivalent formulas are compiled into a common representation and can therefore be substituted freely.
- You cannot use numeric values in these formulae. However, we can declare conversion constants which we will also explore later.
- You can use `1` in these formulae. `1` represents a *dimensionless* value. I will touch on dimensionless values when discussing error prevention in the next section.

These points and others are explained in detail on the [MSDN] page for Units of Measure.

##Using Units of Measure for error prevention

Units of measure come in extremely handy for preventing us introducing errors into our code by using a value with an incorrect unit in a calculation or function.  
As an example taken from the world of brewing, we wouldn't want to mix up the units when making calculations about how much grain we need.

Below is an example of a function that can only take values with the specified dimensions.

*)

///Converts a points per gal (gp / usGal) and volume into total gravity points in that volume
let TotalGravityPoints (potential:float<gp / usGal>) (vol : float<usGal>) =  
    potential * vol

(**

As you can see, this function is declared with explicit type annotations specifying the dimensions of the parameters.  
The F# compiler will now prevent you from using this function with either dimensionless floats, or floats with the the wrong dimension (and of course, non float values).

Consider the following example where we attempt to call the function with dimensionless values:

*)

let totalGravPoints = TotalGravityPoints 240.0 5.0

(**
Attempting to compile this line of code produces the following error notifying us that we haven't satisfied the type constraints and preventing us from introducing an error into our code.

    [lang=output]
    error FS0001: This expression was expected to have type
    float<gp/usGal>    
        but here has type
    float 

Likewise the compiler will stop us from passing different `UoM` to the function. 
Suppose we attempted to use a volume in Litres instead of the expected US Gallons.  

We receive a similar error.

*)
let totalGravPoints = TotalGravityPoints 240.0<gp / usGal> 5.0<L>
(**
    [lang=output]
    error FS0001: Type mismatch. Expecting a
        float<usGal>    
    but given a
        float<L>    
    The unit of measure 'usGal' does not match the unit of measure 'L'

This example may be quite contrived, but it highlights the type safety provided by units of measure.
The F# compiler will also prevent us introducing arithmetic errors such as attempting to add/subtract a different or dimensionless unit from another.  
For instance, the following would not compile, returning the errors shown.

*)

let volume = 5.0<usGal> + 5.0<L>
(**
    [lang=output]
    //result
    error FS0001: The unit of measure 'L' does not match the unit of measure 'usGal'

Likewise attempting to use a dimensionless value would also fail.  
*)

let volume = 5.0<usGal> + 5.0
(**
    [lang=output]
    //result
    error FS0001: The type 'float' does not match the type 'float<usGal>'

A dimensionless value can either be declared simply with no measure, as above, or with the explicit measure of `1` like so:  
*)
let volume = 5.0<usGal> + 5.0<1>
(**
    [lang=output]
    //result
    error FS0001: The type 'float' does not match the type 'float<usGal>'

Although we cannot add/subtract different or dimensionless values from an already dimensioned value, we can multiply or divide them.  
Multiplying or dividing by a dimensionless value will result in same measure, however using a different measure in the calculation will result in a different (potentially new) measure.  

This brings us nicely to our next section. 

##Effects of multiplication and division

By multiplying or dividing a value that either has a measure already, or is dimensionless, we can create new units of measure.  
The result of this process is effectively to *combine* two different units of measure (remember, a dimensionless value can be thought of as having a measure of 1).  

We have already declared a unit of measure that can be used to demonstrate this, our `ppg` measure.  
A `ppg` value is simply a `gp` value divided by a `usGal` value.
*)

let totalGravityPoints = 240.0<gp>
let beerVolume = 5.0<usGal>
let pointsPerGallon = totalGravityPoints / beerVolume

(**

The value of pointsPerGallon above is just what we would expect.

    [lang=output]
    val pointsPerGallon : float<gp/usGal> = 48.0

The exact same principle works for multiplication too and don't forget, two or more units of measure can be considered equal.  

##Type inference and measure equality

Lets take the following function as an example;
*)

///Calculates the maximum potential gravity points for a given weight of grain with the given potential and target volume
let MaxPotentialPoints (grainPotential:float<gp/lb>) (grain:float<lb>) (vol:float<usGal>) = 
    (grainPotential * grain) / vol

(**
The F# compiler correctly infers that the result of this function is of the type `float<gp/usGal>` (hover over the function above to see this)
 
We also know that equivalent measures are interchangeable.  
This means, we could alternatively declare this function as returning a `<ppg>` measure explicitly like so.

*)
//Explicit return type
let MaxPotentialPoints (grainPotential:float<gp/lb>) (grain:float<lb>) (vol:float<usGal>) :float<ppg> = 
    (grainPotential * grain) / vol

(**
The F# type system will allow us to use either of these functions where the alternative dimension is required (i.e. a `ppg` where a `gp/usGal` is expected).
I did find however, that in certain situations, it can make code much clearer to be explicit about the return type.

Consider the following example where we use a `pgp` measure instead of the `gp/lb` for the grainPotential:

*)
///Potential Gravity Points - The number of Gravity points in a lb of a particular malt
[<Measure>] type pgp = gp / lb

let MaxPotentialPoints (grainPotential:float<pgp>) (grain:float<lb>) (vol:float<usGal>) = 
    (grainPotential * grain) / vol

(**

If you look at the inferred return type of this function you will see it is `float<lb pgp/usGal>`.  
While this is perfectly correct, it can be confusing. 
 
We can clearly see that the `pgp` measure is equivalent to that of `gp /lb` so why has the inferred return type changed from the first example?  

I believe the reasoning is that, although the compiler creates a common representation of the equivalent measures for use at runtime, it doesn't expose this to us through type inference (a possible gotcha with type inference and units of measure perhaps?).  
It is pretty clear to see that all three of the above examples are correct and equivalent. For example `lb pgp/usGal` becomes `(lb * (gp/lb))/usGal`, reducing to  `gp / usGal`.  

We can of course prove this equality in code.
*)
let isEqual = 200.0<lb pgp/usGal> = 200.0<ppg>
(**
    [lang=output]
    //result
    val isEqual : bool = true
*)
let isEqual = 200.0<lb pgp/usGal> = 200.0<gp/usGal>
(**
    [lang=output]
    //result
    val isEqual : bool = true


That covers returning specific measures, but what about when we need to *change* the measure associated with a value?

##Conversion between measures

Sometimes, we want to be able to quickly convert between two different units of measure.  
As I mentioned previously, we cannot simply declare a new measure to represent a conversion factor, but we can declare this as a constant value in terms of the measures in question.  

Consider an example of converting a volume of beer from US Gallons to Litres (Useful when converting from American recipes).  
To do this we can declare the conversion factor as follows.

*)
let litresPerUsGallon = 3.78541<L/usGal>

(**
The conversion factor is given a combined measure of `L/UsGal` (or Litres per gallon). This specifies that `1` Us Gallon is `3.78541` Litres.  
This conversion factor can then be utilised in functions or expressions where needed.
*)

let volume = 5.0<usGal>
let volumeInLitres = volume * litresPerUsGallon 

let ToUsGallons (litres:float<L>) = litres / litresPerUsGallon
let gallons = ToUsGallons 20.0<L>
(**

The resulting values of the examples above show how the use of the conversion constant allows for completely type safe unit conversions. Pretty cool huh?  

    [lang=output]
    val volumeInLitres : float<L> = 18.92705
    val gallons : float<usGal> = 5.283443537


##Converting to and from units of measure
 
At some point when using units of measure, you will undoubtedly need to convert from dimensionless values to ones with measures and vice versa.  
Say we have a function that works on dimensionless values, but we want it to return a specific measure; We have a couple of options available to us in order to accomplish this task.  

- We can multiply the resulting unit by 1, where 1 is given the dimension type we want as the result.  
*)
let TotalGravityPoints potential vol =  
    (potential * vol) * 1.0<gp / usGal> 

(**           
- We can explicitly declare the return type of the function and then use one of the helper functions `LanguagePrimitives.FloatWithMeasure` or `LanguagePrimitives.IntWithMeasure`.
*)

let TotalGravityPoints potential vol : float<gp / usGal> =  
    LanguagePrimitives.FloatWithMeasure (potential * vol)

(**
In order to remove a dimension from a value, we either need to cast it to the value type, or calculate out the units you don't want. 
*)
let cast = float 5.0<L>
(**
    [lang=output]
    //result
    val cast : float = 5.0
*)
let multiplyOut = 5.0<L> / 1.0<L>
(**
    [lang=output]
    //result
    val multiplyOut : float = 5.0

The second example above clearly has the advantage of being type safe. 
Whether you need type safety or not in a situation like this is therefore a key factor in deciding which approach to take.

##Which approach to choose?

We have already seen how you can use conversion constants in order to convert between units of measure.  
However, we can also leverage the previously mentioned, much simpler techniques in order to achieve the same goal with the *potential* for easier to comprehend code.
Personally, I have been mixing both techniques, but in certain situations one approach can be more favourable than the other.  

###The fully type safe approach

For the most part I have tried to utilise type safety throughout code that requires units of measure.  
Generally speaking this is completely painless and the fantastic type inference provided by the F# type system eliminates most of the bloat from our code.

Therefore we can create functions that are fully type safe, including the calculations within them, while maintaining readability.  
*)
///Yeast attenuation - The difference in specific gravity between original gravity and final gravity, as a percentage
let YeastAttenuation (originalGravity:float<sg>) (finalGravity:float<sg>) =
    ((originalGravity - finalGravity) / (originalGravity - 1.0<sg>)) * 100.0<percentage>

(**
###The non fully type safe approach

If we don't care for type safety **during** a calculation, then we could choose the casting approach to remove dimensions due to the boost in readability it can provide (This can be subjective).
Usually, this is when I have a complex calculation contained in a function and I only truly need to restrict the inputs to it.  

By opting for casting to remove the dimensions and the multiply by one trick in order to return the result as the unit of measure we want, it *can* make code clearer than the alternative.  
The inner code would not be type safe, but we know that the passed in values will be checked by the compiler anyway. This can be *good enough* in some cases.

This is also a good approach when the units being used don't have a direct or easy to express relation, or you don't want to declare a complex unit of measure.  
For example, take the following function for converting gravity points to specific gravity.
*)

let ToGravity (gravityPoints:float<gp>) =
    ((float gravityPoints / 1000.0) + 1.0) * 1.0<sg>

(**
Of course we could apply both techniques in the same situation.  
For example we could cast some values to float to reduce complexity, while retaining some level of type safety through the other values involved.  
*)

///The estimated gravity of wort created from an amount of grain in lb with the given ppg, at a particular efficiency and for a target volume
let EstimateGravity  (vol:float<usGal>) (grain:float<lb>) (grainPotential:float<pgp>) (efficiency:float<percentage>) =
    ((grainPotential * grain * (float efficiency / 100.0)) / vol) * 1.0<usGal>
    |> ToGravity

(**
It really is ultimately down to personal preference which direction you take.

##Generics and Units of Measure

Sometimes we want to create functions or types that can be used with more than one unit of measure. 
Enter generics. 

###Generic Measures in Functions

 
We can use generics with units of measure in much the same way as we would with normal types.  
We do however need to explicitly declare the use of the generic units. This can be done by using either an underscore `<_>` or the usual letters. (Using letters allows us to enforce equality constraints between multiple generic parameters, while underscores are effectively a wildcard)

*)
let AddVolumes (vol1:float<_>) (vol2:float<_>) = vol1 + vol2
//alternatively
let AddVolumes (vol1:float<'u>) (vol2:float<'u>) = vol1 + vol2

(**
It is worth noting that this function can be executed with any unit type including a dimensionless value.  
It can therefore be used with plain floats, however type safety is applied in the sense that the two values must have the same dimension.

In simple cases like this, the use of generics gives us everything we need. This is not always the case though...  

Let's look at a more complex example.  
Below is a function that calculates the amount of grain in weight required to brew an amount of beer that has a specific amount of gravity points.
It is easy to see that such a function could be used to determine the weight of grain in various units.
*)
///Calculates required Grain in weight from the target gravity points and effective malt potential (in relation to a fixed weight).
let GrainRequired (gravityPoints:float<gp>) (effectivePotential:float<gp/'u>) =
    gravityPoints / effectivePotential

(**
The use of generics here associates a fixed unit (the gravity points) with any unit of weight.  
Unfortunately however, the effectivePotential inferred to have the unit `<'u>`, losing the link between gravity points and weight unit.

We can make this better.   
By adding an explicit generic attribute to the function itself, we can further aid the type system in applying our constraint.
*)

let GrainRequired<[<Measure>]'u> (gravityPoints:float<gp>) (effectivePotential:float<gp/'u>) =
    gravityPoints / effectivePotential

(**
This looks better, and we can see that by being explicit about the type we expect in return we can restrict the input as required.
*)

let weight = GrainRequired<lb> 180.0<gp> 36.0

(**
    [lang=output]
    //result
    error FS0001: This expression was expected to have type
        float<gp/lb>    
    but here has type
        float

However, that is not the end of the story as there is nothing to stop us **not** being explicit about the type we want.  
We can then pass any unit we like to the function and get a potentially unwanted unit value (or a dimensionless value) back.  
Luckily though, it is highly likely that the result of this function will be used somewhere that is expecting a particular unit, reducing the chance of errors.  
This approach isn't bullet proof however and it helps to be aware of the pit falls.

If anyone knows of a better approach, please feel free to get in touch.

###Generic Measure in Types

One possible use for using generic measures in types is to enforce constraints on relationships between different values.

Lets say that we want to keep track of the various malt we are using in our beer recipe.  
We could create a simple record type that associates the weight of the grain being used, along with it's potential.  
We will more than likely want to use different units of measure for the weight and gravity point / weight potential values. Therefore making this type generic is a good fit.
*)
type Malt<[<Measure>] 'u> = 
    {Weight:float<'u>; Potential:float<gp/'u>;}
(**

By declaring the type as such you make a binding between the two values based on the unit of measure used for the weight.

*)
let maltInPound = { Weight=6.2<lb>; Potential=36.0<gp/lb>; }

(**
    [lang=output]
    //result
    val maltInPound : Malt<lb> = {Weight = 6.2;
                                    Potential = 36.0;}

It is also not possible to mix the dimensions for the two values.
*)
let maltInPound = { Weight=6.2<lb>; Potential=36.0<gp/kg>; }

(**
    [lang=output]
    //result
    error FS0001: Type mismatch. Expecting a
        float<gp/lb>    
    but given a
        float<gp/kg>    
    The unit of measure 'gp/lb' does not match the unit of measure 'gp/kg'

###Multiple Generic units of Measure

You can also create types with more than one generic unit of measure.  
I find this particularly useful for representing relations between distinct units of measure. In the world of brewing this can be useful when ensuring weights and volumes line up.  

*)
//Over simplified Recipe
type Recipe<[<Measure>] 'u, [<Measure>] 'v> =
    {GrainBill:list<Malt<'u>>; Volume:float<'v>}

let AmericanAle = {GrainBill = [{Weight=6.2<lb>; Potential=36.0<gp/lb>}; {Weight=0.5<lb>; Potential=40.0<gp/lb>}];
                    Volume = 5.0<usGal>}
(**

As can be seen above, we can utilise such a type to keep the association between weight in `lb` and volume in `usGal` together.  
We can then use the values from this type (or the type itself) in our brewing functions and the F# compiler will make sure everything is type safe, preventing us from mixing up the units.

##Units of Measure at runtime and interoperability

One key point to take heed of, is that units of measure do not exist at runtime.  
They are intended for static type checking only and therefore prevent us implementing functionality based on checking units.  

They are also not part of the wider .NET type system and can therefore not be consumed, or used, by any non F# assembly.  
If exposing units of measure to consumers, the units are simply not visible and all values will be treated as there underlying, dimensionless type.

##Conclusion

I hope you agree that F# Units of Measure are an extremely useful tool for ensuring type safety when implementing calculations or functions where the units matter.  
For such a simple concept, they can be utilised in numerous ways and can have quite a dramatic impact on error prevention. I'm sure that I've only scratched the surface of their usefulness, but even so, the benefits of using them are immediately apparent.

###Key Benefits

- Prevent mismatches of units at compile time.
- Help to eliminate runtime errors that could be incredibly subtle and hard to track down.
- Can help to improve code readability (This can of course swing the other way if not careful).


###Examples from this post

If anyone is interested the library of brewing calculations I created during this process can be found on [GitHub]. At the time of writing it is still a work in progress.  
Any and all comments are welcome.
*)

(**

[GitHub]: https://github.com/sjpemberton/BrewCalc
[FSharp Units of Measure]: https://msdn.microsoft.com/en-us/library/dd233243.aspx
[MSDN]: https://msdn.microsoft.com/en-us/library/dd233243.aspx
*)

(*** hide ***)
---
layout: post
title: F# Units of Measure - A Worked Example
date: 11/03/2015
comments: true
tags: ["fsharp"]
catagories: ["guides","examples"]
meta: Utilising FSharp Units of Measure for type safe calculations
--- 