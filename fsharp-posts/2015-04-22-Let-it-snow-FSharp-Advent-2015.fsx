
(*** hide ***)
#r """..\packages\FsXaml.Wpf\lib\net45\FsXaml.Wpf.dll"""
#r """..\packages\FsXaml.Wpf\lib\net45\FsXaml.Wpf.TypeProvider.dll"""
#r """..\packages\FSharp.ViewModule.Core\lib\net45\FSharp.ViewModule.Core.Wpf.dll"""
#r "PresentationFramework.dll"
#r "PresentationCore.dll"
#r "System.Xaml.dll"
#r "WindowsBase.dll"

namespace FsWPF

(**
#A basic particle system in F# and WPF

>This post is part of the fantastic [F# advent] event. Thanks to Sergey for organising it!  

While my post my not be quite as advanced as the others so far, I hope you'll find it as interesting as I did writing it.

For the post, I wanted to do something a bit different and with a *slightly* more Christmassy (more wintery at least) theme then my usual posts.

What could be better than a snow simulation implemented using a basic particle system?  
Well, probably quite a lot, but hey, it does look pretty cool - Especially with the F# logo in the background.

The engine is implemented entirely in F#, in a *mostly* functional manner, while the UI uses WPF.  
WPF might not be a great choice for particle animation, but it is familiar to me and means I can utilise [FSXaml] and [Fsharp.ViewModule] to speed up development.

Here's a sneak peak of where we're heading.

<video autoplay controls loop poster="/content/images/post-images/FsAdvent1.jpg" src="/content/images/post-images/fsAdvent1.webm">
Sorry, your browser doesn't support HTML5 video!
Don't worry you can download the video <a href="http://stevenpemberton.net/content/images/post-images/fsAdvent1.webm">here</a> and view it in a video player.
Alternatively, you can view <a href="http://stevenpemberton.net/content/images/post-images/fsAdvent1.gif">this .gif</a> instead.
</video>

<!-- more -->

##Particles, particles everywhere!

We'll start with a bit of background on particle engines.

A basic particle engine is made up of a few simple parts.

 - Emitters: These create particles and initialise them with starting state
 - Colliders: Used to apply logic when a particle collides with an entity (or space)
 - Forces: Applied to the particles in order to alter their state

These parts, are then utilised by a core *engine* in order to generate, update and effectively *simulate* a representation of a particle, or more importantly, many particles.
 
In addition, an engine requires a loop in order to drive the updating of the particle engines simulation and subsequently the rendering of the particles to the UI.
This *should* be fully abstracted away so that the UI implementation could be provided in any language/technology, as long as the engine can be called from it.

So, we know what is needed, where do we start?

###Core Types

We will start by creating the necessary types that we will need in all areas of the engine code and in particular the represent our particles.
I have chosen to use records here for simplicity. It is worth pointing out that using immutable data structures, while perfectly valid, 
will slow down the implementation due to the overhead of recreating the records and containing lists each iteration.  

However, I will stick with immutable structures (At least for the engine), as it's safer and the overhead is bearable for such a simple simulation.

**Vectors**

A particle will consist of its current coordinates, acceleration, and velocity along with various attributes that will directly affect how it is controlled by the engine and rendered to the UI. 
These main attributes can each be represented by a 2D vector (A pair of Cartesian coordinates).

The other attributes such as scale, rotation, alpha etc will be looked at in detail later when we declare the Particle type.

Firstly we will create a core types module to hold the record types and functions related to Vectors and coordinates.

This module includes our Vector record, which is used as both a vector and in cases where we just need Cartesian coordinates, 
in addition to some useful common functions to operate on vector records, such as for calculating the magnitude (or distance) and summing two vectors.

This, of course could have been implemented in a class but I like the functional approach as it will allow me to pipe the vectors to successive function calls.

As well as the Cartesian based vector, we also need a Polar coordinate record (an angle and radius).  
Polar coordinates can be used to determine a random starting direction and magnitude for our particles vectors.

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

Now that we have our core types, let's look at creating a record that represents a particle.

**Particles**

We know that this particle record will need some vectors for position, acceleration and velocity.
We also know that we require some properties that will be used to alter rendering by the UI.  

These include alpha and time to live as they can be explicitly changed by the particle engine each tick.

The full record can be seen below.  
When we get around to creating a particular representation of a particle (i.e the snow) I'll explain some of the properties in more detail.
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

(*** define: exert-force ***)
    let exertForce epicentre strength decayF vec = 
        let diff = epicentre |> diff vec 
        let distance = magnitude diff 
        let decay = decayF distance
        { X = diff.X / distance * decay * strength
          Y = diff.Y / distance * decay * strength }
      
(**

Let's leave the particles there for a minute and investigate what we need the engine to do with them.

##The Engine

From the list provided earlier we can see we need a few basic things for our engine.
We will look at each of these in the coming sections, but first, a quick word on state.

###Engine State

It's obvious that our engine will need to hold state.  
This state will need to be updated on a per tick basis. (I'll get into that in more detail later)  

For now, all we care about is representing the state that must mutate each iteration. Luckily for us, there's not much we need to store.  
Therefore we can capture the mutable state in a single record on a new type that represents an instance of our engine, or, an animation. 

Hopefully this will help to keep the amount of mutable state we need to track down to a minimum.

*)
(***hide***)
module Engine =
    open System
    open Particle

(*** define: engine-event ***)
    type MouseButtonStatus = 
        | LeftDown
        | RightDown
        | Released

    type EngineEvent = 
        | MouseEvent of MouseButtonStatus * position : Vector
(***)
    type State = 
        { Particles : list<Particle>
          Forces : list<Vector -> Vector>
          Colliders : list<Particle -> Particle> 
          Elapsed: float}

    type Animation(spawnRate, maxParticles, particleEmitter, tick, forces, colliders) as x = 

(*** define: events ***)
        let mouseEvent = new Event<EngineEvent>()
(***)
        let mutable state = 
            { Particles = list<Particle>.Empty
              Forces = forces
              Colliders = colliders
              Elapsed = 0.0 }
    
(**

The state record will be used to hold the animations list of particles, the forces in play, the colliders to use, and a counter for elapsed time.
You can also see that I have specified a few more parameters in addition to these on the animation types constructor.

These are:

- A Particle emitter. We mentioned emitters earlier and will see them again later. They basically create particles for the animation.
- Spawn rate. The rate at which particles are spawned (in particles per second)
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
An emitter is a simple function that creates a particle and sets some default values.

This emitter is utilised in a spawning function which takes an amount of particles to spawn and the current particle list as parameters.  
It can then recursively spawn particles until this amount is reached, at which point it will return then new list.

Note, everything is immutable here, so we are creating new particles and a list each iteration which is obviously not the most efficient thing to do.  
There will also come a time when we hit the limit on the number of particles we can create (taken from the max particles parameter passed to the constructor).  
At which time, we need to replace an old dead particle, with a new one.  

By keeping all the dead particles in the array, we can easily replace a dead particle from within our spawning function.  
We then gain the benefit of not having to keep track of particles via an id, or by holding multiple collections.  
Instead we can rely on the fact that once a particle is created, it stays in the same index of the list.

This, as we will see later, helps with keeping things simple when it comes to rendering.

Below is the `spawnParticle` and `replace` functions.

*)

        let replace test replacement list = 
            let rec search acc = 
                function 
                | [] -> None
                | h :: t -> 
                    match test h with
                    | true -> Some(List.rev t @ replacement :: acc)
                    | false -> search (h :: acc) t
            search [] list
    
        let rec spawnParticles toSpawn (accu : Particle list) = 
            match toSpawn with
            | a when accu.Length >= maxParticles -> 
                let replaced = replace (fun p -> p.Locked || p.TimeToLive < 0.0) (particleEmitter()) accu
                match replaced with
                | Some replaced -> 
                    if toSpawn > 1.0 then spawnParticles (toSpawn - 1.0) replaced |> List.rev
                    else replaced |> List.rev
                | _ -> accu
            | b when toSpawn > 0.0 -> particleEmitter() :: accu |> spawnParticles (toSpawn - 1.0)
            | _ -> accu

(**

###Forces - Calculating Particle Acceleration

Next up, determining the new value for a particles acceleration.  
Acceleration can be determined by following Newton's 2nd law of motion.  

>Acceleration = Force / Mass

Force in this case is net force, which is the sum of all forces in our animation.

Calculating net force is a fairly trivial task, we just need to apply each force to a particles current vector (coordinates), 
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
For our simple animation, I will only require a collider on the floor and possibly the walls.  

A collider can be defined as a set of bounds, and in the simplest form, a box, represented by a pair of vectors.
We can also include the logic to handle what happens when a collision is detected within the collision function itself.

This is achieved by creating a simple helper function which we then wrap in a specific collider function that takes a particle as input.
We can then utilise this 'box collider' by augmenting it with explicit logic to execute when a collision occurs.  
For instance, locking a particle in place.

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
This includes an alpha fade in/out as well as the current coordinates.  

At the same time we decrement the particles life by the delta time (the amount of time passed since the last tick) and update it's coordinates and velocity, 
by summing them with velocity and acceleration respectively.

The delta time is also used to adjust the values here.  
This attempts to keep the simulation at a constant speed across a varying speed of ticks (for example, a fast vs slow computer should see the particles change by the same amount).

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
            | true -> { p with TimeToLive = p.TimeToLive - delta }
            | false -> 
                { p with TimeToLive = p.TimeToLive - delta
                         Coords = { X = p.Velocity.X * delta
                                    Y = p.Velocity.Y * delta }
                                  |> sum p.Coords 
                         Velocity = { X = p.Acceleration.X * delta
                                      Y = p.Acceleration.Y * delta }
                                    |> sum p.Velocity 
                         Rotation = p.Rotation + (p.AngularVelocity * delta) }

(**
By bringing this all together we can finally define our update function.  
We make use of `List.map` to create a new list of particles from all the update functions, which is then set into a new state record for the animation to store.

A public port of this update function is created by declaring the respective member on the animation type.  
This is the how the we control how often the engine is updated from within the simulation loop.

*)

        let tick delta state = 
            let updatedState = tick delta state // Tick updates forces
            { updatedState with Particles = 
                                    updatedState.Particles
                                    |> List.rev
                                    |> spawnParticles (delta * spawnRate)
                                    |> List.map (fun p -> 
                                           calcAcceleration p
                                           |> applyColliders
                                           |> updateAlpha
                                           |> updatePosition delta) }

        member this.Update(delta) = 
            state <- tick delta {state with Elapsed = state.Elapsed + delta}
            state
(*** define: event-members ***)
        member this.RaiseMouseEvent status = status |> mouseEvent.Trigger
        member this.MouseEvent = mouseEvent.Publish :> IObservable<EngineEvent>

(**

Phew! That covered a lot of code in a pretty dense format so I hope you managed to keep up.  

The code above gives us pretty much all we need to start creating an actual animation.

##Creating an animation

In order to create an animation we need to tackle two parts.  

 - The creation of a UI, complete with some rendering logic.
 - The creation of a set of specific emitters, colliders, forces and particle types to implement the animation logic itself.
 
We will start by setting up an instance of our animation type to represent snow.
 
###It's Snowtime!
 
First up, we need to tackle our representation of a snowflake and create an emitter in order to initialise an instance of it.  

We make use of the box emitter we saw earlier to define an emitter that generates random starting coordinates from within the given bounds.
By defining the bounds to start above and to the left of the view port, we can make some of our snowflakes appear to be falling from above the screen.

*)
(*** hide ***)
module Mist =

    open Engine
    open Particle
    open System
    open Core

    let private rand = Random()
    let BoxEmitter min max = 
        fun () -> { X = float (rand.Next(int min.X, int max.X))
                    Y = float (rand.Next(int min.Y, int max.Y)) }

    let emitter = BoxEmitter {X = 0.0; Y= 50.0} {X = 1280.0; Y=100.0}
    let rand = Random() //TODO - Can we share?

    //Needs to rework alpha and life if possible
    let CreateMistPatch() = 
        let life = 3.0 + rand.NextDouble()
        { Mass = 1.0
          Img = ""
          Alpha = 0.4
          AlphaTarget = 0.4
          Acceleration = defaultVector
          Locked = false
          TimeToLive = life
          Life = life
          Coords = emitter()
          Velocity = { Radius = 50.0
                       Theta = rand.NextDouble() * Math.PI * 2.0 }
                       |> toCartesian
          Rotation = (Math.PI * 2.0 * rand.NextDouble()) * 180.0 / Math.PI
          AngularVelocity = (1.0 - rand.NextDouble() * 2.0) * 0.5
          Scale = rand.NextDouble() + 1.0 }

    let private tick delta state = state 

    let Animation = new Animation(0.05, 4, CreateMistPatch, tick, [], [])
(***)

(*** hide ***)
module Snow =
    open System
    open Engine
    open Particle

    let pointInBounds (min,max) point = 
        min.X < point.X && max.Y > point.Y && min.Y < point.Y && max.Y > point.Y

    let boxCollider min max particle =
        pointInBounds (min,max) particle.Coords

    let floorCollider particle = 
        {particle with Locked = boxCollider {X = 0.0; Y=670.0} {X = 1280.0; Y=720.0} particle}

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
 - life/TimeToLive: These values control how long the particle should live for.
 - Mass: Used in the acceleration calculation. We use a random value between 0.002 and 0.003.
 - Alpha/AlphaTarget - Control the alpha of the particle in the UI
 - Velocity: A vector representing the particles initial direction and speed
 - Acceleration: Starts at zero
 - Rotation: Starting particle rotation in degrees
 - AngularVelocity: How quickly the particle rotates around a fixed axis
 - Scale: A random scale between 0.5 and 1 to give the particles random size

*)

    let CreateSnowFlake () = 
        let life = 4.0 * (rand.NextDouble() * 2.0)
        { Coords = emitter ()
          Img = "SnowFlake.png"
          Mass = (rand.NextDouble() / 1000.0) + 0.001
          Alpha = 1.0
          AlphaTarget = 1.0
          Velocity = { Radius = 30.0; 
                       Theta = rand.NextDouble() * Math.PI * 2.0}
                       |> toCartesian
          AngularVelocity = 1.0
          Acceleration = defaultVector
          TimeToLive = life
          Life = life
          Rotation = (Math.PI * 2.0 * rand.NextDouble()) |> toDegrees
          Scale = rand.NextDouble() / 2.0 + 0.5 
          Locked = false} 

(**
Pretty straight forward!

Next up. Forces.  
We want our snow flakes to look as realistic as possible (while maintaining simplicity).  
We can go some way to achieving this by applying gravity and wind forces in order to give the snow flakes more dynamic movement.

Gravity is a widely adjusted value based upon that of the earth and wind starts as a gentle breeze. (The wind force will change later)

*)

    let gravity p = {X = 0.0; Y = 9.81 / 60.0}
    let wind p = {X = 0.5; Y = 0.0}

(**
Finally we need a tick (or update) function that can be called during iterations of the simulation loop.  
This tick function is called from within the main engine tick function and therefore only needs specific logic for this animation.

A perfect place to update the the forces in play. Especially the wind.

Particular notice should be given to how wind force is calculated.  
I have based each axis value on a sine wave (or rather half of it in the case of the X axis).
This creates a pleasing effect by slightly altering the strength and direction of the wind smoothly over time.


*)
    let private tick delta state =
        let wind = fun _ -> { X = (Math.Sin state.Elapsed * 0.5 + 0.5) / 10.0;
                              Y = Math.Sin(state.Elapsed * 0.5) / 10.0}
        { state with Forces = [gravity; wind]}


(*** define: snow-mouse-force ***)
    let mutable private mouseForce = fun _ -> {X = 0.0; Y = 0.0}

(**
We could also set the colliders here but as we only have our floor collider that we saw earlier, there is no need.

The last thing we need to do, is create the animation instance passing in the required parameters.  
*)
    let Animation = new Animation(10.0, 1000, CreateSnowFlake, tick, [gravity; wind], [floorCollider]) 

(*** define: handle-mouse-force ***)
    let private applyMouseForce = function
    | MouseEvent(LeftDown, currentPos) ->
        mouseForce <- exertForce currentPos 200.0 (fun d -> 1.0 / (1.0 + d))
    | MouseEvent(RightDown, currentPos) ->
        mouseForce <- exertForce currentPos -200.0 (fun d -> 1.0 / (1.0 + d))
    |_ -> mouseForce <- fun _ -> {X = 0.0; Y = 0.0}

    do
        Animation.MouseEvent
        |> Observable.subscribe applyMouseForce 
        |> ignore


(**
That's it! Our animation is now ready to be run.  
But hold on a minute! we're going to need some form of UI to show the results and that is where I turn to WPF.

##The Renderer

As I mentioned at the beginning of the post, WPF is really not a good choice for particle animation.  
However, it should hold up to the abuse we're about to throw at it, provided we allow for a degree of mutability and alter our approach ever so slightly in order to keep things smooth (As smooth as it can be!).

For the renderer implementation, We will make use of [FsXaml] and [FSharp.ViewModule].  
The reason for this is two fold. They are familiar; They greatly speed up development time.

I won't go too much into these projects here but I have other [posts] on my blog that explain some of the details.

So, where do we start?

The control loop is a good base point.

###The Animation Loop

An animation loop is a simple beast at it's core, but can become complex when striving to keep things running smoothly on machines of varying performance.  
Luckily, as we saw earlier, just by taking into account delta time, we go someway towards this goal without making things too complex.

Let's start by creating a View Model to house our animation control logic. Within which, we will create our loop.  
As we are using WPF, I found the easiest way to create a loop was to utilise a `DispatchTimer` as this plays nicely with WPFs own rendering engine.

*)

(*** hide ***)

open FSharp.ViewModule
open System.Windows
open System.Windows.Input
open System.Windows.Controls
open FsXaml
open Engine
open Particle
open Snow
open System.Collections.ObjectModel
open System
open System.Windows.Threading

module View =
(***)

(*** define: ParticleViewModel ***)
    type ParticleViewModel(x,y,scale,rotation,alpha) as this = 
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
(***)
    type MainViewModel() as this = 
        inherit EventViewModelBase<EngineEvent>() 
    
        let frameTimer = new DispatcherTimer()
(*** define: mouse-command ***)
        let mouseCommand = FunCommand((fun o ->
            let mEvent = o :?> EngineEvent
            Snow.Animation.RaiseMouseEvent mEvent), fun _ -> true) :> ICommand

(*** define: particle-list ***)
        let particles = ObservableCollection<ParticleViewModel>()
(**
We now need to attach an event handler to the 'Tick' of the timer.  
This is where we will update both the simulation and the UI.
*)

(*** define: update-gui ***)
        let updateParticleUI (collection: ObservableCollection<ParticleViewModel>) particles = 
            particles 
            |> List.rev
            |> List.iteri (fun i p-> 
                match i,p with
                | x,_ when i < collection.Count ->
                    collection.[i].X <- p.Coords.X
                    collection.[i].Y <- p.Coords.Y
                    collection.[i].Rotation <- p.Rotation
                    collection.[i].Alpha <- p.Alpha
                | _,_ ->
                    collection.Add(new ParticleViewModel(p.Coords.X, p.Coords.Y,p.Scale,p.Rotation, p.Alpha)))
(***)

        let onTick (_, elapsed) =
            let snowState = Snow.Animation.Update elapsed
            snowState.Particles |> updateParticleUI particles

(**

The onTick function takes a tuple comprised of the current tick time and the total elapsed time.  
All this tick function does is call update on the Snow animation and then update the UI to render the result.  

*You will have noticed the call to updateParticleUI above, we will get to that shortly.*

First let's look at how we attach this function to the loop.  
The constructor of our view model is a good place for this as it means the animation will auto start.  

To start with we pass the `frameTimer.Tick` IEvent to the scan function of the observable module.
This allows us to capture some state on each tick and is how we go about getting the current time and the elapsed time (in seconds), without needing to hold mutable variables outside the handlers.  
This is all done with the use of the current tick count. (For those that don't know, it is the time in milliseconds since the computer last started)

The returned observable function is then passed to `Observable.subscribe` and given our onTick function to call on each observation.  
Finally we set the tick interval so that it fires 60 times a second and start the timer.
*)

        do
            frameTimer.Tick
            |> Observable.scan (fun (previous, elapsed) _  -> 
                (float Environment.TickCount, (float Environment.TickCount - previous) / 1000.0)) 
                (float Environment.TickCount, float Environment.TickCount)
            |> Observable.subscribe onTick
            |> ignore
            frameTimer.Interval <- TimeSpan.FromSeconds(1.0 / 60.0);
            frameTimer.Start();

(*** define: particle-member ***)
        member x.Particles: ObservableCollection<ParticleViewModel> = particles
(*** define: mouse-command-member ***)
        member x.MouseCommand = mouseCommand
(**

###Handling UI Updates

We saw earlier that we need to update the UI on each tick.  
Before we can do this we need to create a UI to update!

So let's look at what's needed.

 - A Particle ViewModel. We use this to prevent adding mutability to the engines representation and to speed things up on the rendering side
 - An observable collection to hold our particles
 - A UI representation of a particle. This will be an image in our case representing the particle texture
 - A canvas or other UI component in order to display the particles

As usual we can look at each of these in turn.  

For the ViewModel I will make use of `FSharp.ViewModules` `BaseViewModel` class in order to take advantage of its implementation of `INotifyPropertyChanged`.  
This means my UI can bind straight onto its properties and update itself automatically on each tick. (One benefit of using WPF!)

*)

(*** include: ParticleViewModel ***)

(**
Our MainViewModel can then be given the relevant collection like so and expose it as a member that can be bound to from XAML.
*)

(*** include: particle-list ***)
(*** include: particle-member ***)
(**
This is then used during the onTick function we defined earlier, in the statement `snowState.Particles |> updateParticleUI particles`  

The `updateParticleUI` function iterates the given collection of particle records and either creates a new `ParticleViewModel` for the particle if it doesn't exist, or alters an existing ones state.

You will notice that I am relying on the particle lists index in order to locate the correct view model to update.  
This is a consequence of a much needed use of mutable data as we cannot rebuild the UI collection due to the massive performance hit that occurs.

*Note, we need to reverse the list as new particles are always added to start of the particles collection*

*)

(*** include: update-gui ***)    

(**

That looks great, onto the UI.

###The XAML View

In order to display the particles on the screen, we need something light weight in terms of child positioning and that can hold a collection of elements natively.  
One solution is to create an items control and set its panel to be a canvas element.  

This will then allow us to define a simple data template and accompanying item style in order to display the particles just as we want.

Below is the important section of XAML. The rest can be viewed on [GitHub] if interested.

{% highlight xml %}
<ItemsControl ItemsSource="{Binding Particles}">
    <ItemsControl.ItemsPanel>
        <ItemsPanelTemplate>
            <Canvas xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" 
            x:Name="Canvas" Grid.Row="0" Width="1280" Height="720" Canvas.Left="0" Canvas.Top="0" Margin="0,0,0,0" Background="Transparent" />
        </ItemsPanelTemplate>
    </ItemsControl.ItemsPanel>
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <Image Source="SnowFlake.png" Width="8" Height="8">
                <Image.RenderTransform>
                    <TransformGroup>
                        <ScaleTransform ScaleX="{Binding Scale}" ScaleY="{Binding Scale}"  />
                        <RotateTransform Angle="{Binding Rotation}" />
                    </TransformGroup>
                </Image.RenderTransform>
            </Image>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
    <ItemsControl.ItemContainerStyle>
        <Style>
            <Setter Property="Canvas.Left" Value="{Binding Path=X}" />
            <Setter Property="Canvas.Top" Value="{Binding Path=Y}" />
            <Setter Property="Image.Opacity" Value="{Binding Alpha}" />
        </Style>
    </ItemsControl.ItemContainerStyle>
</ItemsControl>
{% endhighlight %}

As you can see, the above XAML contains bindings to set the `Canvas.Left` and `Canvas.Right` props, along with opacity, scale and rotation, with the latter two inside a render transform on the image element.

This is all we needed to accomplish. The animation should now be in a state to run successfully and render us a happy little snow scene!
But we can go further. Why not have another type of particle animating at the same time? And what about some user interaction?

##Running Two Animations

Running two animations is an easy task.  
All we need is to set up another animation in exactly the same way as we did for the Snow.  

That is, implement any emitters, colliders, forces and update logic that are required and instantiate a new Animation using them.  
The `MainViewModel` will also need a slight change to make use of them.

It will require two particle collections and needs to ensure both animations get updated every tick.
*)
(*** hide ***)
    let updateParticleUI (collection: ObservableCollection<ParticleViewModel>) particles = 
            particles 
            |> List.rev
            |> List.iteri (fun i p-> 
                match i,p with
                | x,_ when i < collection.Count ->
                    collection.[i].X <- p.Coords.X
                    collection.[i].Y <- p.Coords.Y
                    collection.[i].Rotation <- p.Rotation
                    collection.[i].Alpha <- p.Alpha
                | _,_ ->
                    collection.Add(new ParticleViewModel(p.Coords.X, p.Coords.Y,p.Scale,p.Rotation, p.Alpha)))

    

(***)

    let particles = ObservableCollection<ParticleViewModel>() 
    let mistParticles = ObservableCollection<ParticleViewModel>() 

    let onTick (_, elapsed) =
        let snowState = Snow.Animation.Update elapsed
        let mistState = Mist.Animation.Update elapsed 
        snowState.Particles |> updateParticleUI particles
        mistState.Particles |> updateParticleUI mistParticles

(**
The definition of my second animation is incredibly similar to the first.  
It differs in the fact is has no specific update logic, has no forces or colliders and is therefore much simpler.

With no colliders or forces, it means the mist particles simply drift across the screen in their initial direction and speed. Perfect!
I've omitted the code for brevity so check it out on [GitHub](https://github.com/sjpemberton/FsSnowGlobe/blob/master/ParticleEngine/Mist.fs) if you are intrigued.

To handle the rendering, we will create another `ItemsControl` with a canvas panel in the UI and overlap the first with it.  
That's it, as simple as that!

I won't paste the XAML in here as it is almost identical. Feel free to check it on GitHub [here] if you want.

##User Interaction - Events

Now for the best part. Some user interaction.  

For this we need to accomplish a few things.

 - Propagate events from the UI into the engine
 - Pass these events to the relevant animations
 - Allow the event handlers to have a causal effect on the particles.

Luckily for us, FsXaml provides us with some handy event to command and event converter functionality that we can utilise in order to capture UI events and transform them into `EngineEvent` records.
The code for an `EngineEvent` is straight forward, especially as we currently only care about mouse events.  
Other cases could be added to this union as required.

*)

(*** include: engine-event ***)

(**

Next up, we create the usage of this event on the Animation type.

*)

(*** include: events ***)

(**
And finally publish the event to the outside world, along with a convenient raise event member.  
These will be our port into the events from the UI code.

*)

(*** include: event-members ***)

(**

That is one half of the problem solved, now let's look at how we bind to them from the UI.  
To do this, we will be making use of the event converters from FsXaml.  

The basic premise here is to define a function that takes a system `MousEventArgs` as a parameter and converts it to an instance of our `EngineEvent`.  
Of course, if our `EngineEvent` union had more cases we would likely need multiple converters to convert from different system event types, from different handlers.

*)
(*** hide ***)
    module EventConverters = 
        open System.Windows.Input
        open FsXaml
        open Engine
        open Particle
        open System.Windows
(***)

        let mouseConverter (args : MouseEventArgs) = 
            let status = 
                if args.LeftButton = MouseButtonState.Pressed then LeftDown
                elif args.RightButton = MouseButtonState.Pressed then RightDown
                else Released
        
            let pt = args.GetPosition(args.OriginalSource :?> IInputElement)
            MouseEvent(status, { X = pt.X; Y = pt.Y })

// The converter to convert from MouseEventArgs -> EngineEvent
    type MouseConverter() = 
        inherit EventArgsConverter<MouseEventArgs, EngineEvent>(EventConverters.mouseConverter, MouseEvent(Released, { X = 0.0; Y = 0.0 }))

(**

In order to utilise this converter we need to add it as a resource to the XAML of our view and then use FsXamls `EventToCommand` utility in order to bind to a command on our `MainViewModel`.
  
This is done using the `System.Windows.Interactivity` helpers by specifying a trigger on the `MouseMove` event of the containing Grid element, like so.

{% highlight xml %}
<Window.Resources>
    <events:MouseConverter x:Key="mouseConverter" />
</Window.Resources>
<Grid>
    <i:Interaction.Triggers>
        <i:EventTrigger EventName="MouseMove">
            <fsxaml:EventToCommand Command="{Binding MouseCommand}" EventArgsConverter="{StaticResource mouseConverter}" />
        </i:EventTrigger>
    </i:Interaction.Triggers>
    ...
    {% endhighlight %}

The arguments for the command flow from the event, through the converter, and to the command.  
Hence, we can create our command on the view model to utilise the `EngineEvent`.  

In fact, I have cheated slightly and simply passed the EngineEvent straight from the view model to the snow animation (As this is the animation I want to handle the event in).

*)

(*** include: mouse-command ***)
(*** include: mouse-command-member ***)

(**

Almost there!

The last, but not least task to complete is the actual handling of the `EngineEvent`.  

For my mouse events I want to have different forces applied to the snow particles depending on what button was held down during the moving of the mouse.

 - If the left button is pressed, the particles should be repelled away from the cursors origin
 - If the right button is pressed, the particles are pulled towards the cursor.
 - When both buttons are released the force is removed.

That sounds simple enough. We just need to pattern match over our `EngineEvent` and assign a new event to a mutable variable.

We therefore add the following to our snow animation module.
*)
(**
The mutable binding.
*)
(*** include: snow-mouse-force ***)
(**
The event handler and event subscription.
*)
(*** include: handle-mouse-force ***)
(**

*Keen readers will be wondering what the `exertForce` function does, so here it is*

*)

(*** include: exert-force ***)
(**
It takes an epicentre for the force, strength, a decay function and a vector.  
It then calculates how much force to apply to the vector based on the distance form the epicentre and the decay function. The result is particles closer to the epicentre have more force exerted on them than those farther away.

That's it, everything is in place.  
The .gif below shows the interactivity in action.

<video autoplay controls loop poster="/content/images/post-images/fsAdventInteractive.jpg" src="/content/images/post-images/fsAdventInteractive.webm">
Sorry, your browser doesn't support HTML5 video!
Don't worry you can download the video <a href="http://stevenpemberton.net/content/images/post-images/fsAdventInteractive.webm">here</a> and view it in a video player.
Alternatively, you can view <a href="http://stevenpemberton.net/content/images/post-images/fsAdventInteractive.gif">this .gif</a> instead.
</video>

##Conclusion

I hope you enjoyed this simple animation.  
It was fun to create and the simplicity of F# made for a relatively quick and painless experience.

I would love to see how it performs when WPF is not being used as the render and I am more than aware of many places where efficiency could be improved in the engine code.

Likewise a much better option would have been to use OpenGL or DirectX for the rendering but the learning curve would have likely been to steep for the time I had. (Possible follow on post in the new year?)

As usual, all the code is available on [GitHub] so feel free to check it out!

Thanks again to [Sergey Tihon] for organising this amazing event.

*)


(**
[F# advent]:https://sergeytihon.wordpress.com/2015/10/25/f-advent-calendar-in-english-2015/
[here]:https://github.com/sjpemberton/FsSnowGlobe/blob/master/FsSnowGlobe/MainWindow.xaml
[GitHub]:https://github.com/sjpemberton/FsSnowGlobe/
[FSharp.ViewModule]:https://github.com/fsprojects/FSharp.ViewModule
[FsXaml]:https://github.com/fsprojects/FsXaml
[posts]:http://stevenpemberton.net/archive/tags/WPF.html
[Sergey Tihon]:https://sergeytihon.wordpress.com/tag/fsadvent/
*)

(*** hide ***)
---
layout: post
title: Let It Snow! - F# Advent 2015
date: 19/12/2015
comments: true
tags: ["fsharp","WPF",]
catagories: ["guides","examples"]
meta: A basic particle system in FSharp and WPF for FSharp Advent 2015
---