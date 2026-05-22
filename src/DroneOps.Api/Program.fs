module Program

open Microsoft.AspNetCore.Builder
open Giraffe
open DroneOps.Api.AkkaHosting

let webApp: HttpHandler =
    choose
        [ subRoute
              "/api"
              (choose
                  [ DroneOps.Api.SimulationEndpoints.router
                    DroneOps.Api.MissionEndpoints.router ])
          RequestErrors.NOT_FOUND "Not found" ]

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)

    builder.Services.AddGiraffe() |> ignore
    configureAkka builder.Services

    let app = builder.Build()

    app.UseGiraffe webApp

    app.Run()

    0
