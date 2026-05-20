module DroneOps.Domain.Events

open Identifiers
open Position
open Battery
open Drone
open Mission
open World

// ─── Simulation tick ──────────────────────────────────────────

// Публикует SimulationClockActor через EventStream.
// Все дроны подписаны на этот тип — двигаются синхронно.
type SimulationTick =
    { Tick: int64
      DeltaMs: int
      Speed: decimal }

// ─── Drone events ─────────────────────────────────────────────

// DroneActor публикует только события о себе.
// Он НЕ публикует MissionEvent и ChargingEvent —
// это чужие области состояния.
type DroneEvent =
    | DroneSpawned of droneId: DroneId * position: Position
    | DroneBecameIdle of droneId: DroneId
    | DronePositionChanged of droneId: DroneId * position: Position
    | DroneBatteryChanged of droneId: DroneId * battery: BatteryLevel
    | DroneNeedsCharging of droneId: DroneId * position: Position * battery: BatteryLevel
    | DroneMissionAcknowledged of missionId: MissionId * droneId: DroneId
    | DronePickupReached of missionId: MissionId * droneId: DroneId
    | DroneDropoffReached of missionId: MissionId * droneId: DroneId
    | DroneChargingStarted of droneId: DroneId * stationId: ChargingStationId
    | DroneChargingCompleted of droneId: DroneId * stationId: ChargingStationId
    | DroneFailed of droneId: DroneId * reason: DroneFailureReason
    | DroneStopped of droneId: DroneId

// ─── Mission events ───────────────────────────────────────────

// Публикует только MissionDispatcherActor —
// он единственный владелец состояния миссий.
// Реагирует на DroneEvent и переводит MissionStatus.
type MissionEvent =
    | MissionCreated of mission: Mission
    | MissionAssigned of missionId: MissionId * droneId: DroneId
    | MissionAcknowledged of missionId: MissionId * droneId: DroneId
    | MissionPickupReached of missionId: MissionId * droneId: DroneId
    | MissionDropoffStarted of missionId: MissionId * droneId: DroneId
    | MissionCompleted of missionId: MissionId * droneId: DroneId
    | MissionReturnedToPending of missionId: MissionId * previousDroneId: DroneId * reason: MissionRetryReason
    | MissionFailed of missionId: MissionId * reason: MissionFailureReason

// ─── Charging events ──────────────────────────────────────────

// Публикует только ChargingCoordinatorActor —
// он владелец состояния зарядной инфраструктуры.
// DroneActor подписывается чтобы получить назначенную станцию.
type ChargingEvent =
    | ChargingStationAssigned of droneId: DroneId * stationId: ChargingStationId * position: Position
    | ChargingStationQueued of droneId: DroneId
    | ChargingStationReleased of stationId: ChargingStationId

// ─── World events ─────────────────────────────────────────────

// WorldMap статичен в MVP.
// WorldInitialized — диагностическое событие,
// не используется для первичной загрузки карты в TelemetryActor
// из-за отсутствия буферизации в EventStream.
type WorldEvent =
    | WorldInitialized of map: WorldMap
    | ObstacleAdded of position: Position
    | ObstacleRemoved of position: Position
