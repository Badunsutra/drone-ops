module DroneOps.Actors.MissionDispatcherActor

open Akka.Actor
open Akka.Event
open DroneOps.Domain.Identifiers
open DroneOps.Domain.Mission
open DroneOps.Domain.Events
open DroneOps.Actors.ActorNames
open DroneOps.Actors.Messages

[<Literal>]
let private MaxRetries = 3

type MissionDispatcherActor() =
    inherit UntypedActor()

    let mutable log = Unchecked.defaultof<ILoggingAdapter>
    let mutable pending: Mission list = []
    let mutable active: Map<MissionId, Mission> = Map.empty
    let mutable idleDrones: Set<DroneId> = Set.empty
    let mutable retryCounts: Map<MissionId, int> = Map.empty

    member private _.PublishMission(event: MissionEvent) =
        UntypedActor.Context.System.EventStream.Publish(event)

    // Сортировка: Critical > High > Normal > Low, FIFO внутри приоритета
    member private _.InsertPending(mission: Mission) =
        pending <- mission :: pending |> List.sortByDescending Mission.priorityWeight

    member private this.TryDispatch() =
        match pending, idleDrones |> Set.toList with
        | [], _
        | _, [] -> ()
        | mission :: rest, droneId :: _ ->
            idleDrones <- idleDrones |> Set.remove droneId
            pending <- rest

            let assigned =
                { mission with
                    Status = Assigned droneId }

            active <- active |> Map.add mission.Id assigned

            // Отправляем команду дрону через ActorSelection — без хранения ссылок
            let path = $"/user/{Fleet}/{DroneId.value droneId}"
            UntypedActor.Context.System.ActorSelection(path).Tell(AssignMission mission)

            this.PublishMission(MissionAssigned(mission.Id, droneId))
            log.Info("Mission [{0}] assigned to drone [{1}]", MissionId.value mission.Id, DroneId.value droneId)

    member private this.FindActiveMissionForDrone(droneId: DroneId) =
        active
        |> Map.toSeq
        |> Seq.tryFind (fun (_, m) ->
            match m.Status with
            | Assigned d
            | PickupInProgress d
            | DropoffInProgress d -> d = droneId
            | _ -> false)
        |> Option.map fst

    member private this.ReturnMissionToPending(missionId: MissionId, droneId: DroneId, reason: MissionRetryReason) =
        match active |> Map.tryFind missionId with
        | None -> ()
        | Some mission ->
            active <- active |> Map.remove missionId
            let retryCount = retryCounts |> Map.tryFind missionId |> Option.defaultValue 0

            if retryCount < MaxRetries then
                let returned = { mission with Status = Pending }
                this.InsertPending(returned)
                retryCounts <- retryCounts |> Map.add missionId (retryCount + 1)
                this.PublishMission(MissionReturnedToPending(missionId, droneId, reason))

                log.Info(
                    "Mission [{0}] returned to pending, retry {1}/{2}",
                    MissionId.value missionId,
                    retryCount + 1,
                    MaxRetries
                )
            else
                retryCounts <- retryCounts |> Map.remove missionId
                this.PublishMission(MissionFailed(missionId, RetryLimitExceeded reason))
                log.Warning("Mission [{0}] failed — retry limit exceeded", MissionId.value missionId)

    member private this.HandleDroneEvent(evt: DroneEvent) =
        match evt with
        | DroneSpawned(droneId, _) ->
            idleDrones <- idleDrones |> Set.add droneId
            this.TryDispatch()

        | DroneBecameIdle droneId ->
            idleDrones <- idleDrones |> Set.add droneId
            this.TryDispatch()

        | DroneMissionAcknowledged(missionId, droneId) ->
            match active |> Map.tryFind missionId with
            | None -> ()
            | Some mission ->
                active <-
                    active
                    |> Map.add
                        missionId
                        { mission with
                            Status = PickupInProgress droneId }

                this.PublishMission(MissionAcknowledged(missionId, droneId))

        | DronePickupReached(missionId, droneId) ->
            match active |> Map.tryFind missionId with
            | None -> ()
            | Some mission ->
                active <-
                    active
                    |> Map.add
                        missionId
                        { mission with
                            Status = DropoffInProgress droneId }

                this.PublishMission(MissionPickupReached(missionId, droneId))
                this.PublishMission(MissionDropoffStarted(missionId, droneId))

        | DroneDropoffReached(missionId, droneId) ->
            active <- active |> Map.remove missionId
            retryCounts <- retryCounts |> Map.remove missionId
            this.PublishMission(MissionCompleted(missionId, droneId))
            log.Info("Mission [{0}] completed by drone [{1}]", MissionId.value missionId, DroneId.value droneId)

        | DroneNeedsCharging(droneId, _, _) ->
            idleDrones <- idleDrones |> Set.remove droneId

            match this.FindActiveMissionForDrone(droneId) with
            | None -> ()
            | Some missionId ->
                this.ReturnMissionToPending(missionId, droneId, MissionRetryReason.DroneNeedsCharging droneId)

        | DroneFailed(droneId, reason) ->
            idleDrones <- idleDrones |> Set.remove droneId

            match this.FindActiveMissionForDrone(droneId) with
            | None -> ()
            | Some missionId ->
                active <- active |> Map.remove missionId
                this.PublishMission(MissionFailed(missionId, MissionFailureReason.DroneFailed(droneId, reason)))

        | DroneStopped droneId -> idleDrones <- idleDrones |> Set.remove droneId

        | _ -> () // DronePositionChanged, DroneBatteryChanged — игнорируем

    override this.PreStart() =
        log <- Logging.GetLogger(UntypedActor.Context)

        UntypedActor.Context.System.EventStream.Subscribe(this.Self, typeof<DroneEvent>)
        |> ignore

        log.Info("MissionDispatcher started")

    override this.PostStop() =
        UntypedActor.Context.System.EventStream.Unsubscribe(this.Self, typeof<DroneEvent>)
        |> ignore

    override this.OnReceive(message: obj) =
        match message with
        | :? MissionDispatcherCommand as cmd ->
            match cmd with
            | CreateMission mission ->
                this.InsertPending(mission)
                this.PublishMission(MissionCreated mission)

                log.Info(
                    "Mission [{0}] created (priority={1}), pending={2}",
                    MissionId.value mission.Id,
                    mission.Priority,
                    pending.Length
                )

                this.TryDispatch()

        | :? DroneEvent as evt -> this.HandleDroneEvent(evt)

        | _ -> this.Unhandled(message)
