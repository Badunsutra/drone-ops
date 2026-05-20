# Domain model

## Цель

Доменная модель должна быть пригодна для чистого тестирования без Akka.NET и ASP.NET Core.

Actor messages могут использовать доменные типы, но доменные типы не должны зависеть от actor runtime.

## Базовые идентификаторы

```fsharp
type DroneId = DroneId of string
type MissionId = MissionId of string
type ChargingStationId = ChargingStationId of string
type OperatorId = OperatorId of string
```

## Position

```fsharp
type Position =
    {
        X: int
        Y: int
    }
```

Для MVP используется grid-map, поэтому координаты целочисленные.

## BatteryLevel

Батарея должна иметь инвариант 0..100.

```fsharp
type BatteryLevel = private BatteryLevel of int

module BatteryLevel =
    let create percent =
        if percent >= 0 && percent <= 100 then
            Ok (BatteryLevel percent)
        else
            Error $"Invalid battery level: {percent}"

    let unsafeCreate percent =
        match create percent with
        | Ok value -> value
        | Error message -> invalidArg (nameof percent) message

    let value (BatteryLevel percent) = percent

    let drain amount (BatteryLevel percent) =
        max 0 (percent - amount) |> BatteryLevel

    let charge amount (BatteryLevel percent) =
        min 100 (percent + amount) |> BatteryLevel
```

## DroneFailureReason

Не использовать `Failed of string` для доменных причин отказа.

```fsharp
type DroneFailureReason =
    | BatteryDepleted
    | ConnectionLost
    | ObstacleCollision of position: Position
    | RouteNotFound
    | ManualAbort of operator: OperatorId
    | InternalError of message: string
```

## MissionRetryReason

```fsharp
type MissionRetryReason =
    | DroneNeedsCharging of DroneId
    | AssignmentRejected of DroneId
    | AssignmentTimeout of DroneId
```

`MissionRetryReason` означает не финальный отказ миссии, а причину неудачной попытки выполнения или назначения миссии.

Такая причина может привести к возврату миссии в `Pending`, если политика retry допускает ещё одну попытку. Решение принимает только `MissionDispatcherActor`.

## MissionFailureReason

```fsharp
type MissionFailureReason =
    | NoAvailableDrone
    | DroneFailed of DroneId * DroneFailureReason
    | RoutePlanningFailed
    | RetryLimitExceeded of lastReason: MissionRetryReason
    | CancelledByOperator of operator: OperatorId
```

`MissionFailureReason` означает финальный отказ миссии.

Важно: `AssignmentTimeout` не входит напрямую в `MissionFailureReason`. Таймаут назначения — это причина неудачной попытки (`MissionRetryReason.AssignmentTimeout`). Если лимит попыток исчерпан, `MissionDispatcherActor` публикует финальный отказ как `MissionFailed(missionId, RetryLimitExceeded lastReason)`.

Правило владения бизнес-логикой:

```text
MissionRetryReason описывает причину неудачной попытки.
MissionFailureReason описывает финальный отказ миссии.
MissionDispatcherActor единолично решает, когда retry превращается в failure.
```

## Route

`Route` должен иметь явную семантику.

Конвенция MVP:

- route не содержит текущую позицию дрона;
- первый элемент списка — следующая клетка, куда должен перейти дрон;
- последний элемент — целевая позиция.

```fsharp
type Route = private Route of Position list

module Route =
    let create steps =
        match steps with
        | [] -> Error "Route must contain at least one next position"
        | _ -> Ok (Route steps)

    let steps (Route steps) = steps

    let tryNext (Route steps) =
        match steps with
        | [] -> None
        | head :: tail -> Some (head, Route tail)
```

## DroneStatus

```fsharp
type DroneStatus =
    | Idle
    | MovingToPickup of MissionId
    | MovingToDropoff of MissionId
    | ReturningToCharge of ChargingStationId option
    | Charging of ChargingStationId
    | Failed of DroneFailureReason
```

Семантика `ReturningToCharge`:

- `None` — дрон уже решил вернуться на зарядку, но станция ещё не назначена;
- `Some stationId` — станция назначена, дрон строит или уже выполняет маршрут к ней.

## MissionPriority

Для MVP приоритеты сохраняются в доменной модели, но алгоритм диспетчеризации может стартовать с простой сортировки `Critical -> High -> Normal -> Low`, а внутри одного приоритета использовать FIFO.

```fsharp
type MissionPriority =
    | Low
    | Normal
    | High
    | Critical
```

## MissionStatus

```fsharp
type MissionStatus =
    | Pending
    | Assigned of DroneId
    | PickupInProgress of DroneId
    | DropoffInProgress of DroneId
    | Completed
    | Failed of MissionFailureReason
```

Переходы управляются `MissionDispatcherActor` на основании событий прогресса от дрона:

- `Assigned -> PickupInProgress` после `DroneMissionAcknowledged`;
- `PickupInProgress -> DropoffInProgress` после `DronePickupReached`;
- `DropoffInProgress -> Completed` после `DroneDropoffReached`.

Для MVP `DroneMissionAcknowledged` означает, что дрон принял миссию, построил маршрут к pickup и начнёт движение на ближайшем `SimulationTick`.

## DroneConfig

Тип должен быть явно описан, так как используется в `SpawnDrone`.

```fsharp
type DroneConfig =
    {
        Id: DroneId
        InitialPosition: Position
        InitialBattery: BatteryLevel
        BatteryDrainPerStep: int
        BatteryChargePerTick: int
        LowBatteryThreshold: BatteryLevel
        MaxPayloadKg: decimal option
    }
```

## DroneState

```fsharp
type DroneState =
    {
        Id: DroneId
        Position: Position
        Battery: BatteryLevel
        Status: DroneStatus
        CurrentMission: MissionId option
        CurrentRoute: Route option
        Config: DroneConfig
    }
```

## Mission

```fsharp
type Mission =
    {
        Id: MissionId
        Pickup: Position
        Dropoff: Position
        PayloadKg: decimal option
        Priority: MissionPriority
        Status: MissionStatus
    }
```

## WorldMap

```fsharp
type WorldMap =
    {
        Width: int
        Height: int
        Obstacles: Set<Position>
        ChargingStations: Map<ChargingStationId, Position>
    }
```

## Domain events

События лежат в `Events.fs`. Это типизированный внутренний протокол системы, который публикуется через `ActorSystem.EventStream`.

### SimulationTick

```fsharp
type SimulationTick =
    {
        Tick: int64
        DeltaMs: int
        Speed: decimal
    }
```

### DroneEvent

`DroneEvent` описывает только состояние и прогресс конкретного дрона. Эти события публикует `DroneActor` или `FleetSupervisorActor`.

```fsharp
type DroneEvent =
    | DroneSpawned of droneId: DroneId * position: Position
    | DroneBecameIdle of droneId: DroneId
    | DronePositionChanged of droneId: DroneId * position: Position
    | DroneBatteryChanged of droneId: DroneId * battery: BatteryLevel
    | DroneNeedsCharging of droneId: DroneId * position: Position * battery: BatteryLevel
    | DroneMissionAcknowledged of missionId: MissionId * droneId: DroneId
    | DronePickupReached of missionId: MissionId * droneId: DroneId
    | DroneDropoffReached of missionId: MissionId * droneId: DroneId
    | DroneChargingStarted of droneId: DroneId * stationId: ChargingStationId
    | DroneChargingCompleted of droneId: DroneId * stationId: ChargingStationId
    | DroneFailed of droneId: DroneId * reason: DroneFailureReason
    | DroneStopped of droneId: DroneId
```

`DroneStopped` означает штатную остановку по команде оператора/системы. `DroneFailed` означает аварийное завершение или переход в `Failed`.

### MissionEvent

`MissionEvent` публикует `MissionDispatcherActor`, потому что именно он владеет состоянием миссии. Он преобразует `DroneMissionAcknowledged`, `DronePickupReached` и `DroneDropoffReached` в изменения `MissionStatus` и соответствующие события миссии.

```fsharp
type MissionEvent =
    | MissionCreated of mission: Mission
    | MissionAssigned of missionId: MissionId * droneId: DroneId
    | MissionAcknowledged of missionId: MissionId * droneId: DroneId
    | MissionPickupReached of missionId: MissionId * droneId: DroneId
    | MissionDropoffStarted of missionId: MissionId * droneId: DroneId
    | MissionCompleted of missionId: MissionId * droneId: DroneId
    | MissionReturnedToPending of missionId: MissionId * previousDroneId: DroneId * reason: MissionRetryReason
    | MissionFailed of missionId: MissionId * reason: MissionFailureReason
```

### ChargingEvent

```fsharp
type ChargingEvent =
    | ChargingStationAssigned of droneId: DroneId * stationId: ChargingStationId * position: Position
    | ChargingStationQueued of droneId: DroneId
    | ChargingStationReleased of stationId: ChargingStationId
```

### WorldEvent

```fsharp
type WorldEvent =
    | WorldInitialized of map: WorldMap
    | ObstacleAdded of position: Position
    | ObstacleRemoved of position: Position
```

Для MVP `WorldMap` считается статичным после запуска. `ObstacleAdded` и `ObstacleRemoved` можно оставить в backlog или использовать только в будущих этапах.

## RouteEngine

```fsharp
type RoutePlanningError =
    | StartOutsideMap
    | TargetOutsideMap
    | TargetBlocked
    | NoPathFound

module RouteEngine =
    val planRoute : WorldMap -> start: Position -> target: Position -> Result<Route, RoutePlanningError>
```

## Movement

`Movement.fs` содержит чистую механику движения на один шаг по маршруту.

`Movement.fs` не создаёт `DroneEvent`, `MissionEvent` или другие domain events. Он возвращает только результат движения. `DroneActor` интерпретирует этот результат вместе со своим текущим `DroneStatus` и сам публикует нужные события.

Примерная модель результата:

```fsharp
type MovementResult =
    {
        Position: Position
        RemainingRoute: Route
        ReachedWaypoint: Position option
        RouteCompleted: bool
    }

module Movement =
    val moveOneStep : position: Position -> route: Route -> Result<MovementResult, DroneFailureReason>
```

Пример интерпретации в `DroneActor`:

```fsharp
match state.Status, movement.RouteCompleted with
| MovingToPickup missionId, true ->
    let nextState = { state with Position = movement.Position; Route = movement.RemainingRoute }
    become nextState
    publish (DronePickupReached(state.Id, missionId))

| MovingToDropoff missionId, true ->
    let nextState = { state with Position = movement.Position; Route = movement.RemainingRoute }
    become nextState
    publish (DroneDropoffReached(state.Id, missionId))

| ReturningToCharge (Some stationId), true ->
    let nextState =
        { state with
            Position = movement.Position
            Route = movement.RemainingRoute
            Status = Charging stationId }

    become nextState
    publish (DroneChargingStarted(state.Id, stationId))

| _ ->
    let nextState = { state with Position = movement.Position; Route = movement.RemainingRoute }
    become nextState
```

Правило порядка:

```text
DroneActor сначала фиксирует новое состояние через become/context.Become.
Только после этого он публикует domain event в EventStream.
```

Это особенно важно для `DroneChargingStarted`: к моменту обработки события подписчиками дрон уже должен находиться в статусе `Charging stationId`, а не `ReturningToCharge (Some stationId)`.

Правило:

```text
Movement.fs отвечает на вопрос: куда дрон переместился за один tick.
DroneActor отвечает на вопрос: какое доменное событие означает это перемещение.
```

Так исключается дублирование или потеря событий `DronePickupReached`, `DroneDropoffReached` и `DroneChargingStarted`.

## UI DTO

Доменные типы не обязаны напрямую сериализоваться в UI JSON. Для UI используются DTO/read model.

### InitialWorldDto

Статичная карта отправляется UI один раз при подключении или отдаётся через HTTP endpoint.

```fsharp
type ChargingStationDto =
    {
        Id: string
        X: int
        Y: int
    }

type ObstacleDto =
    {
        X: int
        Y: int
    }

type InitialWorldDto =
    {
        Width: int
        Height: int
        Obstacles: ObstacleDto list
        Stations: ChargingStationDto list
    }
```

### Runtime snapshot

Runtime snapshot содержит изменяемое состояние.

```fsharp
type DroneViewDto =
    {
        Id: string
        X: int
        Y: int
        BatteryPercent: int
        Status: string
        CurrentMissionId: string option
    }

type MissionViewDto =
    {
        Id: string
        PickupX: int
        PickupY: int
        DropoffX: int
        DropoffY: int
        Priority: string
        Status: string
    }

type EventViewDto =
    {
        Kind: string
        Message: string
        Tick: int64 option
    }

type WorldSnapshotDto =
    {
        Tick: int64
        Drones: DroneViewDto list
        Missions: MissionViewDto list
        RecentEvents: EventViewDto list
    }
```

Причина разделения: `Obstacles` и `Stations` статичны в MVP, поэтому нет смысла гонять их в каждом throttled snapshot.
