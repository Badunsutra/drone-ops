module DroneOps.Tests.MovementTests

open Xunit
open DroneOps.Domain.Battery
open DroneOps.Domain.Drone
open DroneOps.Domain.Position
open DroneOps.Domain.Route
open DroneOps.Simulation.Movement

// ─── Helpers ──────────────────────────────────────────────────

let private pos x y = { X = x; Y = y }

let private makeRoute positions =
    Route.create positions |> Result.defaultWith (fun _ -> failwith "invalid route")

let private defaultConfig =
    { StepsPerTick = 1
      BatteryDrainPerStep = 5
      BatteryChargePerTick = 10
      LowBatteryThreshold = BatteryLevel.unsafeCreate 20
      MaxPayloadKg = None }

// ─── moveOneStep ──────────────────────────────────────────────

[<Fact>]
let ``moveOneStep moves to first position in route`` () =
    let route = makeRoute [ pos 1 0; pos 2 0; pos 3 0 ]

    match moveOneStep route with
    | Error e -> Assert.Fail $"Expected Ok, got: {e}"
    | Ok r -> Assert.Equal(pos 1 0, r.Position)

[<Fact>]
let ``moveOneStep on single step route completes route`` () =
    let route = makeRoute [ pos 1 0 ]

    match moveOneStep route with
    | Error e -> Assert.Fail $"Expected Ok, got: {e}"
    | Ok r ->
        Assert.True(r.RouteCompleted)
        Assert.Equal(Some(pos 1 0), r.ReachedWaypoint)
        Assert.Equal(None, r.RemainingRoute)

[<Fact>]
let ``moveOneStep does not complete route when steps remain`` () =
    let route = makeRoute [ pos 1 0; pos 2 0 ]

    match moveOneStep route with
    | Error e -> Assert.Fail $"Expected Ok, got: {e}"
    | Ok r ->
        Assert.False(r.RouteCompleted)
        Assert.Equal(None, r.ReachedWaypoint)
        Assert.True(r.RemainingRoute.IsSome)

// ─── moveTick ─────────────────────────────────────────────────

[<Fact>]
let ``moveTick StepsPerTick=1 moves one step`` () =
    let route = makeRoute [ pos 1 0; pos 2 0; pos 3 0 ]

    match moveTick route BatteryLevel.full defaultConfig with
    | Error e -> Assert.Fail $"Expected Ok, got: {e}"
    | Ok r -> Assert.Equal(pos 1 0, r.Position)

[<Fact>]
let ``moveTick StepsPerTick=2 moves two steps`` () =
    let config = { defaultConfig with StepsPerTick = 2 }
    let route = makeRoute [ pos 1 0; pos 2 0; pos 3 0 ]

    match moveTick route BatteryLevel.full config with
    | Error e -> Assert.Fail $"Expected Ok, got: {e}"
    | Ok r -> Assert.Equal(pos 2 0, r.Position)

[<Fact>]
let ``moveTick stops early when route completed`` () =
    let config = { defaultConfig with StepsPerTick = 5 }
    let route = makeRoute [ pos 1 0; pos 2 0 ]

    match moveTick route BatteryLevel.full config with
    | Error e -> Assert.Fail $"Expected Ok, got: {e}"
    | Ok r ->
        Assert.True(r.RouteCompleted)
        Assert.Equal(pos 2 0, r.Position)

[<Fact>]
let ``moveTick stops when battery depleted`` () =
    let config =
        { defaultConfig with
            StepsPerTick = 5
            BatteryDrainPerStep = 50 }

    let battery = BatteryLevel.unsafeCreate 50
    let route = makeRoute [ pos 1 0; pos 2 0; pos 3 0; pos 4 0 ]

    match moveTick route battery config with
    | Error e -> Assert.Fail $"Expected Ok, got: {e}"
    | Ok r -> Assert.Equal(pos 1 0, r.Position)
