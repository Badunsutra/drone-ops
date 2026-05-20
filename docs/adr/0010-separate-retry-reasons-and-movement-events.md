# ADR 0010: Separate retry reasons and movement events

## Статус

Принято.

## Контекст

В модели миссий есть два похожих, но разных сценария:

- неудачная попытка назначения или выполнения миссии, после которой миссию можно вернуть в очередь;
- финальный отказ миссии, после которого она больше не должна назначаться автоматически.

Также модуль `Movement.fs` отвечает за низкоуровневую механику движения. Если он начнёт возвращать `DroneEvent list`, возникнет риск смешать чистую simulation-логику с доменными событиями `DroneActor`.

## Решение

1. `MissionRetryReason` описывает причину неудачной попытки.
2. `MissionFailureReason` описывает финальный отказ миссии.
3. Таймаут назначения хранится только как `MissionRetryReason.AssignmentTimeout`.
4. Финальный отказ из-за исчерпания попыток выражается как `MissionFailureReason.RetryLimitExceeded of lastReason: MissionRetryReason`.
5. `MissionDispatcherActor` единолично владеет счётчиком попыток и правилом перехода `retry -> failure`.
6. `Movement.fs` не создаёт domain events.
7. `Movement.moveOneStep` возвращает `MovementResult`.
8. `DroneActor` преобразует `MovementResult + DroneStatus` в `DroneEvent`.

## Последствия

Плюсы:

- retry/failure policy становится явно локализованной в `MissionDispatcherActor`;
- `AssignmentTimeout` не дублируется в двух типах ошибок;
- `Movement.fs` остаётся чистым, маленьким и легко тестируемым;
- события достижения pickup/dropoff/charging не дублируются между simulation-модулем и actor-логикой.

Минусы:

- `DroneActor` должен явно интерпретировать результат движения;
- `MissionDispatcherActor` должен хранить retry count по миссиям.
