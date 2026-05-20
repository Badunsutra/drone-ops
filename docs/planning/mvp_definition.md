# MVP definition

## Цель MVP

Получить работающую realtime-симуляцию на F# + Akka.NET + SignalR, где actor model применяется для сущностей с состоянием, а не для всех функций подряд.

## Функциональность

- Карта 20x20.
- 3-5 дронов.
- 1-2 зарядные станции.
- Несколько препятствий.
- Создание миссии pickup/dropoff.
- Очередь миссий.
- Приоритет миссий `Critical -> High -> Normal -> Low`, внутри одного приоритета FIFO.
- Назначение миссий свободным дронам.
- Явное подтверждение назначения через `DroneMissionAcknowledged -> MissionAcknowledged`.
- Построение маршрута через `RouteEngine.planRoute`.
- Перемещение дрона по маршруту на каждом tick.
- Расход батареи.
- Самостоятельное решение дрона о необходимости зарядки.
- `InitialWorldDto` для статичной карты: размеры, препятствия, станции.
- Throttled `WorldSnapshotDto` через SignalR для runtime-состояния.
- Базовая web-визуализация grid-map.

## Actor set MVP

- `CommandGatewayActor`
- `SimulationClockActor`
- `WorldActor`
- `FleetSupervisorActor`
- `DroneActor`
- `MissionDispatcherActor`
- `ChargingCoordinatorActor`
- `TelemetryActor`
- `SignalRBridgeActor`
- `DeadLetterMonitorActor`

## Не включать в MVP

- `BatteryManagerActor` как внешний владелец логики батареи.
- `RoutePlannerActor`.
- Akka.Streams.
- Akka.Persistence.
- Akka.Cluster.
- сложную 3D/Canvas-графику.

## Acceptance criteria

- Дрон получает миссию и доходит до pickup/dropoff.
- Батарея уменьшается при движении.
- При низком заряде дрон публикует событие `DroneNeedsCharging`.
- `ChargingCoordinatorActor` назначает станцию.
- UI получает `InitialWorldDto` и может отрисовать станции/препятствия.
- UI получает runtime snapshot не чаще заданного throttle interval.
- `MissionDispatcherActor` переводит миссию в `PickupInProgress` после `DroneMissionAcknowledged` и публикует `MissionAcknowledged`.
- `MissionDispatcherActor` переводит миссию в `DropoffInProgress` после `DronePickupReached`.
- `MissionDispatcherActor` завершает миссию после `DroneDropoffReached`.
- `DroneActor` завершает зарядку на tick при достижении `BatteryLevel = 100`, публикует `DroneChargingCompleted`, после чего `ChargingCoordinatorActor` освобождает станцию.
- Dead letters логируются.
- При искусственном падении `DroneActor` миссия помечается failed или переназначается через `MissionDispatcherActor`.
