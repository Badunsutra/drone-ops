module DroneOps.Actors.ChargingCoordinatorActor

open Akka.Actor
open Akka.Event
open DroneOps.Domain.Identifiers
open DroneOps.Domain.Position
open DroneOps.Domain.Events
open DroneOps.Domain.World
open DroneOps.Actors.ActorNames

type ChargingCoordinatorActor(world: WorldMap) =
    inherit UntypedActor()

    let mutable log = Unchecked.defaultof<ILoggingAdapter>

    // Свободные станции: Id → Position
    let mutable freeStations     : Map<ChargingStationId, Position> =
        world.ChargingStations |> Map.map (fun _ s -> s.Position)

    // Занятые станции: StationId → DroneId
    let mutable occupiedStations : Map<ChargingStationId, DroneId> = Map.empty

    // Очередь дронов ожидающих станцию — FIFO
    let mutable waitingQueue     : DroneId list = []

    member private _.Publish(event: ChargingEvent) =
        UntypedActor.Context.System.EventStream.Publish(event)

    // Ближайшая свободная станция к позиции дрона
    member private _.FindNearestFreeStation(dronePos: Position) =
        if Map.isEmpty freeStations then None
        else
            freeStations
            |> Map.toSeq
            |> Seq.minBy (fun (_, stationPos) -> Position.manhattanDistance dronePos stationPos)
            |> fst
            |> Some

    member private this.HandleDroneNeedsCharging(droneId: DroneId, position: Position) =
        match this.FindNearestFreeStation(position) with
        | None ->
            // Свободных станций нет — ставим в очередь
            waitingQueue <- waitingQueue @ [ droneId ]
            this.Publish(ChargingStationQueued droneId)
            log.Info("Drone [{0}] queued for charging (queue length={1})",
                DroneId.value droneId, waitingQueue.Length)

        | Some stationId ->
            let stationPos = freeStations[stationId]
            freeStations     <- freeStations     |> Map.remove stationId
            occupiedStations <- occupiedStations |> Map.add stationId droneId
            this.Publish(ChargingStationAssigned(droneId, stationId, stationPos))
            log.Info("Station [{0}] assigned to drone [{1}]",
                ChargingStationId.value stationId, DroneId.value droneId)

    member private this.HandleDroneChargingCompleted(droneId: DroneId, stationId: ChargingStationId) =
        occupiedStations <- occupiedStations |> Map.remove stationId

        // Восстанавливаем позицию станции из WorldMap
        match world.ChargingStations |> Map.tryFind stationId with
        | None ->
            log.Warning("Unknown station [{0}]", ChargingStationId.value stationId)
        | Some station ->
            // Сначала публикуем освобождение
            this.Publish(ChargingStationReleased stationId)
            log.Info("Station [{0}] released by drone [{1}]",
                ChargingStationId.value stationId, DroneId.value droneId)

            // Затем назначаем следующего в очереди если есть
            match waitingQueue with
            | [] ->
                freeStations <- freeStations |> Map.add stationId station.Position

            | nextDroneId :: rest ->
                waitingQueue     <- rest
                occupiedStations <- occupiedStations |> Map.add stationId nextDroneId
                this.Publish(ChargingStationAssigned(nextDroneId, stationId, station.Position))
                log.Info("Station [{0}] immediately assigned to queued drone [{1}]",
                    ChargingStationId.value stationId, DroneId.value nextDroneId)

    member private this.HandleDroneEvent(evt: DroneEvent) =
        match evt with
        | DroneNeedsCharging(droneId, position, _) ->
            this.HandleDroneNeedsCharging(droneId, position)

        | DroneChargingCompleted(droneId, stationId) ->
            this.HandleDroneChargingCompleted(droneId, stationId)

        | DroneFailed(droneId, _)
        | DroneStopped droneId ->
            // Убираем из очереди если был там
            waitingQueue <- waitingQueue |> List.filter (fun id -> id <> droneId)
            // Освобождаем станцию если была занята
            let stationOpt =
                occupiedStations
                |> Map.toSeq
                |> Seq.tryFind (fun (_, dId) -> dId = droneId)
                |> Option.map fst
            match stationOpt with
            | None -> ()
            | Some stationId ->
                this.HandleDroneChargingCompleted(droneId, stationId)

        | _ -> ()

    override this.PreStart() =
        log <- Logging.GetLogger(UntypedActor.Context)
        UntypedActor.Context.System.EventStream.Subscribe(this.Self, typeof<DroneEvent>) |> ignore
        log.Info("ChargingCoordinator started — {0} stations available",
            world.ChargingStations.Count)

    override this.PostStop() =
        UntypedActor.Context.System.EventStream.Unsubscribe(this.Self, typeof<DroneEvent>) |> ignore

    override this.OnReceive(message: obj) =
        match message with
        | :? DroneEvent as evt -> this.HandleDroneEvent(evt)
        | _                    -> this.Unhandled(message)
