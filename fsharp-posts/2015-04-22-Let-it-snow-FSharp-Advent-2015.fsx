
(*** hide ***)
namespace FsWPF
open System


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

(*** hide ***)
    let toDegrees rads =
        rads * (180.0 / Math.PI)

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
          AlphaTarget: float
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
          Elapsed: float}

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
    
        let boxCollider min max particle =
            pointInBounds (min,max) particle.Coords

        let floorCollider particle = 
            {particle with Locked = boxCollider {X = 0.0; Y=670.0} {X = 1280.0; Y=720.0} particle}
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
This includes an alpha fade in/out as well as the current coordiantes.  

At the same time we need to decrement the delta time passed from the particles life and update it's velocity for future use.

*)

        let updateAlpha p =
            match 1.0 - p.TimeToLive / p.Life with
            | lifeRatio when lifeRatio <= 0.25
                -> { p with Alpha = lifeRatio / 0.25 * p.AlphaTarget }
            | lifeRatio when lifeRatio >= 0.75 
                -> { p with Alpha = (1.0 - lifeRatio) / 0.25 * p.AlphaTarget }
            | _ -> { p with Alpha = p.AlphaTarget }

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
            state <- tick secs {state with Elapsed = state.Elapsed + secs}
            state


(**

Phew! That covered a lot of code in a pretty dense format.  
The code above gives us pretty much all we need to start creating an actual animation.

##Creating an animation

In order to create an animation we need to tackle two parts.  

 - The creation of a UI, complete with some rendering logic.
 - Creating a set of specific emitters, colliders, forces and particle types to implement the animation logic itself.
 
 We will start by setting up an instance of our animation type to represent snow.
 
 ###It's Snowtime!
 
 First up, we need to tackle our representation of a snowflake and create an emitter in order to initialise them.  
 For our snowflakes, we will create a box emitter that creates random starting coordinates for our snowflakes within a 
 rectangle that starts above and to the left of the view port.

*)
(*** hide ***)
module snow =
    open Engine

    let floorCollider particle = 
        {particle with Locked = BoxCollider {X = 0.0; Y=670.0} {X = 1280.0; Y=720.0} particle}

    let private rand = Random()

(***)

    let BoxEmitter min max = 
        fun () -> { X = float (rand.Next(int min.X, int max.X))
                    Y = float (rand.Next(int min.Y, int max.Y)) }

    let emitter = BoxEmitter {X = -50.0; Y= -40.0} {X = 1280.0; Y=100.0}

(**

We will the use this emitter in or snowflake generator function.  
This function basically just generates some random values for the important properties of the particle.  

Let's run through some of these quickly and describe there usage.

 - Coords: The initial coordinates created by our box emitter.
 - life/TimeToLive: This value controls how long the particle should live for.
 - Mass: Used in the acceleration calculation. We use a random value between 0.002 and 0.003.
 - Alpha/AlphaTarget - Control the alpha of the particle in the UI
 - Velocity: A vector representing the particles initial direction and speed
 - Acceleration: Starts at zero
 - Rotation: Starting particle rotation in degrees
 - Scale: A random scale between 0.5 and 1 to give the particles a random size

*)

    let CreateSnowFlake () = 
        let life = 4.0 * (rand.NextDouble() * 2.0)
        { Coords = emitter ()
          Img = "SnowFlake.png"
          Mass = (rand.NextDouble() / 1000.0) + 0.001
          Alpha = 1.0
          AlphaTarget = 1.0
          Velocity = toCartesian {Radius = 30.0; Theta = rand.NextDouble() * Math.PI * 2.0}
          AngularVelocity = 1.0
          Acceleration = defaultVector
          TimeToLive = life
          Life = life
          Rotation = (Math.PI * 2.0 * rand.NextDouble()) |> toDegrees
          Scale = rand.NextDouble() / 2.0 + 0.5 
          Locked = false} 

(**
Next up. Forces.  
For our snow flakes we want gravity and wind in order to give more dynamic movement to the particles.
*)

    let gravity p = {X = 0.0; Y = 9.81 / 60.0}
    let wind p = {X = 0.5; Y = 0.0}

(**
Finally we need a tick (or update) function that can be called during iteration of the simulation loop.  
This function will update the the forces in play. 

Particular notice should be given to the wind force that is calculated based upon a sine wave (or rather half of it) 
in order to give it varying magnitude in a single direction.


*)
    let private tick delta state =
        let wind = fun _ -> { X = (Math.Sin state.Elapsed * 0.5 + 0.5) / 10.0;
                              Y = Math.Sin(state.Elapsed * 0.5) / 10.0}
        { state with Forces = [gravity; wind]}


(*** hide ***)
    let zzz = ()

(**
We could also set the colliders here but as we only have our floor collider that we saw earlier, there is no need.

The last thing we need to do, is create the animation instance passing in the required parameters.  
*)
    let Animation = new Animation(10.0, 1000, CreateSnowFlake, tick, [gravity; wind], [floorCollider]) 


(**
That's it! Our animation is no ready to be ran.  
Of course, we are going to need some form of UI to show the results and that is where I turn to WPF.

##The Renderer

As I mentioned at the beggining of the post, WPF is really not a good choice for particle animation.  
However it should hold up to the abuse we give it provided we allow for a degree of mutabality and alter our approach slightly in order to keep things smooth (As smooth as it can be!).

For the renderer implementation, I am making use of FSXaml and FSharp.ViewModule.  
The reason for this is two fold. They are familiar to me and greatly speed up development.

So, where do we start?

The control loop is a good base point.

###The Animation Loop

An animation loop is a simple beast at it's core, but can become complex if striving to keep things running smoothly on machines of varying performance.
We can start by creating a ViewModel to house our animation control logic.  

Within this view model, we can create our loop.
As we are using WPF, I found the easiest way to create a loop was to utilise a DispatchTimer as this plays well with WPFs own rendering engine.

*)

(*** hide ***)
namespace ViewModels

(***)

type GlobeViewModel() as this = 
    inherit EventViewModelBase<EngineEvent>() //If not using events, change base
    
    let frameTimer = new DispatcherTimer()
    
(**
We now need to attach an event handler to the 'Tick' of the timer.  
This is where we call both the update of our simulation, and specifiy any updates to render to the UI.


*)

    let onTick (_, elapsed) =
        let snowState = Snow.Animation.Update elapsed
        snowState.Particles |> updateParticleUI particles

(**

The onTick function takes a tuple comprised of the current tick time and the total elapsed time.  
All this tick function does is cal update on the Snow animation and then updates the UI.  

You will have noticed the call to updateParticleUI above, we will get to that shortly.

First let's look at how we attch this function to the loop.
This is done in the constructor of our view model and we pass the frameTimer.Tick observable to the scan function of the observable module.
This allows us to capture some state on each tick and is how we go about getting the current time and the elapsed time (in seconds). This is all done with the use of the current tick count.

This new observable function is then passed to subscribe and given our onTick function as a callback.  
Finally we set the tick interval so that it fires 60 times a second and start the timer.
*)

    do
        frameTimer.Tick
        |> Observable.scan (fun (previous, elapsed) _  -> 
            (float Environment.TickCount, (float Environment.TickCount - previous) / 1000.0)) (float Environment.TickCount, float Environment.TickCount)
        |> Observable.subscribe onTick
        |> ignore
        frameTimer.Interval <- TimeSpan.FromSeconds(1.0 / 60.0);
        frameTimer.Start();
        
(**

###Handling UI Updates

We saw earlier that we need to update the UI on each tick.  
Before we can do this we need to create a UI to update!

So let's look at what's needed.

 - A Particle ViewModel. We use this to prevent adding mutablity to the engines representation and to speed things up on the rendering side
 - An observable collection to hold our particles
 - A UI representation of a particle. This will be an image in our case representing the particle texture
 - A canvas or other UI component in order to display the particles

As usual we can look at each of these in turn.  
For the ViewModel I will make use of FSharp.ViewModules BaseViewModel in order to take advantage of its implementation of INotifyPropertyChanged.  
This means my UI can bind straight into its properties and update itself automatically on each tick.

*)

    type NotifyingPoint(x,y,scale,rotation,alpha) as this = 
        inherit ViewModelBase()
    
        let rotation = this.Factory.Backing(<@this.Rotation@>, rotation)
        let scale = this.Factory.Backing(<@this.Scale@>, scale)
        let alpha = this.Factory.Backing(<@this.Alpha@>, alpha)
        let x = this.Factory.Backing(<@this.X@>, x)
        let y = this.Factory.Backing(<@this.Y@>, y)
    
        member this.Rotation with get () = rotation.Value and set (v) = rotation.Value <- v
        member this.Scale with get () = scale.Value and set (v) = scale.Value <- v
        member this.Alpha with get () = alpha.Value and set (v) = alpha.Value <- v
        member this.X with get () = x.Value and set (v) = x.Value <- v
        member this.Y with get () = y.Value and set (v) = y.Value <- v

(**
Our MainViewModel can then be given the relevant colection like so and expose it as a method that can be bound to from XAML.
*)

    let particles = ObservableCollection<NotifyingPoint>() //Snow
    member x.Particles: ObservableCollection<NotifyingPoint> = particles
    
(**
This is then used during the onTick function from earlier in the statement `snowState.Particles |> updateParticleUI particles`  

The `updateParticleUI` function iterates the given collection of particle records and either creates a new viewModel for the particel if it doesn't exist, or alters an existing ones state.

You will notice that I am relying on the particle lists index in order to locate the correct view model to update.  
This is a consequance of a much needed use of mutable data as we cannot rebuild the UI collection due to the massive performance hit that occurs.

We also need to reverse the list as new particles are always added to start of the particles collection.

*)
    let updateParticleUI (collection: ObservableCollection<NotifyingPoint>) particles = 
        particles 
        |> List.rev
        |> List.iteri (fun i p-> 
            match i,p with
            | x,_ when i < collection.Count ->
                collection.[i].Visible <- true
                collection.[i].X <- p.Coords.X
                collection.[i].Y <- p.Coords.Y
                collection.[i].Rotation <- p.Rotation
                collection.[i].Alpha <- p.Alpha
            | _,_ ->
                collection.Add(new NotifyingPoint(p.Coords.X, p.Coords.Y,p.Scale,p.Rotation, p.Alpha)))

(**

Great, onto the UI.

###The XAML View

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