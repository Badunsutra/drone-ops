# DroneOps Playground

Учебная realtime sandbox-система на F# для моделирования автономного флота дронов.

Цель проекта — изучить Akka.NET, F#, ASP.NET Core, SignalR и визуализацию состояния симуляции через Web/WPF-клиент на примере не-CRUD домена, где actor model применяется естественно.

## Ключевые принципы

- Один владелец изменяемого состояния: состояние дрона принадлежит только `DroneActor`.
- Чистая доменная логика выносится в F#-модули и тестируется без Akka.NET.
- Actor используется только там, где есть состояние, жизненный цикл, конкуренция или координация.
- Частые UI-обновления передаются через throttled snapshot, а не через `ask` на каждый запрос.
- Domain events и simulation ticks публикуются через `ActorSystem.EventStream`.
- Статичная карта (`InitialWorldDto`) отделена от runtime snapshot (`WorldSnapshotDto`).
- Для ASP.NET Core используется `Akka.Hosting`, а не ручное поднятие `ActorSystem`.
- `RoutePlannerActor` не входит в MVP: маршрутизация реализуется как чистый модуль `RouteEngine`.

## MVP

1. Backend на ASP.NET Core + F#.
2. Actor runtime через Akka.NET + Akka.Hosting.
3. 2D grid-map 20x20.
4. Несколько дронов.
5. Очередь миссий.
6. Route engine как чистая функция.
7. Заряд батареи внутри `DroneActor`.
8. `SimulationClockActor` публикует `SimulationTick` через EventStream.
9. `TelemetryActor` собирает события и формирует throttled snapshot.
10. SignalR отправляет `InitialWorldDto` и runtime `WorldSnapshotDto` в Web/WPF UI.

## Состав документации

- `docs/project_overview.md`
- `docs/stack.md`
- `docs/repository_structure.md`
- `docs/development_notes.md`
- `docs/architecture/architecture_overview.md`
- `docs/architecture/actors.md`
- `docs/domain/domain_model.md`
- `docs/planning/roadmap.md`
- `docs/planning/backlog.md`
- `docs/planning/mvp_definition.md`
- `docs/adr/*`
- `docs/changelog.md`
