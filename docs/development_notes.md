# Development notes

## Практические правила

1. Сначала пишется доменная логика без Akka.NET.
2. Actor добавляется только после появления состояния, lifecycle или координации.
3. Один mutable state — один владелец.
4. Не использовать `ask` в hot path телеметрии.
5. Для UI использовать throttled snapshot.
6. Actor paths задавать явно.
7. Dead letter monitoring включить на раннем этапе.
8. Supervision strategy описывать до реализации actor.
9. Route planning в MVP держать как чистую функцию.
10. DTO для UI не смешивать с доменными типами.
11. Domain events и simulation ticks публиковать через `ActorSystem.EventStream`.
12. Статичную карту отправлять как `InitialWorldDto`, runtime-состояние — как `WorldSnapshotDto`.

## Частота обновления UI

Рекомендуемый интервал публикации snapshot: 100-200 мс.

Паттерн:

```text
many domain events -> update latest read model -> publish latest snapshot by timer
```

Такой подход предотвращает перегрузку SignalR и UI.

## Ошибки и supervision

Для `DroneActor` в MVP предпочтительно:

```text
failure -> Stop -> DroneFailed event -> MissionDispatcher reassigns mission
```

Default restart не использовать без осознанной причины, потому что actor может потерять transient state: маршрут, текущую миссию, внутренний прогресс.

## Ask pattern

Разрешено:

- debug endpoints;
- integration tests;
- редкие административные запросы.

Не использовать:

- каждый UI frame;
- сбор snapshot с каждого дрона каждые 100 мс;
- частую диспетчеризацию батареи.

## Dead letters

Добавить `DeadLetterMonitorActor` на раннем этапе. Это особенно полезно при явных actor paths и lifecycle-сценариях: остановка дрона, перезапуск симуляции, завершение миссии.

## EventStream conventions

В MVP `ActorSystem.EventStream` используется как внутренняя шина для domain events и `SimulationTick`.

Правило:

```text
commands -> direct tell
events -> EventStream.Publish
```

Пример: `CommandGatewayActor` отправляет команду `CreateMissionCommand` напрямую в `MissionDispatcherActor`, но `MissionDispatcherActor` публикует `MissionCreated` через `EventStream`.

## InitialWorldDto и WorldSnapshotDto

`WorldMap` статична в MVP, поэтому UI получает препятствия и станции один раз:

```text
client connected -> request InitialWorldDto -> render static map
then SignalR WorldSnapshotDto -> update drones/missions/events
```

Так runtime snapshot остаётся компактным и не дублирует карту каждые 100-200 мс.

## Movement.fs

`Movement.fs` — чистый simulation-модуль, который двигает дрон на один шаг по `Route`. Он не знает про actors, missions, charging flow, SignalR и domain events. Это позволяет покрыть движение обычными unit-тестами до интеграции с Akka.NET.

Принятое правило для реализации:

```text
Movement.moveOneStep не возвращает DroneEvent list.
Movement.moveOneStep возвращает MovementResult.
DroneActor преобразует MovementResult + DroneStatus в DroneEvent.
```

Это особенно важно для событий достижения контрольных точек: `DronePickupReached`, `DroneDropoffReached`, `DroneChargingStarted`. Их должен публиковать только `DroneActor`, а не `Movement.fs`.


## DroneActor state commit before publish

При обработке `MovementResult` `DroneActor` обязан сначала применить новое состояние, а только потом публиковать domain event.

```text
nextState = reduce(state, movementResult)
become/context.Become(nextState)
EventStream.Publish(domainEvent)
```

Для `DroneChargingStarted` это обязательное правило: перед публикацией события статус должен быть уже изменён с `ReturningToCharge (Some stationId)` на `Charging stationId`. Иначе подписчики могут обработать событие и увидеть устаревшее состояние дрона.

## Startup state and EventStream

`ActorSystem.EventStream` не буферизует события. Поэтому события, опубликованные до подписки actor, будут потеряны для этого actor.

Для первичного состояния нельзя полагаться на `WorldEvent.WorldInitialized`. Использовать явные редкие admin-запросы:

- `TelemetryActor -> WorldActor`: получить `WorldMap` и собрать `InitialWorldDto`;
- `CommandGatewayActor -> WorldActor`: получить границы карты для runtime validation.

Это не противоречит ограничению на `ask`, потому что речь не о frequent UI snapshot, а о startup/admin state.

## Actor startup order

`TelemetryActor` и `CommandGatewayActor` выполняют startup/admin-`ask` к `WorldActor`, поэтому `WorldActor` должен быть зарегистрирован и создан раньше этих акторов. В `Akka.Hosting` это нужно зафиксировать порядком регистрации hosted actors или отдельным bootstrap actor.

Для устойчивого старта рекомендуется не полагаться только на порядок создания, а добавить retry/backoff для startup-запросов к `WorldActor`. Если `WorldActor` временно недоступен, `TelemetryActor` и `CommandGatewayActor` должны повторить запрос, а не считать отсутствие карты нормальным состоянием.

## Mission retry and final failure

`MissionRetryReason` и `MissionFailureReason` намеренно разделены.

- `MissionRetryReason` описывает неудачную попытку: низкий заряд, отказ дрона принять назначение, timeout acknowledgement.
- `MissionFailureReason` описывает финальный отказ миссии: отмена оператором, отсутствие доступных дронов, исчерпанный лимит попыток.

`AssignmentTimeout` должен существовать только как `MissionRetryReason.AssignmentTimeout`. Если таймаут приводит к финальному отказу, он оборачивается в `MissionFailureReason.RetryLimitExceeded lastReason`.

Счётчик попыток и правило `retry -> failure` принадлежат `MissionDispatcherActor`.
