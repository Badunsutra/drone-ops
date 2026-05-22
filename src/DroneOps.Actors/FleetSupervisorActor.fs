module DroneOps.Actors.FleetSupervisorActor

open Akka.Actor
open Akka.Event
open DroneOps.Domain.Identifiers
open DroneOps.Domain.Events
open DroneOps.Domain.Drone
open DroneOps.Domain.World
open DroneOps.Actors.Messages
open DroneOps.Actors.DroneActor

type FleetSupervisorActor(world: WorldMap) =
    inherit UntypedActor()

    let mutable log = Unchecked.defaultof<ILoggingAdapter>
    // Реестр дронов: DroneId → IActorRef
    let mutable drones: Map<DroneId, IActorRef> = Map.empty

    // Supervision strategy: Stop on failure.
    // Default Restart не используем — дрон потеряет маршрут и миссию.
    // Stop → DroneFailed event → MissionDispatcher reassigns.
    override _.SupervisorStrategy() =
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
                // Дочерний актор получает имя из DroneId — явный path в иерархии:
                // /user/fleet/drone-001
                let name = DroneId.value droneId
                let props = Props.Create<DroneActor>(fun () -> DroneActor(initialState, world))
                let ref = UntypedActor.Context.ActorOf(props, name)
                // Наблюдаем за дочерним актором — получим Terminated при остановке
                UntypedActor.Context.Watch(ref) |> ignore
                drones <- drones |> Map.add droneId ref
                log.Info("Spawned drone [{0}] at {1}", DroneId.value droneId, position)

            | StopDrone droneId ->
                match drones |> Map.tryFind droneId with
                | None -> log.Warning("StopDrone: drone [{0}] not found", DroneId.value droneId)
                | Some ref -> ref.Tell(StopDroneCommand)

        // Получаем уведомление когда дочерний актор остановился
        | :? Terminated as t ->
            let stopped = drones |> Map.tryFindKey (fun _ ref -> ref = t.ActorRef)

            match stopped with
            | None -> ()
            | Some droneId ->
                drones <- drones |> Map.remove droneId
                UntypedActor.Context.System.EventStream.Publish(DroneStopped droneId)
                log.Info("Drone [{0}] stopped", DroneId.value droneId)

        | _ -> this.Unhandled(message)
