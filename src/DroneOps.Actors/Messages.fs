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

// Внутреннее сообщение — актор отправляет себе через scheduler.
// private в рамках модуля не работает для типов, поэтому используем
// отдельный тип чтобы отличить от внешних команд.
type ClockTick = ClockTick

// ─── World ────────────────────────────────────────────────────

type WorldCommand =
    // Ask-запрос — возвращает WorldMap через Sender.Tell
    | GetWorldMap

// ─── Fleet ────────────────────────────────────────────────────

type FleetCommand =
    | SpawnDrone of droneId: DroneId * position: Position * battery: BatteryLevel * config: DroneConfig
    | StopDrone of droneId: DroneId

// ─── Drone ────────────────────────────────────────────────────

type DroneCommand =
    | AssignMission of mission: Mission
    | ForceReturnToBase
    | StopDroneCommand
