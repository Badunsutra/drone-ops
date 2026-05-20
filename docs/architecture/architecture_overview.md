# Architecture overview

## Runtime схема

```text
Web UI / WPF UI
      │
      │ SignalR
      ▼
DroneOps.Api
      │
      │ user commands
      ▼
CommandGatewayActor
      │
      ├── FleetSupervisorActor
      │       ├── DroneActor /user/fleet/drone-001
      │       ├── DroneActor /user/fleet/drone-002
      │       └── DroneActor /user/fleet/drone-N
      │
      ├── MissionDispatcherActor
      ├── SimulationClockActor
      ├── WorldActor
      ├── ChargingCoordinatorActor
      ├── TelemetryActor
      ├── SignalRBridgeActor
      └── DeadLetterMonitorActor
```

## Поток команд

```text
UI -> SignalR/HTTP -> CommandGatewayActor -> domain actors
```

`CommandGatewayActor` является единой точкой входа для внешних команд. Он валидирует команду на уровне runtime-протокола и маршрутизирует её дальше. Для проверок координат он кэширует `WorldMap`, полученный от `WorldActor` при старте через редкий admin-`ask`.

## Simulation tick

```text
SimulationClockActor -> EventStream.Publish(SimulationTick) -> subscribers
```

`SimulationClockActor` отвечает только за время симуляции: pause/resume/speed/tick dispatch.

Он не хранит мир, не знает о батареях, миссиях, маршрутах и конкретных подписчиках. `DroneActor` подписывается на `SimulationTick` в `PreStart` и отписывается в `PostStop`.

## Телеметрия

```text
DroneActor/MissionDispatcherActor/ChargingCoordinatorActor/etc.
    -> EventStream.Publish(domain event)
    -> TelemetryActor updates read model
    -> Latest WorldSnapshotDto
    -> SignalRBridgeActor
    -> SignalR Hub
    -> UI
```

Telemetry не должна вызывать `ask` на каждом UI-запросе. Она поддерживает последний известный runtime snapshot и публикует его с ограниченной частотой.

Статичная карта (`InitialWorldDto`: размер, препятствия, станции) отдаётся UI отдельно при подключении или через HTTP endpoint. `TelemetryActor` получает её при старте явным запросом к `WorldActor`, а не через `WorldEvent.WorldInitialized`, чтобы избежать startup race из-за небуферизованного `EventStream`. Runtime snapshot (`WorldSnapshotDto`) содержит только изменяемое состояние: дроны, миссии и последние события.

## Разделение ответственности

| Компонент | Ответственность |
|---|---|
| `DroneActor` | Единственный владелец состояния конкретного дрона |
| `FleetSupervisorActor` | Создание, остановка, supervision дочерних DroneActor |
| `MissionDispatcherActor` | Очередь миссий и назначение свободным дронам |
| `SimulationClockActor` | Управление временем симуляции и рассылка Tick |
| `WorldActor` | Статичная карта: размеры, препятствия, станции |
| `ChargingCoordinatorActor` | Координация очередей зарядных станций |
| `TelemetryActor` | Сбор событий и сборка последнего snapshot |
| `SignalRBridgeActor` | Изолированная отправка snapshot в SignalR |
| `CommandGatewayActor` | Runtime boundary для команд UI/API |
| `DeadLetterMonitorActor` | Наблюдение за потерянными сообщениями |

## Принцип владения состоянием

Состояние должно иметь одного владельца.

Примеры:

- батарея дрона принадлежит `DroneActor`;
- текущий маршрут дрона принадлежит `DroneActor`;
- очередь миссий принадлежит `MissionDispatcherActor`;
- очередь зарядки принадлежит `ChargingCoordinatorActor`;
- статичная карта принадлежит `WorldActor`;
- последний UI snapshot принадлежит `TelemetryActor` или отдельному read model сервису.

Запрещённый дизайн для MVP:

```text
BatteryManagerActor спрашивает DroneActor о батарее и сам решает, что дрону делать
```

Правильный дизайн MVP:

```text
DroneActor сам уменьшает BatteryLevel, сам определяет NeedCharging и публикует событие/команду
```

## Route planning

В MVP планирование маршрута не является актором.

Используется чистый F#-модуль:

```fsharp
type Route = private Route of Position list

module RouteEngine =
    val planRoute : WorldMap -> start: Position -> target: Position -> Result<Route, RoutePlanningError>
```

Actor можно добавить позже только если появится состояние: кэш, пул расчётов, cancellation, внешний сервис или распределённое планирование.

## Ask pattern

`ask` допускается для редких административных операций и тестов, но не используется как основной способ получения frequent state.

Для UI snapshot применяется read model:

```text
Events -> TelemetryActor -> LatestSnapshot -> throttled publish
```

## Mission acknowledgement

Назначение миссии не считается подтверждённым сразу после отправки команды дрону. Для защиты от зависших или неспособных принять задачу дронов используется явное событие:

```text
AssignMission -> DroneActor -> DroneMissionAcknowledged -> MissionDispatcherActor -> MissionAcknowledged
```

`DroneActor` публикует `DroneMissionAcknowledged`, потому что это факт о состоянии дрона. `MissionDispatcherActor` получает его, переводит миссию `Assigned -> PickupInProgress` и публикует `MissionAcknowledged`, потому что он владеет состоянием миссии. Если acknowledgement не пришёл за timeout, миссия возвращается в очередь или помечается failed.

## Mission progress

```text
DroneActor reaches pickup
  -> DronePickupReached
  -> MissionDispatcherActor transitions PickupInProgress -> DropoffInProgress
  -> MissionDispatcherActor publishes MissionPickupReached and MissionDropoffStarted

DroneActor reaches dropoff
  -> DroneDropoffReached
  -> MissionDispatcherActor transitions DropoffInProgress -> Completed
  -> MissionDispatcherActor publishes MissionCompleted
```

Так `DroneActor` остаётся владельцем своего движения, а `MissionDispatcherActor` — владельцем lifecycle миссии.

## Charging completion

```text
DroneActor is Charging stationId
  -> on each SimulationTick applies BatteryLevel.charge
  -> when BatteryLevel reaches 100 publishes DroneChargingCompleted
  -> ChargingCoordinatorActor releases station and assigns next queued drone
```

## Backpressure и throttling

В MVP используется простой паттерн:

- все события обновляют последний snapshot;
- таймер каждые 100-200 мс публикует актуальный snapshot;
- если событий пришло 1000, UI всё равно получает 5-10 snapshot/sec.

В следующих этапах этот слой можно заменить на Akka.Streams.


## Low battery interruption rule

При низком заряде активная миссия возвращается в управление `MissionDispatcherActor`: дрон публикует `DroneNeedsCharging`, диспетчер находит миссию, назначенную этому дрону, и переводит её обратно в `Pending` для повторного назначения. Зарядная инфраструктура реагирует на то же событие независимо: `ChargingCoordinatorActor` назначает станцию или ставит дрона в очередь.

`DroneActor` не публикует `ChargingEvent`; он публикует только `DroneEvent` о начале/завершении зарядки. `ChargingEvent` является областью ответственности `ChargingCoordinatorActor`.
