# Backlog

## Domain

- [ ] Добавить `BatteryLevel` со smart constructor.
- [ ] Добавить `DroneConfig`.
- [ ] Добавить `Route` с явной семантикой.
- [ ] Заменить `Failed of string` на typed failure reasons.
- [ ] Добавить `WorldMap`.
- [ ] Добавить `MissionPriority`.
- [ ] Добавить typed events: `DroneEvent`, `MissionEvent`, `ChargingEvent`, `WorldEvent`, `SimulationTick`.
- [ ] Добавить DTO: `InitialWorldDto` и `WorldSnapshotDto`.

## Simulation

- [ ] Реализовать `RouteEngine.planRoute`.
- [ ] Реализовать `Movement.moveOneStep` как чистую функцию, возвращающую `MovementResult`, без `DroneEvent list`.
- [ ] Реализовать battery drain/charge.
- [ ] Реализовать drone decision: need charging.
- [ ] Реализовать mission state transitions.

## Actors

- [ ] Подключить Akka.Hosting.
- [ ] Добавить `CommandGatewayActor`.
- [ ] Добавить `SimulationClockActor`.
- [ ] Добавить `WorldActor`.
- [ ] Добавить `FleetSupervisorActor`.
- [ ] Добавить `DroneActor`.
- [ ] Добавить `MissionDispatcherActor`.
- [ ] Добавить `ChargingCoordinatorActor`.
- [ ] Добавить `TelemetryActor`.
- [ ] Добавить `SignalRBridgeActor`.
- [ ] Добавить `DeadLetterMonitorActor`.
- [ ] Подключить `ActorSystem.EventStream` для domain events и simulation ticks.
- [ ] Реализовать lifecycle-подписку `DroneActor` на `SimulationTick`.
- [ ] Добавить `DroneMissionAcknowledged -> MissionAcknowledged` flow.
- [ ] Зафиксировать явные actor paths.
- [ ] Описать и реализовать supervision strategy.

## API / Realtime

- [ ] Добавить `WorldHub`.
- [ ] Добавить команды создания миссии.
- [ ] Добавить команды pause/resume/speed.
- [ ] Добавить отдачу `InitialWorldDto`.
- [ ] Добавить throttled `WorldSnapshotDto` publish.

## UI

- [ ] Сделать grid-map 20x20.
- [ ] Отобразить дроны.
- [ ] Отобразить зарядные станции.
- [ ] Отобразить препятствия.
- [ ] Отобразить список миссий.
- [ ] Отобразить event log.

## Later

- [ ] Akka.Streams telemetry pipeline.
- [ ] Akka.Persistence.
- [ ] Akka.Cluster.
- [ ] WPF-клиент.
- [ ] OpenTelemetry.
