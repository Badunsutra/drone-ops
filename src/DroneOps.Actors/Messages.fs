module DroneOps.Actors.Messages

open DroneOps.Domain.Identifiers
open DroneOps.Domain.Position
open DroneOps.Domain.Battery
open DroneOps.Domain.Drone
open DroneOps.Domain.Mission

// ─── SimulationClock ──────────────────────────────────────────

type SimulationClockCommand =
    | StartSimulation
    | PauseSimulation
    | ResumeSimulation
    | ChangeSpeed of speed: decimal

type ClockTick = ClockTick

// ─── World ────────────────────────────────────────────────────

type WorldCommand = | GetWorldMap

// ─── Fleet ────────────────────────────────────────────────────

type FleetCommand =
    | SpawnDrone of droneId: DroneId * position: Position * battery: BatteryLevel * config: DroneConfig
    | StopDrone of droneId: DroneId

// Internal: DroneActor → FleetSupervisor (parent)
// Сигнализирует что дрон сам обнаружил и опубликовал свой сбой
type DroneInternalMessage = DroneReportedFailure of droneId: DroneId * reason: DroneFailureReason

// ─── Drone ────────────────────────────────────────────────────

type DroneCommand =
    | AssignMission of mission: Mission
    | ForceReturnToBase
    | StopDroneCommand

// ─── MissionDispatcher ────────────────────────────────────────

type MissionDispatcherCommand = CreateMission of mission: Mission
