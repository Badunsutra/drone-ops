# Предлагаемая структура репозитория

```text
DroneOps/
│
├─ README.md
├─ DroneOps.sln
│
├─ docs/
│  ├─ project_overview.md
│  ├─ stack.md
│  ├─ repository_structure.md
│  ├─ development_notes.md
│  ├─ changelog.md
│  │
│  ├─ architecture/
│  │  ├─ architecture_overview.md
│  │  └─ actors.md
│  │
│  ├─ domain/
│  │  └─ domain_model.md
│  │
│  ├─ planning/
│  │  ├─ roadmap.md
│  │  ├─ backlog.md
│  │  └─ mvp_definition.md
│  │
│  └─ adr/
│     ├─ 0001-use-fsharp-for-domain.md
│     ├─ 0002-use-akka-net-for-simulation-runtime.md
│     ├─ 0003-use-signalr-for-realtime-ui.md
│     ├─ 0004-use-akka-hosting.md
│     ├─ 0005-keep-route-planning-as-function.md
│     ├─ 0006-drone-owns-battery.md
│     └─ 0007-use-eventstream-for-domain-events.md
│
├─ src/
│  ├─ DroneOps.Domain/
│  │  ├─ DroneOps.Domain.fsproj
│  │  ├─ Identifiers.fs
│  │  ├─ Position.fs
│  │  ├─ Battery.fs
│  │  ├─ Route.fs
│  │  ├─ Drone.fs
│  │  ├─ Mission.fs
│  │  ├─ World.fs
│  │  ├─ Events.fs
│  │  └─ Dtos.fs
│  │
│  ├─ DroneOps.Simulation/
│  │  ├─ DroneOps.Simulation.fsproj
│  │  ├─ RouteEngine.fs
│  │  ├─ Movement.fs
│  │  ├─ BatteryRules.fs
│  │  └─ MissionRules.fs
│  │
│  ├─ DroneOps.Actors/
│  │  ├─ DroneOps.Actors.fsproj
│  │  ├─ ActorNames.fs
│  │  ├─ CommandGatewayActor.fs
│  │  ├─ SimulationClockActor.fs
│  │  ├─ WorldActor.fs
│  │  ├─ DroneActor.fs
│  │  ├─ FleetSupervisorActor.fs
│  │  ├─ MissionDispatcherActor.fs
│  │  ├─ ChargingCoordinatorActor.fs
│  │  ├─ TelemetryActor.fs
│  │  ├─ SignalRBridgeActor.fs
│  │  └─ DeadLetterMonitorActor.fs
│  │
│  ├─ DroneOps.Api/
│  │  ├─ DroneOps.Api.fsproj
│  │  ├─ Program.fs
│  │  ├─ AkkaHosting.fs
│  │  ├─ Hubs/
│  │  │  └─ WorldHub.fs
│  │  └─ Endpoints/
│  │     ├─ MissionEndpoints.fs
│  │     └─ SimulationEndpoints.fs
│  │
│  ├─ DroneOps.Web/
│  │  └─ ...
│  │
│  └─ DroneOps.Wpf/
│     └─ ...
│
└─ tests/
   ├─ DroneOps.Domain.Tests/
   ├─ DroneOps.Simulation.Tests/
   ├─ DroneOps.Actors.Tests/
   └─ DroneOps.Api.Tests/
```

## Правило порядка файлов F#

В F# порядок файлов в проекте важен.

Рекомендуемый порядок для `DroneOps.Domain.fsproj`:

```text
Identifiers.fs
Position.fs
Battery.fs
Route.fs
Drone.fs
Mission.fs
World.fs
Events.fs
Dtos.fs
```

Сначала должны идти базовые типы, затем типы, которые от них зависят.

## Рекомендация

На старте не создавать слишком много проектов. Минимальный набор:

```text
DroneOps.Domain
DroneOps.Simulation
DroneOps.Actors
DroneOps.Api
DroneOps.Tests
```

Web и WPF добавить после появления работающего backend MVP.
