# ADR 0004: Use Akka.Hosting for ASP.NET Core integration

## Статус

Принято.

## Контекст

ActorSystem должен жить внутри ASP.NET Core backend. Ручное создание ActorSystem усложняет lifetime management, DI, logging и конфигурацию.

## Решение

Использовать `Akka.Hosting` для интеграции Akka.NET с ASP.NET Core.

## Следствия

- ActorSystem управляется host lifecycle.
- Упрощается регистрация actors и props.
- Проще подключать DI-сервисы, включая `IHubContext` для SignalR.
- Документация и код стартуют с современного подхода Akka.NET.
