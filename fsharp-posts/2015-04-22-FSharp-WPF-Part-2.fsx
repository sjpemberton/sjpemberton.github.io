
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

The main focus for this stage of the development of my BrewLab application was to create a clear separation between the functional representation of the domain (The models and their related business logic), and the more imperative, OO style used for the ViewModels.  
The aim was to have a complete, robust domain model, implemented using F# modules, records and functions. These would then be utilised by the corresponding ViewModels, separating the concerns of the domain with the visual representation of it.  

All in all, A pretty stereotypical MVVM implementation but with the added benefits of the F# language. Specifically, Immutability by default (no unforeseen side effects!), 
functions for business logic (clear and concise, making the domain simple to model) and unique features like Units of measure, pattern matching and active patterns which all help to reduce complexity and clarify the solution.

##Extending the View Models

The first step was to make a clearer separation of the model/domain and the view via the use of the existing ViewModels. 
At the end of the last post, I had already refactored the direct use of the Grain record by the view model by creating a `GrainViewModel`. To recap, this is shown below:
*)

(*** hide ***)
open Units

type grain = {Name:string; Weight:float<g>; Potential:float<pgp>; Colour:float<EBC> }
(**
*)

type RecipeViewModel() as this = 
    inherit ViewModelBase()
    let grain = ObservableCollection<GrainViewModel>()
    let addMaltCommand = 
        this.Factory.CommandSync
            (fun param -> 
            grain.Add(GrainViewModel(name = "Maris Otter", Weight = 0.1<g>, potential = 37.0<pgp>, colour = 4.0<EBC>)))

    member x.AddMaltCommand = addMaltCommand
    member x.Grain = grain


(**
This Change allowed us to decouple the model from the view and allow for us to introduce a layer of mutability.  
  
The benefits of this are two fold, the View no longer cares what the model looks like, only what the view model(s) exposes to it. This allows for the model to be changed freely, as long as we keep the View Model consistent. 
Of course, if we don't, we are invalidating our ViewModel not the View, this would be picked up in testing (Via unit testing the VMs).

Secondly it allows us to model the entire domain, business logic and all, entirely separate of any UI elements.  
This is the biggest benefit as it means we can utilise the power of F# as a domain modelling tool while ensuring that our representation is as robust as possible.  

At the end of the day it should be completely possible to test the model (The domain and the logic related to it) and the ViewModels (The User driven *manipulation* of the model. That is, the user actions that cause a reaction within the domain.) 

The first step then, was to extend the GrainViewModel to wrap a *snapshot* of our Grain model and expose mutable properties that can later be used to re-build/update the model.

At this point it is worth stating that the purpose of the View Models is as follows:
- To wrap a snapshot of a model. This can then be ustilised to provide functionality such as dirty checking or undo etc.
- To hold the state of the application. The VMs will hold all current state which will only be propagated to the persistent storage at a save point. 
- To interact with the business logic (domain) in order to handle updates to the model.

This means the VMs *utilise* the domain modules to handle any business logic. In other words the View models are just a pass through to the actual doamin, which is all modelled in F# goodness. 


First up, I introduced a new base class for my view models.  
I must admit, I didn't particualy like this approach, but I did not want to duplicate code for updating the wrapped snapshot of the model in my VMs. I also couldn't parameterise the update logic, 
as this logic is specific to the view models themselves. (This is the case because the View Models know which parts of the UI require updating in specific scenarios, reducing the load on the UI)

This new base class is as follows:

*)

[<AbstractClass>]
type LabViewModel<'t>(model:'t) as this = 
    inherit ViewModelBase()

    ///Mutable cache of the current model snapshot - used for dirty checking etc
    let mutable model = model

    abstract member UpdateModel: 't ->'t

    member x.GetModel() = 
        let newModel = this.UpdateModel model
        model <- newModel
        newModel

(**
I then derived my view models from this class.
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

(**
We need to handle the link between various VMs and the fact that the view needs to be updated when the various parts of the model change.

To do this, we can go a few different directions.

1. Use a simple eventing system and fire events when a model part is changed. For example a GrainChange event will cause the recipe VM to refresh it's snapshot of the recipe (by pulling from the current state of the other VMs).  
This has the benefit of being quick to implement, but it also directly ties the VMs together.

2. Use a message based approach where the various VMs can subscribe to a service and update themselves as required. 


The simple event based approach is as easy as adding an additional property and member to our grain model and then triggering the event when we update any fields.

*)

type GrainViewModel(addition) as this = 
    inherit LabViewModel<GrainAddition<kg>>(addition)

    let eventStream = Event<ViewModelEvent>()
    let weight = this.Factory.Backing(<@ this.Weight @>, 0.001<kg>, greaterThan (fun a -> 0.000<kg>))
  
    member this.EventStream = eventStream.Publish :> IObservable<ViewModelEvent>
    member x.Weight 
        with get () = weight.Value
        and set (value) = 
            weight.Value <- value
            eventStream.Trigger ViewModelEvent.Update

(**
This can then be subscribed to like so from the recipe VM.
*)

 let addMaltCommand = 
    this.Factory.CommandSync(fun param ->
        let gvm = GrainViewModel({Grain = this.Grains.[0]; Weight = 0.0<kg>})
        gvm.EventStream.Subscribe handleRefresh |> ignore
        this.Grain.Add(gvm) //Default to first in list - Could be empty instead?
        this.RefreshParts)

(**
Alternatively, we can use a more decoupled approach.  
To achieve this we can use a simple publish/subscriber based module. This allows multiple publishes and subscribers to easily react to changes within our view models.  

Even though my design is currently simple, I opted for the latter approach as it will give me a few benefits. 
1. The main Recipe VM can subscribe once to the aggregator service. (As opposed to multiple VMs)
2. Completely distinct parts of the app can force the Recipe View to be updated. These parts range from the obvious ingredient VMs to the not so obvious Equipment setup and settings sections.

Of course, this could be implemented using agents. however, I see no need to introduce this level of complexity (I know, they're not really complex) as the App doesn't  really fit the use case.  
It is a simple, synchronous change based application and therefore does not need the benefits provided by Agents. These better suite, parallel, asynchronous, multi threaded/concurrent applications.
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