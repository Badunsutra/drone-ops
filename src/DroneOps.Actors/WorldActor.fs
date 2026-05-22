module DroneOps.Actors.WorldActor

open Akka.Actor
open Akka.Event
open DroneOps.Domain.Events
open DroneOps.Domain.World
open DroneOps.Actors.Messages

type WorldActor(initialMap: WorldMap) =
    inherit UntypedActor()

    let mutable log = Unchecked.defaultof<ILoggingAdapter>
    // WorldMap иммутабелен в MVP — храним как val
    let world = initialMap

    override this.PreStart() =
        log <- Logging.GetLogger(UntypedActor.Context)
        // Публикуем для диагностики — TelemetryActor не полагается на это событие
        // при старте (EventStream не буферизует), но полезно для логов
        UntypedActor.Context.System.EventStream.Publish(WorldInitialized world)

        log.Info(
            "WorldActor started — map {0}x{1}, obstacles={2}, stations={3}",
            world.Width,
            world.Height,
            world.Obstacles.Count,
            world.ChargingStations.Count
        )

    override this.OnReceive(message: obj) =
        match message with
        | :? WorldCommand as cmd ->
            match cmd with
            | GetWorldMap ->
                // Отвечаем отправителю — используется при startup ask
                this.Sender.Tell(world)
        | _ -> this.Unhandled(message)
