# Roadmap

## Этап 1. Domain model

- `DroneId`, `MissionId`, `ChargingStationId`.
- `Position`, `WorldMap`.
- `BatteryLevel` со smart constructor.
- `Route` с явной семантикой.
- `DroneFailureReason`, `MissionRetryReason`, `MissionFailureReason`.
- `DroneConfig`, `DroneState`, `Mission`.
- `MissionPriority`.
- `DroneEvent`, `MissionEvent`, `ChargingEvent`, `WorldEvent`, `SimulationTick`.
- `InitialWorldDto` и `WorldSnapshotDto`.

Результат: доменная модель компилируется и покрыта unit-тестами.

## Этап 2. Чистая simulation logic

- `RouteEngine.planRoute`.
- `Movement.moveOneStep` с результатом `MovementResult`, без генерации domain events.
- battery drain/charge rules.
- mission transition rules.
- drone decision: `NeedCharging`.

Результат: можно прогнать симуляцию одного дрона без Akka.NET.

## Этап 3. Akka.Hosting bootstrap

- ASP.NET Core host.
- Akka.Hosting integration.
- явные actor names.
- `DeadLetterMonitorActor`.

Результат: ActorSystem управляется через ASP.NET Core lifetime.

## Этап 4. Базовые actors

- `SimulationClockActor`.
- `WorldActor`.
- `FleetSupervisorActor`.
- `DroneActor`.
- публикация `SimulationTick` через EventStream.
- подписка/отписка `DroneActor` в lifecycle hooks.

Результат: несколько дронов получают `SimulationTick` через EventStream и меняют состояние.

## Этап 5. Supervision

- Явная strategy для `FleetSupervisorActor`.
- Stop-on-drone-failure для MVP.
- событие `DroneFailed`.
- реакция `MissionDispatcherActor`.

Результат: отказ дрона обрабатывается предсказуемо.

## Этап 6. Mission dispatching

- `MissionDispatcherActor`.
- очередь pending missions.
- приоритеты миссий + FIFO внутри одного приоритета.
- назначение idle drones.
- `DroneMissionAcknowledged -> MissionAcknowledged` как явное подтверждение назначения.
- обработка completion/failure.

## Этап 7. Charging coordination

- `ChargingCoordinatorActor`.
- очередь на зарядку.
- назначение станции.
- заряд батареи остаётся внутри `DroneActor`.

## Этап 8. Telemetry read model

- `TelemetryActor`.
- `InitialWorldDto` для статичной карты.
- latest `WorldSnapshotDto` для runtime-состояния.
- throttling 100-200 мс.
- recent events buffer.

## Этап 9. SignalR integration

- `WorldHub`.
- `SignalRBridgeActor`.
- подписка UI на snapshot.

## Этап 10. Web visualization

- grid-map.
- drones, stations, obstacles.
- mission list.
- event log.

## Этап 11. WPF client

- SignalR .NET Client.
- альтернативная визуализация.

## Этап 12. Akka.Streams

- заменить или дополнить telemetry pipeline.
- backpressure.
- aggregation windows.

## Этап 13. Persistence

- сохранить mission log.
- восстановление состояния после restart.
- сравнить supervision Restart + persistence против Stop + reassign.
