module DroneOps.Tests.RouteEngineTests

open Xunit
open DroneOps.Domain.Position
open DroneOps.Domain.Route
open DroneOps.Domain.World
open DroneOps.Simulation.RouteEngine

// ─── Helpers ──────────────────────────────────────────────────

let private emptyMap =
    WorldMap.create 10 10 |> Result.defaultWith (fun _ -> failwith "invalid map")

let private pos x y = { X = x; Y = y }

// ─── Basic routing ────────────────────────────────────────────

[<Fact>]
let ``planRoute finds path between adjacent cells`` () =
    let result = planRoute emptyMap (pos 0 0) (pos 0 1)
    Assert.True(Result.isOk result)

[<Fact>]
let ``planRoute path consists of adjacent steps only`` () =
    let route =
        planRoute emptyMap (pos 0 0) (pos 3 3)
        |> Result.defaultWith (fun _ -> failwith "no path")

    Route.steps route
    |> List.pairwise
    |> List.iter (fun (a, b) -> Assert.True(Position.isAdjacent a b, $"steps {a} and {b} are not adjacent"))

[<Fact>]
let ``planRoute last step is target`` () =
    let target = pos 4 4

    let route =
        planRoute emptyMap (pos 0 0) target
        |> Result.defaultWith (fun _ -> failwith "no path")

    Assert.Equal(target, Route.destination route)

[<Fact>]
let ``planRoute start equals target returns single step route`` () =
    let p = pos 2 2

    let route =
        planRoute emptyMap p p |> Result.defaultWith (fun _ -> failwith "no path")

    Assert.Equal(1, Route.length route)

// ─── Obstacle handling ────────────────────────────────────────

[<Fact>]
let ``planRoute returns NoPathFound when fully blocked`` () =
    let blockedMap =
        [ for y in 0..9 -> pos 1 y ]
        |> List.fold (fun m p -> WorldMap.addObstacle p m) emptyMap

    Assert.Equal(Error NoPathFound, planRoute blockedMap (pos 0 0) (pos 2 0))

[<Fact>]
let ``planRoute routes around single obstacle`` () =
    let obstacle = pos 1 0
    let world = emptyMap |> WorldMap.addObstacle obstacle

    let route =
        planRoute world (pos 0 0) (pos 2 0)
        |> Result.defaultWith (fun _ -> failwith "no path")

    Assert.DoesNotContain(obstacle, Route.steps route)

// ─── Boundary validation ──────────────────────────────────────

[<Fact>]
let ``planRoute returns StartOutsideMap for out of bounds start`` () =
    Assert.Equal(Error StartOutsideMap, planRoute emptyMap (pos -1 0) (pos 0 0))

[<Fact>]
let ``planRoute returns TargetOutsideMap for out of bounds target`` () =
    Assert.Equal(Error TargetOutsideMap, planRoute emptyMap (pos 0 0) (pos 99 99))

[<Fact>]
let ``planRoute returns StartBlocked when start is obstacle`` () =
    let world = emptyMap |> WorldMap.addObstacle (pos 0 0)
    Assert.Equal(Error StartBlocked, planRoute world (pos 0 0) (pos 2 2))

[<Fact>]
let ``planRoute returns TargetBlocked when target is obstacle`` () =
    let world = emptyMap |> WorldMap.addObstacle (pos 5 5)
    Assert.Equal(Error TargetBlocked, planRoute world (pos 0 0) (pos 5 5))
