# Actor model

## Общие правила

1. Actor используется только при наличии состояния, жизненного цикла или координации.
2. Состояние имеет единственного владельца.
3. Частые UI-запросы не должны использовать `ask`.
4. Имена акторов должны быть явными и стабильными.
5. Supervision strategy должна быть описана до реализации.
6. Dead letters должны мониториться с раннего этапа.
7. Доменные события и simulation ticks публикуются через `ActorSystem.EventStream`, если нет причины для прямого `tell`.

## Actor paths

```text
/user/command-gateway
/user/simulation-clock
/user/world
/user/fleet
/user/fleet/drone-001
/user/fleet/drone-002
/user/missions
/user/charging
/user/telemetry
/user/signalr-bridge
/user/dead-letter-monitor
```

## EventStream как внутренняя шина событий

Для MVP принимается единый механизм pub/sub:

```text
DroneActor / MissionDispatcherActor / SimulationClockActor
    -> ActorSystem.EventStream.Publish(event)
    -> independent subscribers
```

Преимущества:

- `DroneActor` не знает адреса `TelemetryActor`;
- `MissionDispatcherActor` может реагировать на `DroneBecameIdle` без прямой зависимости от дрона;
- `SimulationClockActor` не хранит список подписчиков;
- добавление новых read models не требует менять существующих actors.

### Типичные подписки

| Event | Publisher | Subscribers |
|---|---|---|
| `SimulationTick` | `SimulationClockActor` | `DroneActor`, `TelemetryActor`, optional debug actors |
| `DroneEvent` | `DroneActor`, `FleetSupervisorActor` | `TelemetryActor`, `MissionDispatcherActor`, `ChargingCoordinatorActor` |
| `MissionEvent` | `MissionDispatcherActor` | `TelemetryActor`, optional audit actor |
| `ChargingEvent` | `ChargingCoordinatorActor` | `TelemetryActor`, `DroneActor` for assignment replies |
| `WorldEvent` | `CommandGatewayActor`, `WorldActor` | `TelemetryActor` |

### Правило разделения DroneEvent и MissionEvent

`DroneActor` публикует только события о собственном состоянии и прогрессе дрона. Он не является владельцем жизненного цикла миссии.

Поэтому события вида "дрон принял миссию", "дрон достиг pickup" и "дрон достиг dropoff" относятся к `DroneEvent`. `MissionDispatcherActor` слушает эти события и преобразует их в `MissionEvent`, изменяя состояние миссии.

Пример:

```text
DroneActor publishes DroneMissionAcknowledged
  -> MissionDispatcherActor receives it
  -> MissionDispatcherActor transitions MissionStatus: Assigned -> PickupInProgress
  -> MissionDispatcherActor publishes MissionAcknowledged
```

Так сохраняется правило владения состоянием: дрон знает о себе и своём прогрессе, диспетчер знает о миссии.

Прямой `tell` используется для команд, где получатель является явной частью use case: например `CommandGatewayActor -> MissionDispatcherActor` или `FleetSupervisorActor -> DroneActor`.

## CommandGatewayActor

Единая точка входа для команд из API/UI.

Принимает:

- `CreateMissionCommand`
- `SpawnDroneCommand`
- `PauseSimulationCommand`
- `ResumeSimulationCommand`
- `ChangeSimulationSpeedCommand`
- `ForceReturnToBaseCommand`

Отправляет команды в `MissionDispatcherActor`, `FleetSupervisorActor`, `SimulationClockActor` и конкретные `DroneActor`.

### Runtime protocol validation

`CommandGatewayActor` валидирует не доменную физику, а корректность runtime-протокола:

- команда пришла в допустимом состоянии симуляции;
- ссылаемый `DroneId` известен `FleetSupervisorActor` или command registry;
- ссылаемый `MissionId` существует, если команда работает с существующей миссией;
- нельзя создать миссию за пределами `WorldMap`;
- нельзя вручную отправить дрон на несуществующую станцию;
- команда не противоречит режиму `Paused`, если для неё требуется активная симуляция.

Для проверки границ карты `CommandGatewayActor` получает `WorldMap` от `WorldActor` при старте через редкий admin-`ask` и кэширует размеры/статичные координаты. При будущих dynamic map-изменениях кэш обновляется через `WorldEvent`.

Глубокие доменные проверки остаются в `DroneOps.Domain` / `DroneOps.Simulation`.

## SimulationClockActor

Отвечает только за время симуляции.

Состояние:

- `IsPaused`
- `TickInterval`
- `SimulationSpeed`
- `TickNumber`

Не хранит список подписчиков.

На каждом интервале публикует событие:

```fsharp
type SimulationTick =
    {
        Tick: int64
        DeltaMs: int
        Speed: decimal
    }
```

Публикация:

```text
SimulationClockActor -> EventStream.Publish(SimulationTick)
```

Подписчики сами регистрируются и снимают подписку в lifecycle hooks.

## WorldActor

Источник правды о статичной карте.

Состояние:

- размеры карты;
- препятствия;
- координаты зарядных станций;
- статичные зоны.

Не формирует runtime snapshot и не управляет simulation tick.

`WorldMap` статична в MVP. UI получает её один раз при подключении через `InitialWorldDto` или отдельный HTTP endpoint.

## DroneActor

Единственный владелец состояния конкретного дрона.

Состояние:

- `DroneId`
- `Position`
- `BatteryLevel`
- `DroneStatus`
- `CurrentMission`
- `CurrentRoute`
- `Config`

Lifecycle:

- в `PreStart` подписывается на `SimulationTick` и `ChargingEvent`;
- в `PostStop` отписывается от `SimulationTick` и `ChargingEvent`;
- при stop/failure публикует `DroneFailed` или `DroneStopped`, если это применимо.

Семантика остановки:

- `DroneFailed` — аварийный переход в `Failed`, например потеря связи, столкновение, разряд батареи или внутренняя ошибка;
- `DroneStopped` — штатная остановка по команде оператора/системы, например `StopDroneCommand` или controlled shutdown дочернего actor.

Поведение:

- принимает назначенную миссию;
- подтверждает получение миссии событием `DroneMissionAcknowledged`;
- вызывает `RouteEngine.planRoute` как чистую функцию;
- на каждом `SimulationTick` двигается на следующий шаг;
- уменьшает батарею;
- сам принимает решение `NeedCharging`;
- публикует события телеметрии;
- сообщает о невозможности выполнить миссию;
- слушает `ChargingStationAssigned` и, если событие относится к его `DroneId`, строит маршрут к назначенной станции.

`DroneActor` не публикует `ChargingEvent`. События `DroneChargingStarted` и `DroneChargingCompleted` относятся к `DroneEvent`, потому что описывают состояние дрона. События `ChargingStationAssigned`, `ChargingStationQueued` и `ChargingStationReleased` публикует только `ChargingCoordinatorActor`.

`DroneActor` не отдаёт батарею на управление внешнему `BatteryManagerActor`.

## FleetSupervisorActor

Создаёт и контролирует `DroneActor`.

Состояние:

- registry `DroneId -> IActorRef`;
- конфигурация supervision.

### Supervision MVP

Для учебного проекта стратегия должна быть явной:

```text
DroneActor failure -> Stop child -> publish DroneFailed -> notify MissionDispatcher through EventStream
```

Причина: default restart может стереть текущее состояние дрона и создать иллюзию продолжения миссии после потери маршрута/заряда.

Позже можно сравнить стратегии:

- Restart + persistence;
- Stop + reassign mission;
- Resume только для recoverable errors.

## MissionDispatcherActor

Владелец очереди миссий.

Состояние:

- pending missions;
- assigned missions;
- completed missions;
- failed missions;
- known available drones, если используется push/event модель.

Поведение:

- принимает новые миссии;
- назначает миссии свободным дронам;
- получает события `DroneBecameIdle`, `DroneNeedsCharging`, `DroneMissionAcknowledged`, `DronePickupReached`, `DroneDropoffReached`, `DroneFailed` через `EventStream`;
- переводит миссию `Assigned -> PickupInProgress` после `DroneMissionAcknowledged`;
- переводит миссию `PickupInProgress -> DropoffInProgress` после `DronePickupReached`;
- переводит миссию `DropoffInProgress -> Completed` после `DroneDropoffReached`;
- возвращает активную миссию в очередь или помечает её failed, если дрон ушёл на зарядку;
- переназначает миссию при отказе дрона.

### Mission assignment flow

```text
CreateMissionCommand
  -> MissionDispatcherActor adds Pending mission
  -> MissionDispatcherActor selects available DroneActor
  -> DroneActor receives AssignMission
  -> DroneActor validates local ability and route
  -> DroneActor publishes DroneMissionAcknowledged
  -> MissionDispatcherActor changes MissionStatus: Assigned -> PickupInProgress
  -> MissionDispatcherActor publishes MissionAcknowledged
```

Для MVP `DroneMissionAcknowledged` означает: дрон принял миссию, построил маршрут к pickup и готов начать движение на ближайшем tick.

Если acknowledgement не пришёл за timeout, `MissionDispatcherActor` фиксирует неудачную попытку как `MissionRetryReason.AssignmentTimeout`. Далее он применяет retry policy:

```text
retryCount < maxRetryCount
  -> MissionReturnedToPending(missionId, droneId, AssignmentTimeout droneId)

retryCount >= maxRetryCount
  -> MissionFailed(missionId, RetryLimitExceeded(AssignmentTimeout droneId))
```

Именно `MissionDispatcherActor` владеет бизнес-правилом лимита попыток.


### Mission interruption by low battery

В MVP низкий заряд имеет приоритет над текущей миссией. `DroneActor` сам принимает решение, что продолжать миссию нельзя, и публикует `DroneNeedsCharging`.

```text
DroneActor executing mission detects low battery
  -> clears CurrentMission locally
  -> changes status to ReturningToCharge None
  -> publishes DroneNeedsCharging(droneId, position, battery)
MissionDispatcherActor receives DroneNeedsCharging
  -> finds active mission assigned to this drone
  -> changes MissionStatus to Pending for retry
  -> publishes MissionReturnedToPending, or MissionFailed if retry policy forbids retry
ChargingCoordinatorActor receives DroneNeedsCharging
  -> assigns station or queues drone
```

### Movement result interpretation

`Movement.fs` не формирует domain events. Он возвращает только `MovementResult`: новую позицию, остаток маршрута и факт завершения маршрута.

`DroneActor` интерпретирует результат движения в контексте своего статуса:

```text
MovingToPickup + RouteCompleted
  -> publish DronePickupReached

MovingToDropoff + RouteCompleted
  -> publish DroneDropoffReached

ReturningToCharge(Some stationId) + RouteCompleted
  -> change status to Charging stationId
  -> publish DroneChargingStarted
```

Правило порядка для всех подобных переходов:

```text
1. DroneActor вычисляет nextState.
2. DroneActor применяет become/context.Become с nextState.
3. DroneActor публикует domain event.
```

Это правило запрещает смешивать низкоуровневую механику движения и доменные события миссии/зарядки, а также исключает ситуацию, когда подписчик получил событие, но при чтении состояния дрон ещё находится в старом статусе.

Для MVP рекомендуется правило: миссия возвращается в `Pending` и может быть назначена другому дрону. Событие `MissionFailed` использовать только для финального отказа: `AssignmentTimeout`, `NoAvailableDrone`, `DroneFailed`, отмена оператором или превышение лимита retry.

Если дрон ушёл на зарядку между pickup и dropoff, для учебного MVP допустимо считать миссию невыполненной и вернуть её в `Pending`. Более реалистичная модель с состоянием груза у дрона выносится в backlog.

### Mission progress flow

```text
DroneActor reaches pickup position
  -> publishes DronePickupReached
  -> MissionDispatcherActor changes MissionStatus: PickupInProgress -> DropoffInProgress
  -> MissionDispatcherActor publishes MissionPickupReached and MissionDropoffStarted
  -> DroneActor builds route to dropoff and changes status to MovingToDropoff

DroneActor reaches dropoff position
  -> publishes DroneDropoffReached
  -> MissionDispatcherActor changes MissionStatus: DropoffInProgress -> Completed
  -> MissionDispatcherActor publishes MissionCompleted
  -> DroneActor changes status to Idle and publishes DroneBecameIdle
```

`MissionDropoffStarted` фиксирует момент, когда миссия перешла от этапа pickup к этапу доставки.

## ChargingCoordinatorActor

Упрощённая замена исходного BatteryManagerActor.

Ответственность:

- очередь дронов, ожидающих зарядки;
- назначение свободной станции;
- публикация события о назначении станции.

Не отвечает за вычисление батареи, решение о зарядке и изменение состояния батареи.

### Charging flow

```text
DroneActor detects low battery
  -> publishes DroneNeedsCharging
  -> changes status to ReturningToCharge None
ChargingCoordinatorActor receives DroneNeedsCharging
  -> selects free station or queues drone
  -> publishes ChargingStationAssigned
DroneActor receives ChargingStationAssigned for own DroneId
  -> builds route to station
  -> changes status to ReturningToCharge (Some stationId)
DroneActor arrives at station
  -> changes status to Charging stationId
  -> publishes DroneChargingStarted
DroneActor receives SimulationTick while Charging
  -> applies BatteryLevel.charge
  -> publishes DroneBatteryChanged when value changes
DroneActor reaches full charge or configured ready threshold
  -> changes status to Idle
  -> publishes DroneChargingCompleted and DroneBecameIdle
ChargingCoordinatorActor receives DroneChargingCompleted
  -> marks station free
  -> publishes ChargingStationReleased
  -> assigns next queued drone if any
```

Для MVP критерий завершения зарядки — `BatteryLevel = 100`. Позже можно заменить его на configurable ready threshold.

Семантика `ReturningToCharge None`: дрон уже решил вернуться на зарядку, но конкретная станция ещё не назначена.

Семантика `ReturningToCharge (Some id)`: станция назначена, маршрут к ней построен или строится.


При переходе `ReturningToCharge (Some stationId) -> Charging stationId` изменение состояния должно быть применено до публикации `DroneChargingStarted`. В Akka это означает: сначала новое состояние actor через `become/context.Become`, затем `EventStream.Publish`. Публикация события без предварительного commit состояния запрещена для `DroneActor`.

## TelemetryActor

Собирает события и формирует read model для UI.

Состояние:

- статичная карта `InitialWorldDto`, полученная от `WorldActor` при старте через явный admin-`ask`;
- последний known state по дронам;
- последний known state по миссиям;
- журнал последних событий ограниченного размера;
- dirty flag для snapshot;
- throttling timer.

Паттерн публикации:

```text
Event received -> update read model -> mark dirty
Timer elapsed -> if dirty then publish WorldSnapshotDto
```

`TelemetryActor` подписан на события через `EventStream`, но не является владельцем доменного состояния.

Важно: `TelemetryActor` не полагается на `WorldEvent.WorldInitialized` для первичной карты. `EventStream` не буферизует события, поэтому при старте возможна race condition: `WorldActor` может опубликовать событие до подписки `TelemetryActor`. Для начальной загрузки карты используется явный запрос к `WorldActor`; `WorldInitialized` остаётся диагностическим/будущим событием для dynamic map-сценариев.

## SignalRBridgeActor

Изолирует вызовы SignalR от остальных акторов.

Ответственность:

- получить `WorldSnapshotDto` от `TelemetryActor` через прямое сообщение или подписку на внутреннее snapshot-событие;
- вызвать `IHubContext<WorldHub>`;
- обработать исключения отправки;
- не блокировать доменных actors.

Для MVP применяется throttled snapshot: не отправлять каждое событие напрямую в SignalR, а отправлять последний накопленный snapshot раз в 100-200 мс.

## DeadLetterMonitorActor

Подписывается на Akka dead letters.

```fsharp
system.EventStream.Subscribe(deadLetterActor, typeof<DeadLetter>)
```

Задачи:

- логировать потерянные сообщения;
- помогать в отладке actor paths;
- выявлять гонки lifecycle/shutdown.

## RoutePlannerActor исключён из MVP

Планировщик маршрута в MVP — не actor.

Вместо него используется:

```text
DroneOps.Simulation.RouteEngine.planRoute
```
