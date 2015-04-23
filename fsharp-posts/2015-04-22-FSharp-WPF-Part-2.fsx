
(*** hide ***)
module FsWPF

#r """..\packages\FsXaml.Wpf\lib\net45\FsXaml.Wpf.dll"""
#r """..\packages\FsXaml.Wpf\lib\net45\FsXaml.Wpf.TypeProvider.dll"""
#r """..\packages\FSharp.ViewModule.Core\lib\net45\FSharp.ViewModule.Core.Wpf.dll"""
#r """..\packages\BrewCalc\BrewCalculations.dll"""
#r "PresentationFramework.dll"
#r "PresentationCore.dll"
#r "System.Xaml.dll"
#r "WindowsBase.dll"

open System.Windows
open System.Windows.Controls

(**
#F# and WPF part two - functional models, decoupled views.

This post is the second in my F# and WPF series.  
It expands upon the basic use of F# for application development with WPF, using the XAML type provider from the previous post in the series.  

The main focus for this stage of the development of my BrewLab application was to create a clear separation between the functional representation of the domain (The model), and the more imperative, OO style used for the ViewModels.  
The aim was to have a complete, robust domain model, implemented using F# modules, records and functions. These would then be utilised by the corresponding ViewModels, separating the concerns of the domain with the visual representation of it.  

All in all, A pretty stereotypical MVVM implementation but with the added benefits of the F# language. Specifically, Immutability by default (no unforeseen side effects!), 
functions for business logic (clear and concise, making the domain simple to model) and unique features like Units of measure, pattern matching and active patterns which all help to reduce complexity and clarify the solution.

##Refactoring the Model from the ViewModels

The first step was to make a clearer separation of the model and the view via the use of the existing ViewModels.  
This called for disallowing the use of the record types directly in collections etc that would end up being bound to the view.  

For example, the Grain collection in the Recipe ViewModel was previously declared as an observable of my grain record type.  
*)

(*** hide ***)
open Units

type grain = {Name:string; Weight:float<g>; Potential:float<pgp>; Colour:float<EBC> }
(**
*)

open System.Collections.ObjectModel

type RecipeViewModel() as this = 
    inherit ViewModelBase()

    let grain = ObservableCollection<grain>()
    member x.Grain with get() = grain


(**
This needed to change in order to decouple the model from the view and allow for us to introduce a layer of mutability.  
The benefits of this are two fold, the View no longer cares what the model looks like, only what the view model(s) exposes to it. This allows for the model to be changed freely, as long as we keep the View Model consistent. 
Of course, if we don't, we are invalidating our ViewModel not the View, this would be picked up in testing (Via unit testing the VMs).

Secondly it allows us to model the entire domain, business logic and all, entirely separate of any UI elements.  
This is the biggest benefit as it means we can utilise the power of F# as a domain modelling tool while ensuring that our representation is as robust as possible.  

At the end of the day it should be completely possible to test the model (The domain and the logic related to it) and the ViewModels (The User driven *manipulation* of the model. That is, the user actions that cause a reaction within the domain.) 

The first step then, was to extend the GrainViewModel introduced at the end of the last post, wrapping a *snapshot* of our Grain model, and exposing mutable properties that can later be used to re-build/update the model.

This required the introduction of a new base class.

*)

open Units
open FSharp.ViewModule
open FSharp.ViewModule.Validation
open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open System

type GrainViewModel(addition) as this = 
    inherit LabViewModel<GrainAddition<kg>>(addition)

    let weight = this.Factory.Backing(<@ this.Weight @>, 0.001<kg>, greaterThan (fun a -> 0.000<kg>))
    let name = this.Factory.Backing(<@ this.Name @>, addition.Grain.Name)
    let potential = this.Factory.Backing(<@ this.Potential @>, addition.Grain.Potential)
    let colour = this.Factory.Backing(<@ this.Colour @>, addition.Grain.Colour)

    let switchGrainCommand = 
        this.Factory.CommandSyncParam(fun (param:obj) ->
            match param with 
            | :? Tuple<obj, bool> as t->
                let grain = t.Item1 :?> grain<kg> //Need to stop this being a tuple
                this.Name <- grain.Name
                this.Potential <- grain.Potential
                this.Colour <- grain.Colour
            | _ -> ignore())

    override x.UpdateModel(model) = 
        { model with Weight = weight.Value }

    member x.Name with get() = name.Value and private set(v) = name.Value <- v
    member x.Potential with get() = potential.Value and private set(v) = potential.Value <- v
    member x.Colour with get() = colour.Value and private set(v) = colour.Value <- v
    
    member x.Weight 
        with get () = weight.Value
        and set (value) = weight.Value <- value

    member x.SwitchGrainCommand = switchGrainCommand

(**
*)

(*** hide ***)
---
layout: post
title: F# and WPF part two - functional models, decoupled views.
date: 23/04/2015
comments: true
tags: ["fsharp","WPF"]
catagories: ["guides","examples"]
series: F# and WPF
series-post-number: 2
meta: Exploring the use of FSharp and WPF to create applications
--- 