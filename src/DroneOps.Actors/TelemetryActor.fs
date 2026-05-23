module DroneOps.Actors.TelemetryActor

open System
open Akka.Actor
open Akka.Event
open DroneOps.Domain.Identifiers
open DroneOps.Domain.Battery
open DroneOps.Domain.Drone
open DroneOps.Domain.Mission
open DroneOps.Domain.Events
open DroneOps.Domain.Dtos
open DroneOps.Actors.ActorNames
open DroneOps.Actors.Messages

// Внутренний триггер публикации snapshot
type private PublishSnapshot = PublishSnapshot

// Read model — последнее известное состояние каждого дрона/миссии
type private DroneReadModel =
    { Id       : DroneId
      Position : DroneOps.Domain.Position.Position
      Battery  : BatteryLevel
      Status   : DroneStatus
      Mission  : MissionId option }

type TelemetryActor(snapshotIntervalMs: int) =
    inherit UntypedActor()

    let mutable log          = Unchecked.defaultof<ILoggingAdapter>
    let mutable tick         = 0L
    let mutable dirty        = false
    let mutable scheduled    : ICancelable option = None

    // Read models
    let mutable drones   : Map<DroneId, DroneReadModel> = Map.empty
    let mutable missions : Map<MissionId, Mission> = Map.empty

    // Кольцевой буфер последних событий для UI
    let maxRecentEvents = 20
    let mutable recentEvents : EventViewDto list = []

    let addEvent kind message tickOpt =
        let evt = { Kind = kind; Message = message; Tick = tickOpt }
        recentEvents <-
            evt :: recentEvents
            |> List.truncate maxRecentEvents

    member private _.Publish(event: obj) =
        UntypedActor.Context.System.EventStream.Publish(event)

    // ─── Snapshot building ────────────────────────────────────

    member private this.BuildSnapshot() : WorldSnapshotDto =
        { Tick         = tick
          Drones       = drones   |> Map.values |> Seq.map this.ToDroneDto   |> Seq.toList
          Missions     = missions  |> Map.values |> Seq.map Mapping.toMissionViewDto |> Seq.toList
          RecentEvents = recentEvents }

    member private _.ToDroneDto(m: DroneReadModel) : DroneViewDto =
        let statusStr =
            match (m.Status : DroneStatus) with
            | Idle                       -> "idle"
            | MovingToPickup _           -> "moving-to-pickup"
            | MovingToDropoff _          -> "moving-to-dropoff"
            | ReturningToCharge None     -> "returning-to-charge"
            | ReturningToCharge (Some _) -> "returning-to-charge"
            | DroneStatus.Charging _     -> "charging"
            | DroneStatus.Failed _       -> "failed"
        { Id               = DroneId.value m.Id
          X                = m.Position.X
          Y                = m.Position.Y
          BatteryPercent   = BatteryLevel.value m.Battery
          Status           = statusStr
          CurrentMissionId = m.Mission |> Option.map MissionId.value }

    // ─── Event handlers ───────────────────────────────────────

    member private this.HandleDroneEvent(evt: DroneEvent) =
        dirty <- true
        match evt with
        | DroneSpawned(droneId, pos) ->
            drones <- drones |> Map.add droneId
                { Id = droneId; Position = pos; Battery = BatteryLevel.full
                  Status = Idle; Mission = None }
            addEvent "drone-spawned" $"Drone {DroneId.value droneId} spawned at ({pos.X},{pos.Y})" (Some tick)

        | DronePositionChanged(droneId, pos) ->
            drones <- drones |> Map.change droneId (Option.map (fun d -> { d with Position = pos }))

        | DroneBatteryChanged(droneId, battery) ->
            drones <- drones |> Map.change droneId (Option.map (fun d -> { d with Battery = battery }))

        | DroneBecameIdle droneId ->
            drones <- drones |> Map.change droneId (Option.map (fun d ->
                { d with Status = Idle; Mission = None }))

        | DroneNeedsCharging(droneId, _, battery) ->
            drones <- drones |> Map.change droneId (Option.map (fun d ->
                { d with Status = ReturningToCharge None; Battery = battery; Mission = None }))
            addEvent "low-battery" $"Drone {DroneId.value droneId} needs charging" (Some tick)

        | DroneChargingStarted(droneId, stationId) ->
            drones <- drones |> Map.change droneId (Option.map (fun d ->
                { d with Status = DroneStatus.Charging stationId }))
            addEvent "charging-started"
                $"Drone {DroneId.value droneId} charging at station {ChargingStationId.value stationId}"
                (Some tick)

        | DroneChargingCompleted(droneId, _) ->
            drones <- drones |> Map.change droneId (Option.map (fun d ->
                { d with Status = Idle }))
            addEvent "charging-done" $"Drone {DroneId.value droneId} fully charged" (Some tick)

        | DroneMissionAcknowledged(missionId, droneId) ->
            drones <- drones |> Map.change droneId (Option.map (fun d ->
                { d with Status = MovingToPickup missionId; Mission = Some missionId }))

        | DronePickupReached(missionId, droneId) ->
            drones <- drones |> Map.change droneId (Option.map (fun d ->
                { d with Status = MovingToDropoff missionId }))

        | DroneDropoffReached(_, droneId) ->
            drones <- drones |> Map.change droneId (Option.map (fun d ->
                { d with Status = Idle; Mission = None }))

        | DroneFailed(droneId, reason) ->
            drones <- drones |> Map.change droneId (Option.map (fun d ->
                { d with Status = DroneStatus.Failed reason }))
            addEvent "drone-failed" $"Drone {DroneId.value droneId} failed: {reason}" (Some tick)

        | DroneStopped droneId ->
            drones <- drones |> Map.remove droneId

    member private this.HandleMissionEvent(evt: MissionEvent) =
        dirty <- true
        match evt with
        | MissionCreated mission ->
            missions <- missions |> Map.add mission.Id mission
            addEvent "mission-created" $"Mission {MissionId.value mission.Id} created" (Some tick)

        | MissionAssigned(missionId, droneId) ->
            missions <- missions |> Map.change missionId (Option.map (fun m ->
                { m with Status = Assigned droneId }))

        | MissionAcknowledged(missionId, droneId) ->
            missions <- missions |> Map.change missionId (Option.map (fun m ->
                { m with Status = PickupInProgress droneId }))

        | MissionPickupReached(missionId, droneId) ->
            missions <- missions |> Map.change missionId (Option.map (fun m ->
                { m with Status = DropoffInProgress droneId }))

        | MissionDropoffStarted _ -> ()

        | MissionCompleted(missionId, droneId) ->
            missions <- missions |> Map.change missionId (Option.map (fun m ->
                { m with Status = Completed }))
            addEvent "mission-completed" $"Mission {MissionId.value missionId} completed by drone {DroneId.value droneId}" (Some tick)

        | MissionReturnedToPending(missionId, _, reason) ->
            missions <- missions |> Map.change missionId (Option.map (fun m ->
                { m with Status = Pending }))
            addEvent "mission-retry" $"Mission {MissionId.value missionId} returned to pending: {reason}" (Some tick)

        | MissionFailed(missionId, reason) ->
            missions <- missions |> Map.change missionId (Option.map (fun m ->
                { m with Status = MissionStatus.Failed reason }))
            addEvent "mission-failed" $"Mission {MissionId.value missionId} failed: {reason}" (Some tick)

    member private this.HandleChargingEvent(evt: ChargingEvent) =
        dirty <- true
        match evt with
        | ChargingStationAssigned(droneId, stationId, _) ->
            drones <- drones |> Map.change droneId (Option.map (fun d ->
                { d with Status = ReturningToCharge(Some stationId) }))
        | ChargingStationQueued droneId ->
            addEvent "charging-queued" $"Drone {DroneId.value droneId} queued for charging" (Some tick)
        | ChargingStationReleased _ -> ()

    // ─── Lifecycle ────────────────────────────────────────────

    override this.PreStart() =
        log <- Logging.GetLogger(UntypedActor.Context)
        let es = UntypedActor.Context.System.EventStream
        es.Subscribe(this.Self, typeof<DroneEvent>)   |> ignore
        es.Subscribe(this.Self, typeof<MissionEvent>) |> ignore
        es.Subscribe(this.Self, typeof<ChargingEvent>) |> ignore
        es.Subscribe(this.Self, typeof<SimulationTick>) |> ignore

        // Запускаем throttling таймер
        let interval = TimeSpan.FromMilliseconds(float snapshotIntervalMs)
        let c =
            UntypedActor.Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                interval, interval, this.Self, PublishSnapshot, this.Self)
        scheduled <- Some c
        log.Info("TelemetryActor started — snapshot interval={0}ms", snapshotIntervalMs)

    override this.PostStop() =
        scheduled |> Option.iter (fun c -> c.Cancel())
        let es = UntypedActor.Context.System.EventStream
        es.Unsubscribe(this.Self, typeof<DroneEvent>)    |> ignore
        es.Unsubscribe(this.Self, typeof<MissionEvent>)  |> ignore
        es.Unsubscribe(this.Self, typeof<ChargingEvent>) |> ignore
        es.Unsubscribe(this.Self, typeof<SimulationTick>) |> ignore

    override this.OnReceive(message: obj) =
        match message with
        | :? SimulationTick as t ->
            tick <- t.Tick

        | :? DroneEvent as evt ->
            this.HandleDroneEvent(evt)

        | :? MissionEvent as evt ->
            this.HandleMissionEvent(evt)

        | :? ChargingEvent as evt ->
            this.HandleChargingEvent(evt)

        | :? PublishSnapshot ->
            if dirty then
                let snapshot = this.BuildSnapshot()
                // Публикуем snapshot — SignalRBridgeActor подхватит
                UntypedActor.Context.System.EventStream.Publish(snapshot)
                dirty <- false

        | _ -> this.Unhandled(message)
