module DroneOps.Simulation.RouteEngine

open DroneOps.Domain.Position
open DroneOps.Domain.Route
open DroneOps.Domain.World

// ─── Errors ───────────────────────────────────────────────────

type RoutePlanningError =
    | StartOutsideMap
    | TargetOutsideMap
    | StartBlocked
    | TargetBlocked
    | NoPathFound

// ─── BFS ──────────────────────────────────────────────────────

// BFS на grid-map гарантирует кратчайший путь.
// Для карты 20x20 производительность достаточна.
// A* можно добавить позже если карта вырастет.

// Восстанавливаем путь от цели к старту по карте предшественников,
// затем разворачиваем — получаем путь от старта к цели.
let private reconstructPath (cameFrom: Map<Position, Position>) (start: Position) (target: Position) : Position list =

    let rec loop current acc =
        if current = start then
            acc
        else
            match Map.tryFind current cameFrom with
            | None -> acc
            | Some prev -> loop prev (current :: acc)

    loop target []

let private bfs (world: WorldMap) (start: Position) (target: Position) =
    let queue = System.Collections.Generic.Queue<Position>()
    let visited = System.Collections.Generic.HashSet<Position>()
    let cameFrom = System.Collections.Generic.Dictionary<Position, Position>()

    queue.Enqueue(start)
    visited.Add(start) |> ignore

    let mutable found = false

    while queue.Count > 0 && not found do
        let current = queue.Dequeue()

        if current = target then
            found <- true
        else
            for neighbor in WorldMap.passableNeighbors current world do
                if not (visited.Contains(neighbor)) then
                    visited.Add(neighbor) |> ignore
                    cameFrom[neighbor] <- current
                    queue.Enqueue(neighbor)

    if found then
        reconstructPath (cameFrom |> Seq.map (fun kv -> kv.Key, kv.Value) |> Map.ofSeq) start target
        |> Some

    else
        None

// ─── Public API ───────────────────────────────────────────────

let planRoute (world: WorldMap) (start: Position) (target: Position) =
    if not (WorldMap.isWithinBounds start world) then
        Error StartOutsideMap
    elif not (WorldMap.isWithinBounds target world) then
        Error TargetOutsideMap
    elif WorldMap.isObstacle start world then
        Error StartBlocked
    elif WorldMap.isObstacle target world then
        Error TargetBlocked
    else if
        // Старт = цель — маршрут из одной точки.
        start = target
    then
        Route.create [ target ] |> Result.mapError (fun _ -> NoPathFound)
    else
        match bfs world start target with
        | None -> Error NoPathFound
        | Some steps -> Route.create steps |> Result.mapError (fun _ -> NoPathFound)
