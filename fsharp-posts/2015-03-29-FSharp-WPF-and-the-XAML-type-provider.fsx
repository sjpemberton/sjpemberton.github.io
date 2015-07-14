
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

#Utilising the FsXaml type provider for WPF development in F#

Coming from a C# background with quite a bit of experience in WPF, I decided it was high time I took a stab at creating a WPF app from the ground up in F#.  
F# and UI development can seem like an unnatural fit due to the functional first nature of the language.  
However, the support for OOP features should allow me to tackle any problem I run into whilst still gaining the benefits of the F# language.

Luckily, the hard work around integration has already been tackled.  

- The [FsXaml] type provider handles XAML - F# code compatibility allowing us to develop applications in the same way as we could in C#.  
It also provides helpful additions such as an EventToCommand implementation.
- The [FSharp.ViewModule] library helps with generating MVVM ViewModels from F# modules and provides a base implementation for ease of development.
- A visual studio template (F# Empty Windows App) already exists for a quick start.

Alternatively the [FSharp.Desktop.UI] project takes a slightly different approach but provides many useful structures such as binding expressions.  
Of course, as the F# community is so awesome, the projects can be used together and an example of such is included in the `FsXaml` demo collection.

I'm learning as I go here so intend to start off with `FsXaml` and `FSharp.ViewModule` (as they have the least dependencies) and see how far I get building a full application from the ground up.  
For the application, I am taking my brewing calculations from [my previous post] and will be creating a UI to utilise them.

I expect that there will be **a lot** of content so I plan on breaking this up into numerous posts. 
 
Therefore this post should be considered part 1 of an ongoing series based on WPF and F#.

<!-- more -->

##FsXaml - Providing types from XAML

FsXaml includes a type provider to generate F# types from our XAML files.  
Using FsXaml is as simple as this:
*)

open System
open FsXaml

type RecipeView = XAML<"..\paket-files\sjpemberton\BrewLab\BrewLab\RecipeView.xaml", true>

(**

The first parameter is the relative path to my XAML file. (Which was referenced from my repository using [Paket] - An awesome feature)  
The second parameter is a boolean flag used for exposing named properties (This can be seen in use later on). 

The FsXaml Type provider does it's work and generates us a type for use in our application.  
This type can be used wherever we need to directly reference the elements declared in our XAML file and as such provides a way of adding 'code behind' to our views.   

I may not need to add code behind to my views, but it is nice to have the option and is extremely useful when we need to add some view specific code.
Let's look at how it's done:

###Code Behind

Extending from the code example above, we can create a 'controller' utilising one of the base classes provided by FsXaml and specify the type of the view it associated with. 
*)

type RecipeViewController() =
    inherit UserControlViewController<RecipeView>()

(** 
Next, we add some logic that would be explicitly used by this view.  
As a contrived example (I don't know if I'll need any view specific logic yet!), let's just highlight some text in a `TextBox` on a focus event. 
*)

    //Naive and not robust selection method 
    let highlightText (e:RoutedEventArgs) =
        let tb = e.OriginalSource :?> TextBox
        tb.SelectAll()

    override this.OnLoaded view = 
        view.RecipeName.GotMouseCapture.Add highlightText

(**
This is direct, code behind, in the RecipeViewController.  
All made possible by the FsXaml type provider.

You can see that the `TextBox` the event is attached to is called RecipeName. This is taken from the XAML by exposing the Name attribute given to the element (remember that boolean flag from earlier?).  

At this point, the XAML looks like so:  

{% highlight xml %}
<UserControl
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:fsxaml="clr-namespace:FsXaml;assembly=FsXaml.Wpf"
        xmlns:local="clr-namespace:Views;assembly=BrewLab"         
        xmlns:viewModels="clr-namespace:ViewModels;assembly=BrewLab"         
        MinHeight="220" MinWidth="300" Height="Auto"
        fsxaml:ViewController.Custom="{x:Type local:RecipeViewController}">
    <UserControl.DataContext>
        <viewModels:RecipeViewModel/>
    </UserControl.DataContext>
    <Grid>
        <TextBox Name="RecipeName" Text="Enter Recipe Name Here..." />
    </Grid>
</UserControl>
{% endhighlight %}

You can see in the above excerpt that the view controller is associated with the XAML file by the use of an attached property.  

{% highlight xml %}
fsxaml:ViewController.Custom="{x:Type local:RecipeViewController}"
{% endhighlight %}

This handles the instantiation of our view controller when the XAML is loaded, giving us a nice clean association in a way we are familiar with from C# (Through the use of partial classes).

The data context is set to my view model (which we'll get to shortly) and then the TextBox is declared.  
You can see that the TextBox is given the name which we saw the FsXaml type provider correctly expose for us to use in our code behind.

*NB: Also of note is that we need to set our XAML files to build as resource for them to function correctly.*

So, let's move on to the next step and investigate what FSharp.ViewModule provides to aid in creating our view models.

##View Models

To create a view model which will house all of our business logic for the view, we turn to `Fsharp.ViewModule`.  

The library provides us with various base classes that provide the usual features that we would likely end up implementing ourselves in a view model base class.

To start, we can declare our Recipe View Model straight from the base like so:

*)

open FSharp.ViewModule

type RecipeViewModel() as this = 
    inherit ViewModelBase()

(**
This will give us everything we need to start creating our UI.  
The `ViewModelBase` class provides an `INotifyPropertyChanged` implementation, a command factory, and some dependency and validation trackers/helpers to cut down on boiler plate code.  

Let's take a look at adding a view over some data and a simple command to add new records using the functionality provided by the base class.

###Commands

First let's define a record type to represent our data.
*)
open Units

type grain = {Name:string; Weight:float<g>; Potential:float<pgp>; Colour:float<EBC> }

(**
Next up, we can add an observable collection of our newly created record type and expose it as a member on our View Model.
*)

(***hide***)
module viewModel2 =
(***)
open System.Collections.ObjectModel

type RecipeViewModel() as this = 
    inherit ViewModelBase()

    let grain = ObservableCollection<grain>()
    member x.Grain with get() = grain


(**
Finally, we can add a command to handle a new grain addition to our recipe.  
Doing so couldn't be more simple. We execute the Factory function that corresponds to the type of command we want, passing in the required parameter(s).

For now, I will give the command a simple function to return a static value.  
Obviously we will need to make this more dynamic going forward; Likely by allowing the user to select from a dropdown list of available grain.
*)

(***hide***)
module viewModel3 =
open Units
(***)

open System.Collections.ObjectModel

type RecipeViewModel() as this = 
    inherit ViewModelBase()

    let grain = ObservableCollection<grain>()
    let addMaltCommand = 
        this.Factory.CommandSync(fun param -> 
            grain.Add { Name = "Maris Otter"
                        Weight = 0.0<g>
                        Potential = 37.0<pgp>
                        Colour = 4.0<EBC> })

    member x.Grain with get() = grain
    member x.AddMaltCommand = addMaltCommand

(**
The final piece of the puzzle is to update the XAML view to bind our new property and command to the appropriate elements.

{% highlight xml %}
<!--Repeated code removed for brevity-->
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="32"/>
        <RowDefinition Height="*"/>
        <RowDefinition Height="32" />
    </Grid.RowDefinitions>
    <TextBox Name="RecipeName" Text="Enter Recipe Name Here..." Grid.Row="0"  FontSize="16"/>
    <DataGrid Name="GrainBill" Grid.Row="1" Margin="3" ItemsSource="{Binding Grain}" >
    </DataGrid>
    <Button Grid.Row="2" FontSize="16" Command="{Binding AddMaltCommand}" >Add Grain</Button>
</Grid>
{% endhighlight %}

You can see the familiar mark-up above to bind the observable collection to the items source property of the `DataGrid` and also the command to the button.  

The command gets fired as expected and adds the default item representation to the grid. Success!  
We can, of course, alter this view and add some extra functionality like the ability to adjust the weight of grain.

##Mutability and Validation

What we need is the ability for the user to alter the weight of the selected grain directly in the grid.  
To do this we can simply make the Weight property mutable, but as I want to restrict what properties are exposed I will also specify the columns myself in the view's XAML.  

This idea is all well and good, but what do we do about validation?

For this we can leverage the validation functions included with `Fsharp.Viewmodule`, and for that I need to abstract out my Grain record into a separate view model.
*)

open FSharp.ViewModule
open FSharp.ViewModule.Validation

type GrainViewModel(name:string, potential:float<pgp>, colour:float<EBC>) as this = 
    inherit ViewModelBase()

    //Create a notifying property from the Backing store factory
    let weight = this.Factory.Backing(<@ this.Weight @>, 0.1<g>, greaterThan (fun a -> 0.0<g>))
    
    member val Name = name
    member x.Weight with get() = weight.Value and set(value) = weight.Value <- value
    member x.Potential:float<pgp> = potential
    member x.Colour:float<EBC> = colour

(**
The above is all standard stuff.  
A view model over our Grain data which includes a single mutable field for the weight as required.  

The backing store for the Weight property is created using a factory method, again provided by Fsharp.ViewModule.  
This function takes (amongst other arguments) a validation function.  In my case I have simply chosen the `greaterThan` function available.  

The validation function is fully composable with the other functions supplied by Fsharp.ViewModule and in addition, we can create *custom* validations through the use of a helper function.  
This composability allows us to create any validation we require in a neat, functional format.

All we then need to do is update the DataGrid in our view's XAML:

{% highlight xml %}
<DataGrid Name="GrainBill" Grid.Row="1" Margin="3" ItemsSource="{Binding Grain}" AutoGenerateColumns="false">
    <DataGrid.Columns>
        <DataGridTextColumn Binding="{Binding Name}" Header="Name" Width="*"/>
        <DataGridTextColumn Binding="{Binding Weight, Mode=TwoWay}" Header="Weight" Width="*"/>
    </DataGrid.Columns>
</DataGrid>
{% endhighlight %}

And update the original view model to incorporate the new one:
*)

(***hide***)
module viewModel4 =
open Units
(***)
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
Entering a value less than or equal to `0` will now cause the validation to fail.  
The default error display is to highlight the field and add an exclamation to the row header. This is what we can see here:

![Failed Validation](/content/images/post-images/WPF-GridValidity.png)

Of course, we could handle this better and the base class provided gives us some useful data collections to help, but that is a topic for another post in the series.

##Running the Application

That covers the basics of using F# to create a WPF application using the FsXaml and FSharp.ViewModule libraries. 

In case anyone is not using the 'F# Empty Windows App' project template, below is the XAML required for the main window, followed by the app start class.  
Implementing both of these will allow us to launch our application correctly.

{% highlight xml %}
<Window
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="clr-namespace:Views;assembly=BrewLab"
    xmlns:fsxaml="http://github.com/fsprojects/FsXaml"
    Title="Brewers Lab" Height="600" Width="800">
    <Grid Name="MainGrid">  
        <local:RecipeView />
    </Grid>
</Window>
{% endhighlight %}

*)

open System
open FsXaml

type App = XAML<"..\paket-files\sjpemberton\BrewLab\BrewLab\App.xaml">

[<STAThread>]
[<EntryPoint>]
let main argv =
    App().Root.Run()

(**
I'm going to leave part one here as it covers all the basic features of the brilliant libraries FsXaml and FSharp.ViewModule.  
We could quite happily create a full application with what we have here, but I expect we will need to dig a little deeper in some scenarios as we get into more complex areas of WPF.

The next post in this series will no doubt be based on a much more refined application (as I aim to actively develop it) and will highlight the areas that I found needed special, or noteworthy treatment to implement in F#.  

Meanwhile, you can follow the progress via my GitHub project here: [BrewLab]
*)

(** 

[FsXaml]:https://github.com/fsprojects/FsXaml
[FSharp.ViewModule]:https://github.com/fsprojects/FSharp.ViewModule
[FSharp.Desktop.UI]:https://github.com/fsprojects/FSharp.Desktop.UI
[Paket]: http://fsprojects.github.io/Paket/
[BrewLab]: https://github.com/sjpemberton/BrewLab
[my previous post]: http://stevenpemberton.net/blog/2015/03/11/FSharp-Units-Of-Measure/

*)


(*** hide ***)
---
layout: post
title: F# WPF and the XAML type provider - An exploration
date: 29/03/2015
comments: true
tags: ["fsharp","WPF"]
catagories: ["guides","examples"]
series: F# and WPF
seriesId: FSharpWPF
series-post-number: 1
meta: Exploring the use of FSharp and WPF to create applications
--- 