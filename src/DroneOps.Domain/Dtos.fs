module DroneOps.Domain.Dtos

// ─── Initial world ────────────────────────────────────────────

// Статичная карта — отправляется UI один раз при подключении.
// Препятствия и станции не меняются в MVP,
// поэтому нет смысла гнать их в каждом snapshot.

type ObstacleDto = { X: int; Y: int }

type ChargingStationDto = { Id: string; X: int; Y: int }

type InitialWorldDto =
    { Width: int
      Height: int
      Obstacles: ObstacleDto list
      Stations: ChargingStationDto list }

// ─── Runtime snapshot ─────────────────────────────────────────

// Отправляется через SignalR каждые 100-200 мс.
// Содержит только изменяемое состояние —
// дроны, миссии и последние события.

type DroneViewDto =
    { Id: string
      X: int
      Y: int
      BatteryPercent: int
      Status: string
      CurrentMissionId: string option }

type MissionViewDto =
    { Id: string
      PickupX: int
      PickupY: int
      DropoffX: int
      DropoffY: int
      Priority: string
      Status: string }

type EventViewDto =
    { Kind: string
      Message: string
      Tick: int64 option }

type WorldSnapshotDto =
    { Tick: int64
      Drones: DroneViewDto list
      Missions: MissionViewDto list
      RecentEvents: EventViewDto list }

// ─── Mapping ──────────────────────────────────────────────────

// Преобразование доменных типов в DTO живёт здесь —
// не в акторах и не в доменных типах.
// Акторы работают с доменными типами,
// TelemetryActor вызывает mapping при сборке snapshot.

module Mapping =

    open Identifiers
    open Position
    open Battery
    open Drone
    open Mission
    open World

    let toObstacleDto (pos: Position) : ObstacleDto = { X = pos.X; Y = pos.Y }

    let toChargingStationDto (station: ChargingStation) : ChargingStationDto =
        { Id = ChargingStationId.value station.Id
          X = station.Position.X
          Y = station.Position.Y }

    let toInitialWorldDto (world: WorldMap) : InitialWorldDto =
        { Width = world.Width
          Height = world.Height
          Obstacles = world.Obstacles |> Set.toList |> List.map toObstacleDto
          Stations =
            world.ChargingStations
            |> Map.values
            |> Seq.map toChargingStationDto
            |> Seq.toList }

    let private droneStatusString (status: DroneStatus) =
        match status with
        | Idle -> "idle"
        | MovingToPickup _ -> "moving-to-pickup"
        | MovingToDropoff _ -> "moving-to-dropoff"
        | ReturningToCharge None -> "returning-to-charge"
        | ReturningToCharge(Some _) -> "returning-to-charge"
        | Charging _ -> "charging"
        | DroneStatus.Failed _ -> "failed"

    let toDroneViewDto (state: DroneState) : DroneViewDto =
        { Id = DroneId.value state.Id
          X = state.Position.X
          Y = state.Position.Y
          BatteryPercent = BatteryLevel.value state.Battery
          Status = droneStatusString state.Status
          CurrentMissionId = state.CurrentMission |> Option.map MissionId.value }

    let private missionStatusString (status: MissionStatus) =
        match status with
        | Pending -> "pending"
        | Assigned _ -> "assigned"
        | PickupInProgress _ -> "pickup-in-progress"
        | DropoffInProgress _ -> "dropoff-in-progress"
        | Completed -> "completed"
        | Failed _ -> "failed"

    let toMissionViewDto (mission: Mission) : MissionViewDto =
        { Id = MissionId.value mission.Id
          PickupX = mission.Pickup.X
          PickupY = mission.Pickup.Y
          DropoffX = mission.Dropoff.X
          DropoffY = mission.Dropoff.Y
          Priority = $"{mission.Priority}".ToLowerInvariant()
          Status = missionStatusString mission.Status }
