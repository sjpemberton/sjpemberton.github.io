
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

open System
open System.Windows
open System.Windows.Controls
open FSharp.ViewModule
open FSharp.ViewModule.Validation
open System.Collections.ObjectModel
open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open Calculations
open Units


type GrainViewModel(name:string, potential:float<pgp>, colour:float<EBC>) as this = 
    inherit ViewModelBase()

    //Create a notifying property from the Backing store factory
    let weight = this.Factory.Backing(<@ this.Weight @>, 0.1<g>, greaterThan (fun a -> 0.0<g>))
    
    member val Name = name
    member x.Weight with get() = weight.Value and set(value) = weight.Value <- value
    member x.Potential:float<pgp> = potential
    member x.Colour:float<EBC> = colour


(**
#Examples and thoughts on WPF development with F#

This post is the second in my F# and WPF series.  
It expands upon the basic use of F# for application development with WPF, using the [XAML type provider] from [the previous post in the series], by focusing on the overall design and how the use of F# effects decisions made during this process.  

The main focus during this stage of development for my BrewLab application was to create a clear separation between the functional representation of the domain (The models and their related business logic), and the more imperative, OO style used for the View Models (VMs).   

The aim was to build a complete, robust domain model, using F# modules, records and functions. that would, in turn, be utilised by the corresponding View Models. Separating the concerns of the domain with the visual representation of it.  

All in all, a pretty stereotypical MVVM implementation but with the added benefits of the F# language. Specifically, Immutability by default (no unforeseen side effects!), 
functions for business logic (clear and concise, making the domain simple to model) and unique features like [units of measure], pattern matching and active patterns which all help to reduce complexity and clarify the solution.

<!-- more -->

When developing any application I like to take a domain driven approach (although, I'm rarely that strict on my own personal process!).  
Therefore, I made the logical choice of implementing the part of the domain I wished to model first, before deciding how the views (and therefore the VMs) would utilise it.


##The Domain Model

The Domain for the application can be completely modelled in a functional manner. Thus ensuring we gain all the benefits from the F# language.  
This Allows us to create a few, simple, immutable types to represent our data and then implement all the required domain logic via native functions.

As an example, below is my representation of the key ingredients required when creating a beer recipe.
As you can see, I have kept the base record types generic, but have, in addition, supplied a Discriminated Union constraining the ingredients to Metric values for now (The default for the application).

Also shown below are two of the functions associated with the ingredients, `CalculateIBUs` and `CalculateGravity`. I have included these here as they are closely related to the `HopAddition` and `GrainAddition` record types.

These two functions are a good example of how simple our domain logic becomes.  
The idea is for these simple functions to be called by the view models. This will help during testing as we should be able to fully test/validate the model first, and then verify that our view models satisfy the user based interaction with it.

*Note: Both of these functions require inputs from the Recipe itself and would therefore should (and do) exist in a `Recipe` module, rather than the Ingredients module.*
*)

module Ingredients =

    type HopType =
    |Pellet
    |Leaf
    |Extract

    type hop<[<Measure>] 'w> = {Name:string; Alpha:float<percentage>;}
    type grain<[<Measure>] 'w> = {Name:string; Potential:float<gp/'w>; Colour:float<EBC>;}

    type HopAddition<[<Measure>] 'w>  = { Hop:hop<'w>; Weight:float<'w>; Time:float<minute>; Type:HopType}
    type GrainAddition<[<Measure>] 'w>  = {Grain: grain<'w>; Weight:float<'w>}

    type yeast<[<Measure>] 't> = {Name:string; Attenuation:float<percentage>; TempRange: float<'t>*float<'t> }

    type Ingredient =
    | Hop of HopAddition<g>
    | Grain of GrainAddition<kg>
    | Yeast of yeast<degC>


    //Recipe functions
    let CalculateIBUs hopAddition sg vol =
        let utilisation = EstimateHopUtilisation sg (float hopAddition.Time)
        EstimateIBUs utilisation hopAddition.Hop.Alpha hopAddition.Weight vol

    let CalculateGravity volume efficiency grain =
        grain 
        |> List.fold (fun acc g -> acc + EstimateGravityPoints g.Weight g.Grain.Potential volume efficiency) 0.0<gp>
        |> ToGravity


(**

Currently the app only handles changes to gravity and bitterness when adding/removing grain or hops. 
As the domain is currently incredibly simple, I won't go into any more detail of the model in this post in order to cover the other aspects of WPF development.  

The models and domain definition shall appear high up the dependency chain, insuring they can be used by the view models defined afterwards.  
Although a topic that can polarise peoples opinions, I find that the 'Dependency Order' enforced by the F# compiler is a massive benefit in both this case, and WPF development as a whole (as is the case in most other areas).  
More on this later...

##Extending the View Models

After I had produced the stripped down domain model to support the initial planned functionality, (Which can be viewed on GitHub if interested) it was time to focus on the view models.

The first step was to make use of the existing View Models; Expanding them in order to achieve a clearer separation of the model/domain and the view.  
As an example of the aims; At the end of the last post, I had already re-factored the direct use of the `Grain` record within the `RecipeViewModel`, by creating, and using, a `GrainViewModel` instead.  
To recap, this is shown below:
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
  
The benefits of this are two fold, the view no longer cares what the model looks like, only what the view model(s) exposes to it.  
This allows for the model to be changed freely, as long as we keep the view model consistent. 
Of course, if we don't, we are invalidating our ViewModel not the View, this would be picked up in testing (Via unit testing the VMs).

Secondly it allows us to model the entire domain, business logic and all, entirely separate of any UI elements.  
This is the biggest benefit as it means we can utilise the power of F# as a domain modelling tool while ensuring that our representation is as robust as possible.  

All being well it should be possible to test the model (The domain and the logic related to it) and the view models in isolation of any UI concerns. (The VMs control the user driven *interaction* with the model. That is, they handle user actions that cause a reaction within the domain.) 

###Exposing the model and handling state

The first step then, was to extend the `GrainViewModel` to wrap a *snapshot* of our `Grain` model and expose mutable properties that can later be used to update/re-build the model.

At this point it is worth stating that the intent of the view models is as follows:

- To wrap a snapshot (or two, depending on our design needs) of a model. These can then be used to provide functionality such as dirty checking or undo etc.
- To hold the state of the application. The VMs will hold all current state which will only be propagated to the persistent storage at a save point. 
- To interact with the business logic (domain) in order to handle updates to the model.

This means the VMs *rely on* the modules that represent the domain to handle any business logic.  
In other words the view models are just a user facing pass through to the actual domain, which is all modelled in functional F# goodness. 

So, let's look at how this could be achieved.

###Creating a new VM base class 

First up, I introduced a new base class for my view models, derived from the `ViewModelBase` provided by [FSharp.ViewModule].  
I must admit, I didn't particularly like this approach due to adding an extra layer of inheritance, but I didn't want to duplicate code for updating the wrapped snapshot of the model in my VMs. I also couldn't parameterise the update logic, 
as this logic is specific to the view models themselves.

The new base class is as follows:

*)

[<AbstractClass>]
type LabViewModel<'t>(model:'t) as this = 
    inherit ViewModelBase()

    let mutable model = model

    abstract GetUpdatedModel : unit -> 't
    member x.UpdateModel() = model <- this.GetUpdatedModel()

(**
I then derived subsequent view models from this class.  
This helps avoid code repetition and is, in my opinion, a valid reason to add the extra level of inheritance.
*)

(***hide***)
module gvm1 =

    open Units
    open FSharp.ViewModule
    open FSharp.ViewModule.Validation
    open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
    open System
    open Ingredients

(***)

    type GrainViewModel(addition) as this = 
        inherit LabViewModel<GrainAddition<kg>>(addition)

        let weight = this.Factory.Backing(<@ this.Weight @>, 0.001<kg>, greaterThan (fun a -> 0.000<kg>))
        let grain = this.Factory.Backing(<@ this.Grain @>, addition.Grain)

        override x.GetUpdatedModel() = 
            { Grain = grain.Value; Weight = weight.Value }

        member x.Grain with get() = grain.Value and set(v) = grain.Value <- v
        member x.Weight with get () = weight.Value and set (value) = weight.Value <- value

(**

As you can see, the grain view model is as minimal as possible, simply exposing mutable values from the immutable model, and implementing any abstract members as required.
Following this approach keeps the view model types as dumb as possible and ensures they are merely used to provide access to the domain model in a controlled way.

Adding all these extra view models caused me to need to think about how they interact with one another.
*)

(**

##View Model Communication, Events and Responsibility

Inherent in all WPF applications is the need for the various parts to communicate with each other, usually for the purpose of informing each other of changes to data.
As such, we need to handle the link between various VMs and the fact that the views related to these VMs needs to be updated when the various parts of the model change.  

To achieve this, we could go in a few different directions.

1. Implement a simple event stream per view model, bind to it from the other view models that want to be notified, and fire events when a model part is changed. For example a `GrainChange` event would cause the recipe VM to refresh its snapshot of the recipe (by pulling from the current state of the other VMs). This has the benefit of being quick to implement, but it also directly ties the VMs together and could introduce circular dependencies. No body wants that!
2. Use a message based approach where the various VMs can subscribe to a service, allowing them to send and receive updates as required.
3. A mix of the above, with the addition of direct communication between immediately related VMs where one of them holds the *responsibility* for the update. For example, my `HopAddition` view model could rely on the `Recipe` view model that *owns* it to trigger an update of its `IBU` property when a related value changes, such as recipe volume or estimated gravity.

Let's quickly see how each of these could be implemented.

###Direct Events

The simple event based approach is as easy as adding an additional property and member to our `GrainViwModel` and then triggering the event when we update any fields.

*)

(***hide***)
module gvm2 =

    open Units
    open FSharp.ViewModule
    open FSharp.ViewModule.Validation
    open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
    open System
    open Ingredients

(***)

    type LabEvent = 
    | GrainChanged
    | HopChanged

    type GrainViewModel(addition) as this = 
        inherit LabViewModel<GrainAddition<kg>>(addition)

        let eventStream = Event<LabEvent>()
        let weight = this.Factory.Backing(<@ this.Weight @>, 0.001<kg>, greaterThan (fun a -> 0.000<kg>))
  
        member this.EventStream = eventStream.Publish :> IObservable<LabEvent>
        member x.Weight 
            with get () = weight.Value
            and set (value) = 
                weight.Value <- value
                eventStream.Trigger GrainChanged
(**

This approach has the benefit of clarity of intent, but at the same time requires the subscribing view models to have direct access to the source of the event.

Alternatively, we can use a more decoupled approach.

###A Simple Event Service

To achieve this we can use a simple publish/subscriber based model.  
This allows multiple publishers and subscribers to easily react to changes within our view models, regardless of whether they directly know about them or not.  
We can also utilise the `INotifyProperty` changed events provided by `Fsharp.ViewModule` base class in order to avoid boilerplate code.

We can implement an incredibly simple event service as follows:

*)

module Events =

    open System

    type LabEvent = 
        | GravityChange
        | VolumeChange
        | HopChange
        | FermentableChange
        | EquipmentChange
        | UnitsChanged

    type RecipeEvent private() = 
        let event = Event<LabEvent>()
    
        static let mutable instance = Lazy.Create((fun x -> new RecipeEvent()))
        static member Instance with get() = instance.Value

        member this.Event = event.Publish
        member this.Subscribe o = this.Event.Subscribe o
        member this.Trigger o = event.Trigger o

(**

I chose to make this a singleton so as to avoid the possibility of VMs subscribing/publishing to different event service handlers.

###Option three, the best of both worlds

Even though my design is currently simple, I chose not to go down the route outlined in point 1 above as I do not like the resulting close coupling of the view models.  
Likewise I chose not to fully go with approach 2.  
This approach does however provide us with a couple of benefits. 

1. The main Recipe VM can subscribe once to an event aggregator service in order to receive updates. (As opposed to multiple VMs)
2. Completely distinct parts of the app can cause others to be updated. These parts range from the obvious ingredient VMs to the not so obvious equipment setup and settings sections. 
All of which could have rippling effects on the wider application.

Therefore, for the purposes of this application, I chose to implement option three. A mixture of the first two approaches.
To do so, I rely on the view models that have the responsibility for causing the updates, to propagate them on to the other view models.  
This can happen in two ways.

- Via direct communication (This is only an option if the update source is the sole owner of the target. For example only the Recipe VM should be able to tell the HopAddition VMs that the gravity has changed).
- Via the decoupled event service.

To make the use of the event service as easy as possible for the view models, I added a `BindEvents` member to the base class and also made use of the `INotifyPropertyChanged` event to broadcast any updates.  
This function makes use of first class events by allowing us to manipulate an event stream to suite the VM it is being bound to, for example, to filter the event type listened to, or map it into another form.  
The `BindEvent` method simply binds a callback to *any* event, passing the `IObservable` through a parameterised function providing the custom binding logic. Finally, it adds the disposable event handle to a list on the VM base that is cleared up in its own Dispose method.

Below we can see the new method/backing collection (added to the base class) to support events and their handles, as well as the use of this method to subscribe the VMs own `INotifyPropertyChanged` event to a notifying event that propagates the event to the afore mentioned service.  
Also of note is the new parameter added to the constructor in order to specify what kind of `LabEvent` to map the change events to.

*)

(***hide***)
module lvm2 = 
    open Events
    open System
    open System.ComponentModel

(***)

    [<AbstractClass>]
    type LabViewModel<'t>(model : 't, eType: LabEvent) as this = 
        inherit ViewModelBase()
        let mutable model = model
        let mutable eventHandles = List.empty

        let NotifyChange e =
            this.UpdateModel()
            RecipeEvent.Instance.Trigger(e)

        //Map the prop change notification to a recipe change event and inform everybody
        do this.BindEvent((this :> INotifyPropertyChanged).PropertyChanged |> Observable.map (fun e -> eType), (fun o -> o), NotifyChange)

        interface IDisposable with
            member x.Dispose() = eventHandles |> List.iter (fun d -> d.Dispose())

        abstract GetUpdatedModel : unit -> 't
        member x.UpdateModel() = model <- this.GetUpdatedModel()

        member x.BindEvent(event, subscribe, callback) = 
            eventHandles <- (event 
                             |> subscribe 
                             |> Observable.subscribe callback)
                             :: eventHandles

(**

The ease of binding to the global event service can be seen in the updated `GrainViewModel` below.

*)
(***hide***)
module gvm3 =

    open Units
    open FSharp.ViewModule
    open FSharp.ViewModule.Validation
    open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
    open System
    open Ingredients
    open lvm2

(***)

    let subscribe source = 
            source
            |> Observable.filter (function Events.LabEvent.UnitsChanged -> true | _ -> false)

    let OnLabEvent e =  ()

    type GrainViewModel (addition) as this = 
        inherit LabViewModel<GrainAddition<kg>>(addition, Events.LabEvent.FermentableChange)

        let weight = this.Factory.Backing(<@ this.Weight @>, 0.001<kg>, greaterThan (fun a -> 0.000<kg>))
        let grain = this.Factory.Backing(<@ this.Grain @>, addition.Grain)

        do base.BindEvent(Events.RecipeEvent.Instance.Event, subscribe, OnLabEvent)

        override x.GetUpdatedModel() = 
            { Grain = grain.Value; Weight = weight.Value }

        member x.Grain with get() = grain.Value and set(v) = grain.Value <- v
        member x.Weight with get () = weight.Value and set (value) = weight.Value <- value

(**

Of course, we could implement this approach using agents. however, I see no need to introduce this level of complexity (I know, they're not really that complex) as the Application doesn't really fit the use case.  
It is a simple, synchronous, single user based application and therefore does not need the benefits provided by Agents. These better suite, parallel, asynchronous, multi threaded/concurrent applications.

I plan on re-visiting this area in a future post in the series once I have put my implementation to the test.

##Views - The easy part

After all this model and view model development, the views obviously need to be updated.  
All that is needed to utilise the new VM changes is to add some extra elements to the view. For completeness, I have included a partial snapshot of the Recipe view.  

{% highlight xml %}
 <Grid Grid.Column="1">
    <Grid.RowDefinitions>
        <RowDefinition Height="32" />
        <RowDefinition Height="*"/>
        <RowDefinition Height="32" />
    </Grid.RowDefinitions>
    <TextBlock Text="Hop Additions" Grid.Row="0"  FontSize="14"/>
    <DataGrid Name="HopAdditions" Grid.Row="1" Margin="3"  ItemsSource="{Binding HopAdditions}" AutoGenerateColumns="false">
        <DataGrid.Columns>
            <DataGridTemplateColumn Header="Name" Width="*">
                <DataGridTemplateColumn.CellTemplate>
                    <DataTemplate>
                        <ComboBox Grid.Row="2" FontSize="16" Text="Select Hop" ItemsSource="{Binding RelativeSource={RelativeSource Mode=FindAncestor, AncestorType=local:RecipeView}, Path=DataContext.Hops, Mode=OneWay}" 
                                  SelectedItem="{Binding Hop, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" Name="HopDropDown">
                            <ComboBox.ItemTemplate>
                                <DataTemplate>
                                    <TextBlock Text="{Binding Name}" Width="{Binding ElementName=HopDropDown, Path=ActualWidth}" />
                                </DataTemplate>
                            </ComboBox.ItemTemplate>
                        </ComboBox>
                    </DataTemplate>
                </DataGridTemplateColumn.CellTemplate>
            </DataGridTemplateColumn>
            <DataGridTextColumn IsReadOnly="True" Binding="{Binding Hop.Alpha}" Header="Alpha (%)" Width="*"/>
            <DataGridTemplateColumn Header="Type" Width="*">
                <DataGridTemplateColumn.CellTemplate>
                    <DataTemplate>
                        <ComboBox Grid.Row="2" FontSize="16" Text="Select Hop Type" ItemsSource="{Binding RelativeSource={RelativeSource Mode=FindAncestor, AncestorType=local:RecipeView}, Path=DataContext.HopTypes, Mode=OneWay}" 
                                  SelectedItem="{Binding Type, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" Name="HopTypeDropDown">
                            <ComboBox.ItemTemplate>
                                <DataTemplate>
                                    <TextBlock Text="{Binding Converter={StaticResource DUConverter}}" Width="{Binding ElementName=HopTypeDropDown, Path=ActualWidth}" />
                                </DataTemplate>
                            </ComboBox.ItemTemplate>
                        </ComboBox>
                    </DataTemplate>
                </DataGridTemplateColumn.CellTemplate>
            </DataGridTemplateColumn>
            <DataGridTextColumn Binding="{Binding Weight, Mode=TwoWay}" Header="Weight" Width="*"/>
            <DataGridTextColumn Binding="{Binding Time, Mode=TwoWay}" Header="Time" Width="*"/>
            <DataGridTextColumn IsReadOnly="True" Binding="{Binding IBU, StringFormat=N2}" Header="IBU" Width="*"/>
            <DataGridTemplateColumn>
                <DataGridTemplateColumn.CellTemplate>
                    <DataTemplate>
                        <Button Name="RemoveHopButton"  Width="28" Height="28" HorizontalAlignment="Center" VerticalAlignment="Center" Background="OrangeRed" 
                                CommandParameter="{Binding}"
                                Command="{Binding RelativeSource={RelativeSource Mode=FindAncestor, AncestorType=local:RecipeView}, Path=DataContext.RemoveHopCommand}" Visibility="Hidden" Content="X" ></Button>
                        <DataTemplate.Triggers>
                            <DataTrigger Binding="{Binding Path=IsMouseOver, RelativeSource={RelativeSource AncestorType=DataGridRow}}" Value="True">
                                <Setter Property="Visibility" TargetName="RemoveHopButton" Value="Visible"/>
                            </DataTrigger>
                        </DataTemplate.Triggers>
                    </DataTemplate>
                </DataGridTemplateColumn.CellTemplate>
            </DataGridTemplateColumn>
        </DataGrid.Columns>
    </DataGrid>
    <Grid Grid.Row="2">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>
        <Button Name="AddHop" FontSize="14" Command="{Binding AddHopCommand}">Add Hop</Button>
    </Grid>
</Grid>
{% endhighlight %}

The view is extremely basic at this point. There are now two grids (only one is shown above), one for hops, one for grain.  
I have also added new columns to the grids as needed and provided a dropdown to switch the grain/hop respectively. Also of note is the use of a Discriminated Union directly as the options for the hop type dropdown.

To do this we need to use a value converter.

###Value Converters

The use of value converters is a common thing in WPF and as such I expect to need to implement many during the course of developing this app.  
So far, I have needed to create one F# specific converter; To convert Discriminated Unions into string representations of their names.

This value converter consists of some simple reflection calls in order to deduce the name of the DU passed to it.

*)

open Microsoft.FSharp.Reflection

type DiscriminatedUnionText() =

    let DuToString value uniontype =
        match FSharpValue.GetUnionFields(value, uniontype) with
        | case, _ -> case.Name

    interface System.Windows.Data.IValueConverter with
        override this.Convert(value, targetType, parameter, culture) =
            DuToString value (value.GetType())
            |> box
            
        override this.ConvertBack(value, targetType, parameter, culture) =
            raise(NotImplementedException())

(**

This converter is currently being used to display the names of the `HopType` discriminated union (declared earlier) in a drop down on each the DataGrid row of the hop grid. 
This usage can be seen above.

We will keep any converters created bundled together with the view implementation as they are entirely view logic.  
In my case, I will include them within the same, separate project for the view.  

As the Application grows larger, I will need to think about its structure.  
This is a new venture for me as it is the first WPF project I have undertaken purely in F#. I therefore plan to discuss this in the next post. 


##Thoughts on WPF development with F#

At this point I thought it would be apt to share my thoughts on the development process in WPF with F#.

Instead of a pros and cons style list I decided to focus on the features of F# that most influenced the implementation of this WPF application so far.  
This is also because I don't see there being any cons anymore, thanks to the XAML Type provider etc, I am now just as comfortable writing F# WPF apps as C#.

###Dependency Order

I briefly touched on this earlier and would like to expand on my points.  
I whole heartedly believe that the enforced dependency order in F# is beneficial to a WPF project.  
The fact that it removes the possibility of introducing pesky cyclic dependencies causes you to really think about what you are doing with your types.

It does prevent some common code styles that I often see used in C# being implemented (such as passing a parent reference to a child - without the use of interfaces), 
but for the most part, where I see them used, they are clearly rash decisions to take a short cut in development and the implementation would be better done another way.

I find that F# prevents this; It makes the developer think.  

Not just about types and their usage but about the application as a whole. This is especially apparent when organising application layers and designing the domain.

All of this makes creating 'Spaghetti Code' much more difficult.  
This has the massive benefit that your applications code is therefore more easy to follow; You can clearly see the intent from the implementation.  

As a result, the code base is generally smaller, bugs are less numerous and technical debt is vastly reduced. 

###Multi-Paradigm

For me, the multi-paradigm approach has great benefits to WPF development.  
We can keep the usual, class based, object oriented code (Albeit a shell of what we would see if using C#) that we are used to for our view models.  
This makes dealing with interaction with XAML a simple task and provides us with the ability to utilise the many OO implementations of features that already exist. (Observable collections and any of the XAML framework classes spring to mind)  

Of course, we strip the F# classes back to the bones and turn to a functional approach when dealing with the domain model.  
This gives us the best of both worlds (in my opinion).  

A simple, class based approach to handling interaction with XAML and the UI, and a functional, robust, domain model for 'business' logic.

I'm sure there are other approaches, but currently, in my experience (this application) why fret over being pure?  
Use the language features that exist where they best fit.

###Functional/F# Specific Benefits

Obviously, there are numerous benefits that a functional style brings to the table and I'm sure I don't need to go into great detail here.  
Therefore, I'll simply list the ones that have stood out the most so far during my WPF dev time.

- Robust type system for domain modelling.
- Immutability by default and controlled mutability.
- First class events.
- Pattern matching - Error prevention.

###Concise Code

An additional benefit brought by the F# language is conciseness.  
It is clear to see that F# is a 'write less, do more' language. Although this could be considered a very well planned side affect, it really does make a difference.  

In WPF development, this quickly becomes noticeable when your View Models are as small as 20 lines of code, while still packing all the functionality required.
The inherent simplicity of F# code also allows for rapid refactoring. This really hit me during my experiments with first class events and allowed me to make massive changes to the infrastructure with minimal time and effort.

##Summing Up

That's all for this post.  
I will now be concentrating on fleshing out functionality of the app and finally adding some styling!

Up in the next post in the series I will be focusing on three areas.

- Revisiting the Event implementation and view model communication.
- How to structure an F# WPF solution.
- Testing the application.

I may also expand on the domain modelling or XAML side of things just for completeness.  
Also to come in future posts are persistence/data stores and their integration with the application. 

Meanwhile, all my progress is visible on GitHub as usual, here: [BrewLab] 

[XAML type provider]:https://github.com/fsprojects/FsXaml
[BrewLab]: https://github.com/sjpemberton/BrewLab
[the previous post in the series]: http://stevenpemberton.net/blog/2015/03/29/FSharp-WPF-and-the-XAML-type-provider/
[units of measure]:http://stevenpemberton.net/blog/2015/03/11/FSharp-Units-Of-Measure/
[FSharp.ViewModule]:https://github.com/fsprojects/FSharp.ViewModule

*)

(*** hide ***)
---
layout: post
title: F# and WPF part two - functional models, decoupled views.
date: 11/05/2015
comments: true
tags: ["fsharp","WPF"]
catagories: ["guides","examples"]
series: F# and WPF
seriesId: FSharpWPF
series-post-number: 2
meta: Exploring the use of FSharp and WPF to create applications
--- 