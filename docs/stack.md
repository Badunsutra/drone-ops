# Технологический стек

## Основной стек MVP

| Слой | Технология | Назначение |
|---|---|---|
| Domain | F# | Доменные типы, чистые функции, инварианты |
| Simulation | F# | RouteEngine, battery rules, mission rules |
| Actor runtime | Akka.NET | Дроны, диспетчеризация, clock, telemetry |
| Hosting | Akka.Hosting | Интеграция ActorSystem с ASP.NET Core DI/lifetime |
| API | ASP.NET Core | HTTP endpoints, backend process |
| Realtime | SignalR | Передача snapshot в UI |
| Web UI | React/Angular/Blazor/Fable | Визуализация карты и статусов |
| WPF UI | WPF + SignalR .NET Client | Альтернативный desktop-клиент |
| Tests | xUnit или Expecto | Unit/integration tests |

## Akka.NET

Akka.NET используется как runtime для сущностей, у которых есть:

- собственное состояние;
- жизненный цикл;
- mailbox;
- supervision;
- асинхронное взаимодействие.

В MVP акторы применяются для:

- `DroneActor`;
- `FleetSupervisorActor`;
- `MissionDispatcherActor`;
- `SimulationClockActor`;
- `WorldActor` как источник статичной карты;
- `ChargingCoordinatorActor`;
- `TelemetryActor`;
- `SignalRBridgeActor`;
- `CommandGatewayActor`;
- `DeadLetterMonitorActor`.

## Akka.Hosting

Для ASP.NET Core используется `Akka.Hosting`.

Причины:

- интеграция с `IServiceCollection`;
- управляемый lifetime `ActorSystem`;
- меньше ручного boilerplate;
- удобнее подключать DI, logging, configuration;
- лучше подходит для современного Akka.NET-проекта.

## Akka.FSharp

`Akka.FSharp` не берётся как основной API MVP.

Классический `actor { ... }` computation expression относится к legacy untyped-style подходу. F# в проекте используется в первую очередь для доменной модели, message types и чистой логики. Runtime-интеграция строится через современный Akka.NET hosting-подход.

## SignalR

SignalR используется для server-to-client real-time updates.

Основной паттерн:

```text
Actor events -> TelemetryActor -> LatestWorldSnapshot buffer -> throttled publish -> SignalRHub -> UI
```

Не нужно отправлять каждое событие напрямую в UI. Для MVP достаточно публиковать последний snapshot с частотой 5-10 раз в секунду.

## Akka.Streams

Akka.Streams не входит в MVP, но планируется как развитие телеметрии.

Добавлять, когда понадобятся:

- backpressure;
- фильтрация/агрегация потоков;
- windowing;
- throttling как часть stream pipeline;
- более устойчивый telemetry pipeline.
