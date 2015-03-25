
(*** hide ***)
module FsWPF
(**

#Utilising the FsXaml type provider for WPF development in F#

Coming from a C# background with quite a bit of experience in WPF, I decided it was high time I took a stab at creating a WPF app from the ground up in F#.  

Luckily, the hard work around integration has already been tackled.  
The FsXaml type provider handles XAML compatability allowing us to develop applications in the same way as we would in C#.
the FSharp.ViewModule library helps with generating MVVM ViewModels from F# modules.

Alternatively the FSharp.Desktop.UI project takes a slightly different (more MVC based) approach but provides many useful structures such as binding expressions.
Of course, as the F# community is so awesome, the projects can be used together and an example of such is included in the FsXaml demo collection.


##FsXaml - Providing types from XAML

Using FsXaml is as simple as this:
*)
	open FsXaml

	type RecipeView = XAML<"RecipeView.xaml">

(**
*)

(*** hide ***)
---
layout: post
title: F# WPF and the XAML type provider - An exploration
date: 25/03/2015
comments: true
tags: ["fsharp","WPF"]
catagories: ["guides","examples"]
meta: Exploring the use of FSharp and WPF to create applications
--- 