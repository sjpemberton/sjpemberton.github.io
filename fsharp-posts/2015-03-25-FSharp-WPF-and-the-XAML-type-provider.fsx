
(*** hide ***)
module FsWPF

#r """..\packages\FsXaml.Wpf\lib\net45\FsXaml.Wpf.dll"""
#r """..\packages\FsXaml.Wpf\lib\net45\FsXaml.Wpf.TypeProvider.dll"""
#r """..\packages\FSharp.ViewModule.Core\lib\net45\FSharp.ViewModule.Core.Wpf.dll"""
#r "PresentationFramework.dll"
#r "PresentationCore.dll"
#r "System.Xaml.dll"
#r "WindowsBase.dll"

open System.Windows
open System.Windows.Controls

(**

#Utilising the FsXaml type provider for WPF development in F#

Coming from a C# background with quite a bit of experience in WPF, I decided it was high time I took a stab at creating a WPF app from the ground up in F#.  
F# and UI development can seem like an unnatural fit do to the functional first nature of the language.  
However, the support for OOP features should allow me to tackle any problem I run into while still gaining the benefits of the F# language.

Luckily, the hard work around integration has already been tackled.  
The [FsXaml] type provider handles XAML compatibility allowing us to develop applications in the same way as we could in C# as well as providing helpful, event based, additions such as an EventToCommand implementation.
the [FSharp.ViewModule] library helps with generating MVVM ViewModels from F# modules.

Alternatively the [FSharp.Desktop.UI] project takes a slightly different approach but provides many useful structures such as binding expressions.
Of course, as the F# community is so awesome, the projects can be used together and an example of such is included in the FsXaml demo collection.

I'm learning as I go here so intend to start off with `FsXaml` (as it has the least dependencies) and see how far I get building a full application from the ground up.
I expect that there will be **a lot** of content here so I plan on breaking this up into numerous posts. 
 
Therefore this post should be considered part 1 of an ongoing series based on WPF and F#.

##FsXaml - Providing types from XAML

Using FsXaml is as simple as this:
*)

open System
open FsXaml

type RecipeView = XAML<"..\paket-files\sjpemberton\BrewLab\BrewLab\RecipeView.xaml", true>

(**

The first parameter is the relative path to my XAML file. (Which was referenced from my repository using [Paket])  
The second parameter is a boolean flag used for exposing named properties. 

The `FsXaml` Type provider does it's work and generates us a type for use in our application.  
This type can be used wherever we need to directly reference the elements declared in our XAML file and as such provides a way of adding 'code behind' to our views.   

I may not need to add code behind to my views, but it is nice to have the option and is extremely useful when we need to add some view specific code.
Let's look at how it's done:

Taking the above code we create a 'controller' utilising one of the base classes provided by FsXaml and specify the type of the view it associated with. 
*)

type RecipeViewController() =
    inherit UserControlViewController<RecipeView>()

(** 
Next, we add some logic that would be explicitly used by this view.  
As a contrived example (I don't know if I'll need any view specific logic yet!), let's just highlight some text focus. 
*)

    //Naive and not robust selection method 
    let highlightText (e:RoutedEventArgs) =
        let tb = e.OriginalSource :?> TextBox
        tb.SelectAll()

    override this.OnLoaded view = 
        view.RecipeName.GotMouseCapture.Add highlightText

(**
This is direct, code behind, in the RecipeViewController. All made possible by the FsXaml type provider.
You can see that the TextBox that the event is attached to is called RecipeName. This is taken from the XAML.  

At this simple stage, the XAML looked as follows:  


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

    fsxaml:ViewController.Custom="{x:Type local:RecipeViewController}"

This handles the instantiation of our view controller when the XAML is loaded, giving us a nice clean association in the way we are used to from C#.

The data context is set to my view model (which we'll get to shortly) and then the TextBox is declared.  
You can see that the TextBox is given a name which the FsXaml type provider correctly exposes for us to use in our code behind.

*NB: Also of note is that we need to set our XAML files to build as resource for them to function correctly.*

##View Models

To create a view model which will house all of our business logic for the view, we turn to `Fsharp.ViewModule`.  
The library provides us with various base classes that provide the usual features we would likely end up encapsulating in view model base.

For example we can declare our Recipe View Model straight from the base like so:

*)

open FSharp.ViewModule

type RecipeViewModel() as this = 
    inherit ViewModelBase()

(**
This will give us all we currently need to start creating our UI.  
The ViewModelBase class provides an INotifyPropertyChanged implementation and some dependency and validation trackers/helpers to cut down on boiler plate code.  


*)

(** 

[FsXaml]:https://github.com/fsprojects/FsXaml
[FSharp.ViewModule]:https://github.com/fsprojects/FSharp.ViewModule
[FSharp.Desktop.UI]:https://github.com/fsprojects/FSharp.Desktop.UI
[Paket]: http://fsprojects.github.io/Paket/

*)


(*** hide ***)
---
layout: post
title: F# WPF and the XAML type provider - An exploration
date: 25/03/2015
comments: true
tags: ["fsharp","WPF"]
catagories: ["guides","examples"]
series: F# and WPF
series-post-number: 1
meta: Exploring the use of FSharp and WPF to create applications
--- 