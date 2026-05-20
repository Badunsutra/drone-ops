# ADR 0005: Keep route planning as a pure function in MVP

## Статус

Принято.

## Контекст

Первичная маршрутизация имеет форму `start, target, obstacles -> route`. В MVP у неё нет собственного состояния, lifecycle и supervision needs.

## Решение

Не вводить `RoutePlannerActor` в MVP. Реализовать маршрутизацию как модуль `DroneOps.Simulation.RouteEngine`.

## Следствия

- Маршрутизация проще тестируется unit-тестами.
- Actor model не используется искусственно.
- Actor можно добавить позже, если появятся кэш, тяжёлые расчёты, cancellation или внешний route service.
