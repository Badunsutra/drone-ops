# ADR-0008: Разделить DroneEvent и MissionEvent по владельцу состояния

## Статус

Принято.

## Контекст

`DroneActor` знает о собственном движении, батарее, маршруте и локальном прогрессе. `MissionDispatcherActor` владеет очередью миссий и их lifecycle-состоянием.

Если `DroneActor` напрямую публикует `MissionEvent`, возникает смешение ответственности: дрон начинает косвенно управлять состоянием миссии, хотя не является владельцем этого состояния.

## Решение

`DroneActor` публикует события прогресса как `DroneEvent`:

- `DroneMissionAcknowledged`;
- `DronePickupReached`;
- `DroneDropoffReached`.

`MissionDispatcherActor` подписывается на эти события, изменяет `MissionStatus` и публикует соответствующие `MissionEvent`:

- `MissionAcknowledged`;
- `MissionPickupReached`;
- `MissionDropoffStarted`;
- `MissionCompleted`.

## Последствия

Плюсы:

- сохраняется принцип единственного владельца состояния;
- таблица `EventStream` становится однозначной;
- проще тестировать `MissionDispatcherActor` как state machine миссий.

Минусы:

- появляется дополнительный шаг преобразования события;
- требуется аккуратно описывать timeout и idempotency для событий прогресса.
