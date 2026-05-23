module Program

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Giraffe
open DroneOps.Api.AkkaHosting
open DroneOps.Api.WorldHub
open DroneOps.Api.SimulationEndpoints
open DroneOps.Api.MissionEndpoints

// Без RequestErrors.NOT_FOUND — Giraffe вернёт None для неизвестных путей
// и ASP.NET Core передаст запрос следующему обработчику (SignalR hub)
let webApp : HttpHandler =
    subRoute "/api" (
        choose [
            DroneOps.Api.SimulationEndpoints.router
            DroneOps.Api.MissionEndpoints.router
        ])

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)

    builder.Services.AddSignalR() |> ignore
    builder.Services.AddGiraffe() |> ignore
    configureAkka builder.Services

    let app = builder.Build()

    app.UseWebSockets()                         |> ignore
    app.UseGiraffe(webApp)                          // Middleware: /api/*
    app.MapHub<WorldHub>("/hubs/world")         |> ignore  // Endpoint: SignalR

    app.Run()

    0
