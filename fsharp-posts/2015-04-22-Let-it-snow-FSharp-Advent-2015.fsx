
(*** hide ***)
module FsWPF


(**
#A basic particle system in F# and WPF

This post is part of the fantastic [F# advent] event. Thanks to Sergey for organising it!

For the post, I decided to do something a bit different and with a 'slightly' more Christmassy theme.
That led me to create a very simple 2D particle engine.

The engine is created in F# of course, and for the UI I chose to use WPF.
WPF might not be a great choice for what I want, but it is familiar to me and meant I could utilise FSXaml and Fsharp.ViewModule to speed up development.

A much better option would have been to use OpenGL or DirectX but the learning curve would have likely been to steep for the time I had.
Anyway, on to the fun part.

<!-- more -->

##Particles, Particles everywhere!

A basic particle engine is made up of a few simple parts, allowing for a nice abstraction.
The engine will consist of the following:

 - Emitters: These create particles and initialise them with starting state
 - Colliders: Used to apply logic when a particle collides with an entity (or space)
 - Forces: Applied to the particles in order to alter their state
 
 In addition, there will be a simulation loop in order to control the updating of the particle engine and the rendering of the particles to the UI.
 This should be fully abstracted away so that the UI implementation could be provided in any language/technology, as long as we can call the F# engine from it.


*)

(***
[F# advent]:https://sergeytihon.wordpress.com/2015/10/25/f-advent-calendar-in-english-2015/
*)

(*** hide ***)
---
layout: post
title: Let it Snow! - F# Advent 2015
date: 19/12/2015
comments: true
tags: ["fsharp","WPF",]
catagories: ["guides","examples"]
meta: A basic particle system in FSharp and WPF for FSharp Advent 2015
---