﻿module Sequential

open ArithmeticLogicUnit
open Utils

type clk =
    | Tick = 0s
    | Tock = 1s
    
let flip = function
    | clk.Tick -> clk.Tock
    | _ -> clk.Tick

[<AbstractClass>]
type Chip() =
    member val outputs : int16 array = Array.empty with get, set
    abstract member doWork: clk -> int16 array -> int16 array
    member x.execute clk inputs = 
        let outcome = x.doWork clk inputs
        x.outputs <- outcome
        outcome

type DFF() =
    inherit Chip()
    let mutable state = 0s
    let mutable pState = 0s
    override x.doWork clk inputs =
        match clk with //Only set the state on a tick - The falling edge
        | clk.Tick -> state <- pState
        | _ -> pState <- inputs.[0]
        [|state|]

//The DFF (Data Flip Flop)
//I have skipped building this chip from combinatorial chips as it is long winded.

//The premise is that the chip returns in(t-1); That is, the input value from the previous clock cycle.
//A clock cycle is from the beginning of the tick, to the end of a tock.
//The DFF therefore accepts its state on a the tock, but does not expose this on the output pin until the following tick.

//The DFF inherently no needs some form of state, as we need to pass the previous clk cycles value out of the function.
//type DFF() = 
//    let mutable state = 0s
//    member x.execute d clk = 
//        let pState = state
//        match clk with //Only set the state on a tock 
//        | true -> state <- d
//        | _ -> ()
//        pState



//The Set-Reset (SR) Latch - The inputs can be thought of as negated (they need to be set to false to take affect!)
//The 2 output vars (The latch state) are Q and !Q - It is assumed !Q should always be the inverse of Q
//Setting both S and R to false will cause issues with this logic and Q and !Q will be equal!
//As we cannot have a cyclic chip we need to model it with implicit state - This is not technically correct but gets the job done!
//Effectively the state represents the continuous current
//This implementation has inherit propagation delay for an entire cycle but misses the subtlety of the tick-tock 
//type SRLatch() =
//    let mutable state = (false,false)
//    member x.execute s r = 
//        state <- (Nand s (snd state),
//                  Nand (fst state) r)
//        state

//We also mock the sequential nature of the NAND chips - One NAND will always win in the real world.
type SRLatch() = 
    inherit Chip()
    let mutable state = [|0s; 0s|]
    override x.doWork clk inputs =
        state <- [|Nand inputs.[0] state.[1];
                   Nand inputs.[1] state.[0]|]
        state

//Adding the clk into the latch allows us to control when the state is set (Ie -only when the clock is high (true))
type ClockedSRLatch() =
    inherit Chip()
    let srLatch = SRLatch()
    override x.doWork clk inputs =
        let s,r,clk2 = (inputs.[0], inputs.[1], clk |> int16 )
        [|Nand s clk2; Nand clk2 r|] |> srLatch.execute clk

        
//A master - slave latch configuration
//This adds a delay to the setting of the slave state, allowing the chip to have the entire clock cycle to settle into it's state.
type RsFlipFlop() =
    inherit Chip()
    let master = new ClockedSRLatch()
    let slave = new ClockedSRLatch()
    override x.doWork clk inputs = 
        inputs
        |> master.execute clk
        |> slave.execute (clk |> flip) 

//Clocked D latch is simply an SR latch with only one input.
//The S input is negated to supply the R input
type ClockedDLatch() =
    inherit Chip()
    let latch = new ClockedSRLatch()
    override x.doWork clk inputs =
         [|inputs.[0]; (Not inputs.[0])|]
         |> latch.execute clk

//The DFF
//It is just an RS Flip Flop with a single input negated to supply both 
//This could also be made form a clockedDLatch and an RS latch instead
type DFlipFlop() =
    inherit Chip()
    let ff = new RsFlipFlop()
    override x.doWork clk inputs =
         [|inputs.[0]; (Not inputs.[0])|]
         |> ff.execute clk

//TODO - Need to make standard interface for state holding chips.

//Stores a single bit.
//The Mux chip acts as a selector on whether to store a new value or keep hold of the old DFF value
type Bit() =
    inherit Chip()
    let dff = new DFF()
    let mutable state = 0s
    override x.doWork clk inputs =
        state <-([|Mux inputs.[0] state inputs.[1]|]
                |> dff.execute clk).[0]
        [|state|]

//A 16 bit register 
type Register() = 
    inherit Chip()
    let bits = [|for i in 1 .. 16 -> new Bit()|]
    override x.doWork clk inputs =
        let inBits = inputs.[0] |> toTwosCompliment 16
        bits |> Array.mapi (fun i b -> ([|inputs.[1]; inBits.[i]|] 
                                        |> b.execute clk).[0])
        
        


//An 16 bit wide 8 bit size, register array.
//Utilises Mux and DMux to select the correct register to store the value in
type RAM8() =
    inherit Chip()
    let registers = [|for i in 1 .. 8 -> new Register()|]
    override x.doWork clk inputs =
        let (inBits, load, address) = (inputs.[0], inputs.[1], inputs.[2] |> toTwosCompliment 3)
        let loadArray = DMux8Way load address
        let state = registers 
                    |> Array.mapi (fun i r -> [|inBits; loadArray.[i]|]
                                              |> r.execute clk)
        Mux8Way16 state.[0] state.[1] state.[2] state.[3] state.[4] state.[5] state.[6] state.[7] address

type RAM64() =
    inherit Chip()
    let ramArray = [|for i in 1 .. 8 -> new RAM8()|]
    override x.doWork clk inputs =
        let (inBits, load, address) = (inputs.[0], inputs.[1], inputs.[2] |> toTwosCompliment 6)
        let ramLoad = DMux8Way load address.[0..2]
        let state = ramArray |> Array.mapi (fun i r -> r.execute clk [|inBits; ramLoad.[i]; (toDecimal 16 address.[3..5]) |])
        Mux8Way16 state.[0] state.[1] state.[2] state.[3] state.[4] state.[5] state.[6] state.[7] address

//Beginning to see the pattern.......

//type RAM512() =
//    inherit Chip()
//    let ramArray = [|for i in 1 .. 8 -> new RAM64()|]
//    override x.doWork clk inputs =
//        let (inBits, load, address) = (inputs.[0], inputs.[1], inputs.[2] |> toTwosCompliment 9)
//        let ramLoad = DMux8Way load address.[0..2]
//        let state = ramArray |> Array.mapi (fun i r -> r.execute clk [|inBits; ramLoad.[i]; (toDecimal 16 address.[3..8]) |])
//        Mux8Way16 state.[0] state.[1] state.[2] state.[3] state.[4] state.[5] state.[6] state.[7] address
//
//type RAM4k() =
//    inherit Chip()
//    let ramArray = [|for i in 1 .. 8 -> new RAM512()|]
//    override x.doWork clk inputs =
//        let (inBits, load, address) = (inputs.[0], inputs.[1], inputs.[2] |> toTwosCompliment 12)
//        let ramLoad = DMux8Way load address.[0..2]
//        let state = ramArray |> Array.mapi (fun i r -> r.execute clk [|inBits; ramLoad.[i]; (toDecimal 16 address.[3..11]) |])
//        Mux8Way16 state.[0] state.[1] state.[2] state.[3] state.[4] state.[5] state.[6] state.[7] address
//
//type RAM16k() =
//    inherit Chip()
//    let ramArray = [|for i in 1 .. 4 -> new RAM4k()|]
//    override x.doWork clk inputs =
//        let (inBits, load, address) = (inputs.[0], inputs.[1], inputs.[2] |> toTwosCompliment 16)
//        let ramLoad = DMux8Way load address.[0..2]
//        let state = ramArray |> Array.mapi (fun i r -> r.execute clk [|inBits; ramLoad.[i]; (toDecimal 16 address.[3..13]) |])
//        Mux8Way16 state.[0] state.[1] state.[2] state.[3] state.[4] state.[5] state.[6] state.[7] address

type Counter() = 
    inherit Chip()
    let register = new Register()
    override x.doWork clk inputs = 
        let (inBits, load, inc, reset) = (inputs.[0], inputs.[1], inputs.[2], inputs.[3])
        let current = register.execute clk [|0s; 0s|]
        let next = Increment (current) //Increment the current value
        let mux1 = MultiMux load next (inBits |> toTwosCompliment 16) 
        let mux2 = MultiMux reset mux1 [|for i in 1..16 -> 0s|]
        let regLoad = Or inc load
                      |> Or reset
        register.execute clk [| mux2 |> toDecimal 16; regLoad|]
        
        
type CounterPM() =
    inherit Chip()
    let register = new Register()
    override x.doWork clk inputs =
        let (inBits, load, inc, reset) = (inputs.[0], inputs.[1], inputs.[2], inputs.[3])
        let current = register.execute clk [|0s; 0s|]
        let toSet = 
            match reset, load, inc with
            | 1s,_,_ -> [|for i in 1..16 -> 0s|]
            | 0s,1s,_ -> inBits |> toTwosCompliment 16
            | 0s,0s,1s -> Increment (current)
            |_,_,_ -> register.execute clk [|inBits; 0s|]
        register.execute clk [|toSet |> toDecimal 16; (load ||| reset ||| inc)|]


//Generic implementation!

type RamSize =
    | Bit8 
    | Bit64 
    | Bit512
    | KB4 
    | KB16

let getSize = function 
    | Bit8   -> 8
    | Bit64  -> 64
    | Bit512 -> 512
    | KB4    -> 4096
    | KB16   -> 16384
    
    
//More F# approach
type RAM(size) = 
    inherit Chip()
    let regArray = [|for i in 1 .. size |> getSize -> new Register()|]
    override x.doWork clk inputs =
        let (inBits, load, address) = (inputs.[0],inputs.[1],inputs.[2])
        regArray.[int address].execute clk [|inBits; load|] //Need to handle index out of range

    
type TestHarness = 
    { inputs : int16 array
      outputs : int16 array
      chips : Chip array }

let setInputs i harness = 
    {harness with inputs = i;}

let executeChips harness clk =
    harness.chips |> Array.fold (fun state (chip: Chip) -> chip.execute clk state) harness.inputs

let rec iterate i clk state = 
    match i with
    | 0 -> state
    | _ -> 
        let result = { state with outputs = executeChips state clk }
        iterate (i - 1) clk result

let cycle iterations clkIters harness = 
    printfn "Executing %i cycles with inputs = %A" iterations harness.inputs
    let rec doCycle i clk harness =
        match i with
        | 0 -> harness
        | _ ->
            let result = iterate clkIters clk harness
            printfn "   Cycle %i - clk: %A - outputs: %A" (iterations - i + 1) clk result.outputs 
            match clk with
                | clk.Tick -> doCycle i (flip clk) result
                | _ -> doCycle (i-1) (flip clk) result
    doCycle iterations clk.Tick harness


