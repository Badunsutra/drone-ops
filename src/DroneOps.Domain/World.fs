module DroneOps.Domain.World

open Identifiers
open Position

// ─── Charging station ─────────────────────────────────────────

// Зарядная станция — часть статичной карты.
// Position хранится здесь чтобы не делать
// отдельный lookup при построении маршрута к станции.
type ChargingStation =
    { Id: ChargingStationId
      Position: Position }

// ─── World map ────────────────────────────────────────────────

// WorldMap статичен в MVP — создаётся один раз при старте,
// не меняется в течение симуляции.
//
// Obstacles — Set для O(log n) проверки при поиске пути.
// ChargingStations — Map для O(log n) lookup по Id.
type WorldMap =
    { Width: int
      Height: int
      Obstacles: Set<Position>
      ChargingStations: Map<ChargingStationId, ChargingStation> }

module WorldMap =

    let create width height =
        if width > 0 && height > 0 then
            Ok
                { Width = width
                  Height = height
                  Obstacles = Set.empty
                  ChargingStations = Map.empty }
        else
            Error $"Map dimensions must be positive, got: {width}x{height}"

    // ─── Stations ─────────────────────────────────────────────

    let addStation station world =
        { world with
            ChargingStations = world.ChargingStations |> Map.add station.Id station }

    let tryFindStation id world =
        world.ChargingStations |> Map.tryFind id

    let stationPosition id world =
        world |> tryFindStation id |> Option.map _.Position

    // ─── Obstacles ────────────────────────────────────────────

    let addObstacle pos world =
        { world with
            Obstacles = world.Obstacles |> Set.add pos }

    let isObstacle pos world = world.Obstacles |> Set.contains pos

    // ─── Validation ───────────────────────────────────────────

    let isWithinBounds pos world =
        Position.isWithinBounds world.Width world.Height pos

    // Позиция доступна для движения —
    // в границах карты и не является препятствием.
    let isPassable pos world =
        isWithinBounds pos world && not (isObstacle pos world)

    // Соседние клетки доступные для движения.
    // RouteEngine будет использовать это при поиске пути.
    let passableNeighbors pos world =
        Position.neighbors pos |> List.filter (fun p -> isPassable p world)
