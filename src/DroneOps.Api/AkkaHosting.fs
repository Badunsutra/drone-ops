module DroneOps.Api.AkkaHosting

open Akka.Actor
open Akka.Hosting
open Microsoft.Extensions.DependencyInjection
open DroneOps.Actors.ActorNames
open DroneOps.Actors.DeadLetterMonitorActor

let configureAkka (services: IServiceCollection) =
    services.AddAkka(
        "DroneOpsSystem",
        fun (builder: AkkaConfigurationBuilder) ->
            builder
                // Интегрируем Akka logging с Microsoft.Extensions.Logging —
                // все логи акторов пойдут в стандартный ILogger pipeline.
                .ConfigureLoggers(fun setup -> setup.AddLoggerFactory() |> ignore)
                // WithActors — регистрируем акторы при старте ActorSystem.
                // DeadLetterMonitor создаётся первым — должен быть готов
                // к моменту когда другие акторы начнут отправлять сообщения.
                .WithActors(fun system (registry: IActorRegistry) ->
                    let deadLetterMonitor =
                        system.ActorOf(Props.Create<DeadLetterMonitorActor>(), DeadLetterMonitor)

                    registry.Register<DeadLetterMonitorActor>(deadLetterMonitor))
            |> ignore
    )
    |> ignore
