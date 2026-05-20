# Changelog

## v7

- Зафиксировано правило реализации `DroneActor`: сначала применить новое состояние через `become/context.Become`, затем публиковать domain event.
- Уточнён переход `ReturningToCharge (Some stationId) -> Charging stationId -> DroneChargingStarted`.
- Обновлён пример интерпретации `MovementResult` в `domain_model.md`.
- Добавлен ADR `0011-commit-drone-state-before-publishing-events.md`.

## v6

- Уточнено разделение `MissionRetryReason` и `MissionFailureReason`.
- Убран прямой `AssignmentTimeout` из `MissionFailureReason`; финальный отказ по таймауту теперь выражается через `RetryLimitExceeded lastReason`.
- Зафиксировано, что retry/failure policy и счётчик попыток принадлежат `MissionDispatcherActor`.
- Уточнено, что `Movement.fs` не генерирует `DroneEvent list`.
- Добавлен `MovementResult` как рекомендуемый результат `Movement.moveOneStep`.
- Зафиксировано правило: `DroneActor` преобразует `MovementResult + DroneStatus` в domain events.
- Добавлен ADR `0010-separate-retry-reasons-and-movement-events.md`.

## v5

- Исправлена таблица `EventStream`: `ChargingEvent` публикует только `ChargingCoordinatorActor`.
- Уточнено, что `DroneActor` подписывается на `SimulationTick` и `ChargingEvent`, а в `PostStop` отписывается от обоих типов событий.
- Зафиксировано, что `DroneActor` не публикует `ChargingEvent`; `DroneChargingStarted` и `DroneChargingCompleted` являются `DroneEvent`.
- Описан сценарий прерывания активной миссии при низком заряде: `DroneNeedsCharging -> MissionReturnedToPending -> retry assignment`.
- Добавлен `MissionRetryReason` и событие `MissionReturnedToPending`.
- Добавлена рекомендация по порядку старта actors: `WorldActor` должен быть доступен до startup/admin-`ask` от `TelemetryActor` и `CommandGatewayActor`; для устойчивости рекомендуется retry/backoff.
- Добавлен ADR `0009-charging-event-ownership-and-low-battery-interruption.md`.

## v4

- Разделена ответственность `DroneEvent` и `MissionEvent`: `DroneActor` публикует факты о себе, `MissionDispatcherActor` преобразует их в lifecycle-события миссий.
- Исправлена таблица `EventStream`: `MissionEvent` публикует только `MissionDispatcherActor`.
- Добавлены `DroneMissionAcknowledged`, `DronePickupReached`, `DroneDropoffReached`.
- Описан flow `PickupInProgress -> DropoffInProgress -> Completed`.
- Дописан полный flow зарядки до `DroneChargingCompleted` и `ChargingStationReleased`.
- Зафиксировано, что `TelemetryActor` и `CommandGatewayActor` получают `WorldMap` через startup/admin-`ask`, а не через `WorldInitialized`.
- Уточнена семантика `DroneStopped` vs `DroneFailed`.
- Уточнено, откуда `CommandGatewayActor` берёт карту для runtime validation.

## 2026-05-20 — архитектурная ревизия

Приняты корректировки:

- Убрано противоречие владения батареей: `DroneActor` является единственным владельцем `BatteryLevel`.
- `BatteryManagerActor` заменён на `ChargingCoordinatorActor`.
- `RoutePlannerActor` исключён из MVP; маршрутизация стала чистым модулем `RouteEngine`.
- `WorldActor` разгружен: теперь он отвечает только за статичную карту.
- Добавлен `SimulationClockActor` для управления tick/pause/resume/speed.
- `TelemetryActor` закреплён как read model/snapshot builder.
- Добавлен throttled snapshot pattern для SignalR.
- Добавлен `CommandGatewayActor` в список акторов и архитектурную схему.
- Зафиксирован `Akka.Hosting` как способ интеграции с ASP.NET Core.
- Уточнено, что Akka.FSharp legacy actor CE не используется как основной подход MVP.
- Добавлен `DeadLetterMonitorActor`.
- Зафиксированы явные actor paths.
- Добавлена supervision strategy для `FleetSupervisorActor`: stop child + notify dispatcher.
- Добавлены `BatteryLevel` smart constructor, `DroneConfig`, typed failure reasons и явная семантика `Route`.

## v3

- Добавлен `InitialWorldDto` для статичной карты: размеры, препятствия, зарядные станции.
- `WorldSnapshotDto` оставлен для runtime-состояния: дроны, миссии, события.
- Зафиксирован `ActorSystem.EventStream` как механизм pub/sub для domain events и `SimulationTick`.
- `SimulationClockActor` больше не хранит список подписчиков; подписчики управляют lifecycle-подпиской сами.
- Описан flow `AssignMission -> DroneMissionAcknowledged -> MissionAcknowledged -> PickupInProgress`.
- В `domain_model.md` возвращены типизированные `DroneEvent`, `MissionEvent`, `WorldEvent`; добавлен `ChargingEvent`.
- Уточнена семантика `ReturningToCharge None/Some`.
- Возвращён `MissionPriority`; для MVP выбран порядок приоритетов с FIFO внутри одного приоритета.
- Описан смысл `Movement.fs`.
- Уточнена runtime protocol validation в `CommandGatewayActor`.
- Добавлен ADR `0007-use-eventstream-for-domain-events.md`.
