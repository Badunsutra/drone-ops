module DroneOps.Api.AkkaHosting

open Akka.Actor
open Akka.Hosting
open Microsoft.AspNetCore.SignalR
open Microsoft.Extensions.DependencyInjection
open DroneOps.Domain.Identifiers
open DroneOps.Domain.Position
open DroneOps.Domain.Battery
open DroneOps.Domain.Drone
open DroneOps.Domain.World
open DroneOps.Actors.ActorNames
open DroneOps.Actors.DeadLetterMonitorActor
open DroneOps.Actors.SimulationClockActor
open DroneOps.Actors.WorldActor
open DroneOps.Actors.FleetSupervisorActor
open DroneOps.Actors.DroneActor
open DroneOps.Actors.ChargingCoordinatorActor
open DroneOps.Actors.MissionDispatcherActor
open DroneOps.Actors.TelemetryActor
open DroneOps.Api.WorldHub
open DroneOps.Api.SignalRBridgeActor
open DroneOps.Actors.Messages

let private buildInitialWorld () =
    let world =
        WorldMap.create 20 20
        |> Result.defaultWith (fun e -> failwith $"Failed to create world: {e}")

    let obstacles =
        [ { X=5; Y=5 }; { X=5; Y=6 }; { X=5; Y=7 }
          { X=10; Y=3 }; { X=10; Y=4 }; { X=10; Y=5 } ]

    let station1 =
        { Id       = ChargingStationId.fromNumber 1 |> Result.defaultWith failwith
          Position = { X=0; Y=0 } }

    let station2 =
        { Id       = ChargingStationId.fromNumber 2 |> Result.defaultWith failwith
          Position = { X=19; Y=19 } }

    obstacles
    |> List.fold (fun w p -> WorldMap.addObstacle p w) world
    |> WorldMap.addStation station1
    |> WorldMap.addStation station2

let private defaultDroneConfig =
    { StepsPerTick         = 1
      BatteryDrainPerStep  = 2
      BatteryChargePerTick = 5
      LowBatteryThreshold  = BatteryLevel.unsafeCreate 20
      MaxPayloadKg         = None }

let configureAkka (services: IServiceCollection) =
    services.AddAkka(
        "DroneOpsSystem",
        // Двухпараметровый вариант — получаем IServiceProvider для IHubContext
        System.Action<AkkaConfigurationBuilder, System.IServiceProvider>(fun builder sp ->
            builder
                .ConfigureLoggers(fun setup ->
                    setup.AddLoggerFactory() |> ignore)
                .WithActors(fun system (registry: IActorRegistry) ->
                    let world = buildInitialWorld()

                    // 1. DeadLetterMonitor
                    let deadLetterMonitor =
                        system.ActorOf(Props.Create<DeadLetterMonitorActor>(), DeadLetterMonitor)
                    registry.Register<DeadLetterMonitorActor>(deadLetterMonitor)

                    // 2. WorldActor
                    let worldActor =
                        system.ActorOf(Props.Create<WorldActor>(fun () -> WorldActor(world)), World)
                    registry.Register<WorldActor>(worldActor)

                    // 3. SimulationClock
                    let clockActor =
                        system.ActorOf(Props.Create<SimulationClockActor>(), SimulationClock)
                    registry.Register<SimulationClockActor>(clockActor)

                    // 4. MissionDispatcher
                    let missionActor =
                        system.ActorOf(Props.Create<MissionDispatcherActor>(), Missions)
                    registry.Register<MissionDispatcherActor>(missionActor)

                    // 5. ChargingCoordinator
                    let chargingActor =
                        system.ActorOf(
                            Props.Create<ChargingCoordinatorActor>(fun () ->
                                ChargingCoordinatorActor(world)),
                            Charging)
                    registry.Register<ChargingCoordinatorActor>(chargingActor)

                    // 6. TelemetryActor
                    let telemetryActor =
                        system.ActorOf(
                            Props.Create<TelemetryActor>(fun () -> TelemetryActor(150)),
                            Telemetry)
                    registry.Register<TelemetryActor>(telemetryActor)

                    // 7. SignalRBridgeActor — получаем HubContext из DI
                    let hubContext = sp.GetRequiredService<IHubContext<WorldHub>>()
                    let signalRBridge =
                        system.ActorOf(
                            Props.Create<SignalRBridgeActor>(fun () ->
                                SignalRBridgeActor(hubContext)),
                            SignalRBridge)
                    registry.Register<SignalRBridgeActor>(signalRBridge)

                    // 8. FleetSupervisor + дроны
                    let fleetActor =
                        system.ActorOf(
                            Props.Create<FleetSupervisorActor>(fun () ->
                                FleetSupervisorActor(world)),
                            Fleet)
                    registry.Register<FleetSupervisorActor>(fleetActor)

                    fleetActor.Tell(SpawnDrone(
                        DroneId.fromNumber 1 |> Result.defaultWith failwith,
                        { X=2; Y=2 }, BatteryLevel.unsafeCreate 80, defaultDroneConfig),
                        Akka.Actor.ActorRefs.NoSender)

                    fleetActor.Tell(SpawnDrone(
                        DroneId.fromNumber 2 |> Result.defaultWith failwith,
                        { X=10; Y=10 }, BatteryLevel.unsafeCreate 90, defaultDroneConfig),
                        Akka.Actor.ActorRefs.NoSender)

                    // 9. Запуск симуляции
                    clockActor.Tell(StartSimulation, Akka.Actor.ActorRefs.NoSender))
            |> ignore))
    |> ignore
