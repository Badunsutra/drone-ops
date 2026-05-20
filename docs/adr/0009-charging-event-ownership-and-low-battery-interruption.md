# ADR 0009: Владение ChargingEvent и прерывание миссии при низком заряде

Статус: принято

## Контекст

В actor model важно, чтобы событие публиковал владелец соответствующей области состояния. В DroneOps дрон владеет своим состоянием и батареей, а координатор зарядки владеет очередью и занятостью зарядных станций.

Также нужен явный сценарий, что происходит с активной миссией, если дрон уходит на зарядку во время выполнения задания.

## Решение

`DroneActor` публикует только `DroneEvent`, включая `DroneChargingStarted` и `DroneChargingCompleted`. Эти события описывают состояние дрона.

`ChargingCoordinatorActor` единственный публикует `ChargingEvent`: `ChargingStationAssigned`, `ChargingStationQueued`, `ChargingStationReleased`. Эти события описывают состояние зарядочной инфраструктуры.

`DroneActor` подписывается на `ChargingEvent`, чтобы получить `ChargingStationAssigned` для своего `DroneId` и перейти из `ReturningToCharge None` в `ReturningToCharge (Some stationId)`.

При низком заряде во время активной миссии `DroneActor` публикует `DroneNeedsCharging` и очищает `CurrentMission`. `MissionDispatcherActor` слушает это событие, находит активную миссию дрона и для MVP возвращает её в `Pending`, публикуя `MissionReturnedToPending`.

## Следствия

- Charging flow не требует прямой зависимости `DroneActor -> ChargingCoordinatorActor`.
- Станции и их очередь остаются во владении `ChargingCoordinatorActor`.
- Mission retry policy становится явной и может быть усложнена позже.
- Для реалистичного сценария pickup/dropoff позже потребуется модель груза у дрона или отдельный cargo actor.
