
(*** hide ***)
module FsWPF


(**
#A basic particle system in F# and WPF

This post is part of the fantastic [F# advent] event. Thanks to Sergey for organising it!

For the post, I decided to do something a bit different and with a 'slightly' more christmassy theme.
That led me to create a very simple 2D particle engine.

The engine is created in F# of course, and for the UI I chose to use WPF.
WPF might not be a great choice for what I want, but it is familiar to me and meant I could utilise FSXaml and Fsharp.ViewModule to speed up development.

<!-- more -->

##Particles, Particles everywhere!

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