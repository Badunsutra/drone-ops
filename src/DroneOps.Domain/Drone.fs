module DroneOps.Domain.Drone

open Identifiers
open Position
open Battery
open Route

// ─── Failure reasons ──────────────────────────────────────────

// Типизированные причины отказа вместо "Failed of string".
// Каждый случай несёт свои данные — столкновение знает позицию,
// ручная отмена знает оператора.
type DroneFailureReason =
    | BatteryDepleted
    | ConnectionLost
    | ObstacleCollision of position: Position
    | RouteNotFound
    | ManualAbort of operator: OperatorId

// ─── Status ───────────────────────────────────────────────────

// DroneStatus — state machine дрона.
// Каждый переход будет явно описан в DroneActor.
//
// ReturningToCharge None     — решение принято, станция ещё не назначена
// ReturningToCharge (Some id) — станция назначена, маршрут строится/выполняется
type DroneStatus =
    | Idle
    | MovingToPickup of missionId: MissionId
    | MovingToDropoff of missionId: MissionId
    | ReturningToCharge of stationId: ChargingStationId option
    | Charging of stationId: ChargingStationId
    | Failed of reason: DroneFailureReason

// ─── Config ───────────────────────────────────────────────────

// Неизменяемая конфигурация дрона — задаётся при spawn,
// не меняется в течение жизни актора.
type DroneConfig =
    { StepsPerTick: int
      BatteryDrainPerStep: int
      BatteryChargePerTick: int
      LowBatteryThreshold: BatteryLevel
      MaxPayloadKg: decimal option }

// ─── State ────────────────────────────────────────────────────

// Полное состояние дрона — то что хранит DroneActor.
// DroneId и Config не меняются после создания,
// остальные поля обновляются на каждом tick.
type DroneState =
    { Id: DroneId
      Position: Position
      Battery: BatteryLevel
      Status: DroneStatus
      CurrentMission: MissionId option
      CurrentRoute: Route option
      Config: DroneConfig }

module DroneState =

    let create id position battery config =
        { Id = id
          Position = position
          Battery = battery
          Status = Idle
          CurrentMission = None
          CurrentRoute = None
          Config = config }

    // Часто используемые проверки статуса —
    // избегаем pattern matching везде в акторе.
    let isIdle state =
        match state.Status with
        | Idle -> true
        | _ -> false

    let isFailed state =
        match state.Status with
        | Failed _ -> true
        | _ -> false

    let needsCharging state =
        BatteryLevel.isLow state.Config.LowBatteryThreshold state.Battery
