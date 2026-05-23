module DroneOps.Actors.FleetSupervisorActor

open Akka.Actor
open Akka.Event
open DroneOps.Domain.Identifiers
open DroneOps.Domain.Drone
open DroneOps.Domain.Events
open DroneOps.Domain.World
open DroneOps.Actors.Messages
open DroneOps.Actors.DroneActor

type FleetSupervisorActor(world: WorldMap) =
    inherit UntypedActor()

    let mutable log = Unchecked.defaultof<ILoggingAdapter>
    let mutable drones: Map<DroneId, IActorRef> = Map.empty
    // Дроны остановленные штатно через StopDrone команду
    let mutable voluntaryStops: Set<DroneId> = Set.empty
    // Дроны которые сами сообщили о сбое через DroneInternalMessage
    let mutable selfReported: Set<DroneId> = Set.empty

    override _.SupervisorStrategy() =
        // Stop on any failure — не используем Restart чтобы не потерять состояние дрона.
        // После Stop: Terminated → FleetSupervisor публикует DroneFailed/DroneStopped
        OneForOneStrategy(fun _ -> Directive.Stop) :> SupervisorStrategy

    override this.PreStart() =
        log <- Logging.GetLogger(UntypedActor.Context)
        log.Info("FleetSupervisor started")

    override this.OnReceive(message: obj) =
        match message with
        | :? FleetCommand as cmd ->
            match cmd with
            | SpawnDrone(droneId, position, battery, config) ->
                let initialState = DroneState.create droneId position battery config
                let name = DroneId.value droneId
                let props = Props.Create<DroneActor>(fun () -> DroneActor(initialState, world))
                let ref = UntypedActor.Context.ActorOf(props, name)
                UntypedActor.Context.Watch(ref) |> ignore
                drones <- drones |> Map.add droneId ref
                log.Info("Spawned drone [{0}] at {1}", name, position)

            | StopDrone droneId ->
                match drones |> Map.tryFind droneId with
                | None -> log.Warning("StopDrone: drone [{0}] not found", DroneId.value droneId)
                | Some ref ->
                    voluntaryStops <- voluntaryStops |> Set.add droneId
                    ref.Tell(StopDroneCommand)

        // DroneActor сам обнаружил сбой — уже опубликовал DroneFailed через EventStream
        | :? DroneInternalMessage as msg ->
            match msg with
            | DroneReportedFailure(droneId, _) -> selfReported <- selfReported |> Set.add droneId

        // Дочерний актор остановился
        | :? Terminated as t ->
            let stoppedOpt = drones |> Map.tryFindKey (fun _ ref -> ref = t.ActorRef)

            match stoppedOpt with
            | None -> ()
            | Some droneId ->
                drones <- drones |> Map.remove droneId
                voluntaryStops <- voluntaryStops |> Set.remove droneId
                selfReported <- selfReported |> Set.remove droneId

                if voluntaryStops |> Set.contains droneId then
                    // Штатная остановка
                    UntypedActor.Context.System.EventStream.Publish(DroneStopped droneId)
                    log.Info("Drone [{0}] stopped normally", DroneId.value droneId)

                elif selfReported |> Set.contains droneId then
                    // Дрон уже опубликовал DroneFailed — дополнительно не публикуем
                    log.Info("Drone [{0}] terminated after self-reported failure", DroneId.value droneId)

                else
                    // Необработанное исключение — дрон не успел сообщить о сбое
                    let reason = InternalError $"Actor terminated unexpectedly"
                    UntypedActor.Context.System.EventStream.Publish(DroneFailed(droneId, reason))
                    log.Warning("Drone [{0}] terminated unexpectedly", DroneId.value droneId)

        | _ -> this.Unhandled(message)
