# ADR 0007: Использовать ActorSystem.EventStream для domain events

## Статус

Принято.

## Контекст

`DroneActor`, `MissionDispatcherActor`, `ChargingCoordinatorActor`, `TelemetryActor` и `SimulationClockActor` должны обмениваться событиями. Если использовать только прямой `tell`, акторы начнут знать адреса друг друга, а добавление нового подписчика потребует менять существующих publishers.

Особенно проблемны два случая:

- `DroneActor` публикует телеметрию и события занятости, но не должен знать о `TelemetryActor` и `MissionDispatcherActor`;
- `SimulationClockActor` не должен хранить список всех `DroneActor` и синхронизировать подписки при spawn/stop.

## Решение

Для событий использовать `ActorSystem.EventStream`.

Правило:

```text
commands -> direct tell
events -> EventStream.Publish
```

`SimulationClockActor` публикует `SimulationTick` через EventStream.

`DroneActor` подписывается на `SimulationTick` в `PreStart` и отписывается в `PostStop`.

`TelemetryActor`, `MissionDispatcherActor`, `ChargingCoordinatorActor` подписываются на нужные domain events независимо друг от друга.

## Последствия

Плюсы:

- меньше coupling между actors;
- проще добавлять read models, логирование и audit;
- `SimulationClockActor` не знает о конкретных дронах;
- lifecycle подписчиков локализован внутри самих actors.

Минусы:

- event flow менее явно виден из constructor dependencies;
- требуется аккуратно документировать список событий и подписчиков;
- ошибки типа события могут проявиться как отсутствие реакции, поэтому нужен `DeadLetterMonitorActor` и хорошие integration tests.
