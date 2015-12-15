
(*** hide ***)
namespace FsWPF


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
 This should be fully abstracted away so that the UI implementation could be provided in any language/technology, as long as we can call the F# engine code from it.

We will start by creating the necessary types in order to represent our particles.
I have chosen to use records here for simplicity. It is worth pointing out that using immutable data structures, while perfectly valid, will slow down the implementation.  
However, for this simple demo I will stick with immutable structures (At least for the engine).

A particle will consist of a vector (2D pair of coordinates) and various attributes that control how it is to be rendered and controlled. 
Such as scale, rotation, alpha and time to live.

The need for a basic vector type, as well as a general representation of Cartesian coordinates prompted me to create a core types module to hold the common record types.

This module includes our Point record, which is used as both a vector and in cases where we just need Cartesian coordinates, 
and also some useful common functions to operate on a vector such as calculating the magnitude (or distance) and summing to vectors.

This, of course could have been implemented in a class but I like the functional approach as it will allow me to pipe the vectors (points) to successive function calls.

As well as the Cartesian point, we also need a Polar record.
This is used to determine a random starting direction for our particles.

*)

[<AutoOpenAttribute>]
module Core =

    open System

    type Vector = { X : float; Y : float }
    type Polar = { Radius : float; Theta : float }

    let defaultVector = { X = 0.0; Y = 0.0 }

    let sum a b = { X = a.X + b.X; Y = a.Y + b.Y }
    let diff a b = { X = a.X - b.X; Y = a.Y - b.Y }

    let magnitude vector = sqrt vector.X ** 2.0 + vector.Y ** 2.0

    let toPolar vec = 
        { Radius = magnitude vec
          Theta = System.Math.Atan(vec.Y / vec.X) }

    let toCartesian polar = 
        { X = polar.Radius * Math.Cos(polar.Theta)
          Y = polar.Radius * Math.Sin(polar.Theta) }

(**

Now that is out of the way, we can look at creating the representation of our particle.

We know that this particle record will need some vectors for position, acceleration and velocity.
We also know that we require some properties that will be used to alter rendering by the UI.  

These include alpha and time to live as they can be explicitly changed by the particle engine each tick.


*)

module Particle =
    
    type Particle = 
        { Coords : Vector
          Mass : float
          Img : string
          Alpha: float
          AlphaMod: float
          Velocity : Vector
          AngularVelocity: float
          Acceleration : Vector
          TimeToLive : float 
          Life: float
          Rotation: float
          Scale: float
          Locked: bool}
      
(**

Let's leave the particles there for a minute and investigate what we need to engine to do.

##The engine

Form the list provided earlier we can see we need a few basic things for our engine.
Let's look at each in turn.

###State Updates

State update swill be handled on a per tick basis. (I'll get into that in more detail later)  
For now, all we care about is representing the state that must mutate each iteration.

I have chosen to capture the mutable state in a single record on a new type that represents an instance of our engine, an animation. 

*)

module Engine =

    open Particle

    type State = 
        { Particles : list<Particle>
          Forces : list<Vector -> Vector>
          Colliders : list<Particle -> Particle> 
          Counter: float}

    type Animation(spawnRate, maxParticles, particleEmitter, tick, forces, colliders) as x = 
    
        let mutable state = 
            { Particles = list<Particle>.Empty
              Forces = forces
              Colliders = colliders
              Counter = 0.0 }
    
(**

The state record will be used to hold the animations list of particles, the forces in play, the colliders to use, and a counter for elapsed time.
You can also see that I have specified a few more parameters on the types constructor.

These are:

- A Particle emitter. We mentioned emitters earlier and will see them again later. They basically create particles for the animation.
- Spawn rate. The rate at which particles are spawned in (particles per second)
- Max Particles. The upper limit of particles that can exist at one time.
- Tick. This is a function that can be used to apply additional, custom logic on an animation tick.

With this in place, we can go about defining a function that will update the animation on each iteration of a loop.  
The loop itself is not defined in the engine, but rather in the calling code. 
This allows for specific architectures to best perform the looping to the renderers benefit.

The update function will need to do multiple things.

 - Call any specific update function of the animation
 - Spawn new particles using the provided emitter
 - Calculate each particles acceleration vector, applying any required forces
 - Check each particle for collisions
 - Update the particles position based on previous steps

We can therefore work through this list to handle each step.

###Emitters - Spawning new particles

New particles are spawned by using the emitter passed to the animations constructor.
An emitter is a simple function that just creates a particle and sets some default values.

Our spawning function will take an amount of particles to spawn and the current particle list as parameters.  
It will then recursively spawn particles until this amount is reached, at which point it will return then new list.

Note everything is immutable here, so we are creating new particles and a list each iteration.
There will however come a time when we hit the limit on the number of particles we can create (taken from the max particles parameter passed to the constructor).  
At such a time, we need to replace an old, dead particle.  

By keeping all the dead particles in the array, and replacing a dead particle here, we gain the benefit of not having to track particle positions within the array,
which as we will see later, helps with keeping things simple when it comes to rendering.

Below is the spawnParticle function, along with the replace function.

*)

        let replace test replaceWith list = 
            let rec search acc = 
                function 
                | [] -> None
                | h :: t -> 
                    match test h with
                    | true -> Some(List.rev t @ replaceWith :: acc)
                    | false -> search (h :: acc) t
            search [] list

        let rec spawnParticles toSpawn (accu: Particle list) = 
            match toSpawn with
            | a when accu.Length >= maxParticles -> 
                let replaced = replace (fun p -> p.Locked || p.TimeToLive < 0.0) (particleEmitter()) accu
                match replaced with 
                | Some replaced -> 
                    if toSpawn > 1.0 then spawnParticles (toSpawn - 1.0) replaced else replaced
                | _ -> accu |> List.rev
            | b when toSpawn > 0.0 -> 
                particleEmitter() :: accu
                |> spawnParticles (toSpawn - 1.0)
            | _ -> accu |> List.rev

(**

###Forces - Calculating Particle Acceleration

Next up, determining the new value for particle acceleration.  

Acceleration can be determined by following Newton's 2nd law of motion.  

Acceleration = Force / Mass

Force in this case is net force, which is the sum of all forces in our animation.

This is a fairly trivial task, we just need to apply each force in play to a particles current vector (coordinates), 
summing them as we go. A fold is the perfect choice here as it won't fail if the list is empty, we simply get back the initial state.

We then divide this by the particles mass.

*)

        let applyForce particle accel force =
            particle.Coords
            |> force
            |> sum accel

        //A = F/M
        let calcAcceleration particle = 
            { particle with Acceleration = 
                                state.Forces
                                |> List.fold (applyForce particle) defaultVector
                                |> fun f -> { f with X = f.X / particle.Mass
                                                     Y = f.Y / particle.Mass }}

(**

After calculating the acceleration, we check for collisions.

###Colliders - Handling Particle Collisions

Collisions come in many guises ranging from simple floor/wall collisions, to more complex collisions with other particles or objects.
For my simple animation, I will only require a collider on the floor and possible the walls.  

A collider can be defined as a set of bounds, and in the simplest form, a box, represented by a pair of vectors.
We can also include the logic to handle what happens when a collision is detected within the collision function.

We can declare a simple helper function and the wrap it in an explicit collider function that takes a particle as input.
We can then utilise this to create a box collider with explicit logic to execute when a collision occurs.

*)

        let pointInBounds (min,max) point = 
            min.X < point.X && max.Y > point.Y && min.Y < point.Y && max.Y > point.Y
    
        let BoxCollider min max particle =
            pointInBounds (min,max) particle.Coords

        let floorCollider particle = 
            {particle with Locked = BoxCollider {X = 0.0; Y=670.0} {X = 1280.0; Y=720.0} particle}
(**

We see above the use of a box collider that will lock a particle in place when it hits an area of the screen near to the floor.

To use this, and any other colliders, we only need execute them in turn. Or better yet, compose them together.  
We can utilise fold to do this, like so.

*)

        let applyColliders particle = 
            particle
            |> match state.Colliders with
               | [] -> id
               | list -> (list |> List.fold (>>) id)

(**

The final piece of the puzzle is to apply the rest of the updates to the particle.  
This includes an alpha fade in/out

*)

        let updateAlpha p =
            match 1.0 - p.TimeToLive / p.Life with
            | lifeRatio when lifeRatio <= 0.25
                -> { p with Alpha = lifeRatio / 0.25 * p.AlphaMod }
            | lifeRatio when lifeRatio >= 0.75 
                -> { p with Alpha = (1.0 - lifeRatio) / 0.25 * p.AlphaMod }
            | _ -> { p with Alpha = p.AlphaMod }

        let updatePosition delta p =
            match p.Locked with
            | true -> { p with TimeToLive = p.TimeToLive - delta}
            | false ->{ p with TimeToLive = p.TimeToLive - delta
                               Coords = sum p.Coords {X = p.Velocity.X * delta; Y = p.Velocity.Y * delta} 
                               Velocity = sum p.Velocity {X = p.Acceleration.X * delta; Y = p.Acceleration.Y * delta} 
                               Rotation = p.Rotation + (p.AngularVelocity * delta)}

(**
Finally we can create our engines update function.  
It simply uses map to create a new list of particles from all the update functions and sets this into a new state record for the animation to store.

We also make a public port to an update. This is the loop logics access to control how often the engine is updated.
This method take a float representing the amount of time passed, in seconds, since the last call.
*)

        let tick secs state = 
            let updatedState = tick secs state // Tick updates forces
            { updatedState with Particles = 
                                    spawnParticles (secs * spawnRate) (updatedState.Particles |> List.rev) 
                                    |> List.map (fun p -> 
                                           calcAcceleration p
                                           |> applyColliders
                                           |> updateAlpha
                                           |> updatePosition secs) }

        member this.Update(secs) = 
            state <- tick secs state
            state


(**

Phew! That covered a lot of code in a pretty dense format.  
The code we just covered gives us pretty much all we need to start creating an actual animation.

##Creating an animation

In order to create an animation we need to tackle two parts.  

 - The creation of a UI, complete with some rendering logic.
 - Creating a set of specifc emitters, colliders, forces and particle types to implement the animation logic itself.
 
 We will start by setting up an instance of our animation type to represent snow.
 
 ###It's Snowtime!
 
 First up, we need to tackle our representation of a snowflake and create an emitter in order to initialise them.  
 

*)

(**
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