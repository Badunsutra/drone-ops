module Program

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Giraffe
open DroneOps.Api.AkkaHosting
open DroneOps.Api.WorldHub

let webApp : HttpHandler =
    subRoute "/api" (
        choose [
            DroneOps.Api.SimulationEndpoints.router
            DroneOps.Api.MissionEndpoints.router
            DroneOps.Api.WorldEndpoints.router
        ])

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)

    builder.Services.AddCors(fun options ->
        options.AddDefaultPolicy(fun policy ->
            policy
                .WithOrigins("http://localhost:5173")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()  // Обязательно для SignalR WebSocket
            |> ignore))
    |> ignore

    builder.Services.AddSignalR() |> ignore
    builder.Services.AddGiraffe() |> ignore
    configureAkka builder.Services

    let app = builder.Build()

    app.UseCors()       |> ignore
    app.UseWebSockets() |> ignore
    app.UseGiraffe(webApp)
    app.MapHub<WorldHub>("/hubs/world") |> ignore

    app.Run()

    0
