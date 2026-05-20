module DroneOps.Simulation.Movement

open DroneOps.Domain.Battery
open DroneOps.Domain.Drone
open DroneOps.Domain.Position
open DroneOps.Domain.Route

// ─── Result ───────────────────────────────────────────────────

type MovementResult =
    { Position: Position
      RemainingRoute: Route option
      ReachedWaypoint: Position option
      RouteCompleted: bool }

// ─── Movement ─────────────────────────────────────────────────

// Один шаг — чистое потребление маршрута без логики батареи.
// Батарея считается только в moveTick.
let moveOneStep (route: Route) =
    match Route.tryNext route with
    | None -> Error RouteNotFound

    | Some(nextPos, remaining) ->
        let routeCompleted =
            match Route.tryNext remaining with
            | None -> true
            | Some _ -> false

        Ok
            { Position = nextPos
              RemainingRoute = if routeCompleted then None else Some remaining
              ReachedWaypoint = if routeCompleted then Some nextPos else None
              RouteCompleted = routeCompleted }

// Несколько шагов за один тик.
// Позиция передаётся через цепочку результатов —
// начальная позиция дрона не нужна.
let moveTick (route: Route) (battery: BatteryLevel) (config: DroneConfig) =

    let rec loop remaining bat stepsLeft =
        match moveOneStep remaining with
        | Error e -> Error e
        | Ok result ->
            let newBattery = BatteryLevel.drain config.BatteryDrainPerStep bat
            let lastStep = stepsLeft = 1
            let batteryEmpty = BatteryLevel.value newBattery = 0

            if result.RouteCompleted || lastStep || batteryEmpty then
                Ok result
            else
                match result.RemainingRoute with
                | None -> Ok result
                | Some next -> loop next newBattery (stepsLeft - 1)

    loop route battery config.StepsPerTick
