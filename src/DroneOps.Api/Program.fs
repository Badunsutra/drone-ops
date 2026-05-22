module Program

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open DroneOps.Api.AkkaHosting

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)

    builder.Services.AddControllers() |> ignore
    configureAkka builder.Services

    let app = builder.Build()

    app.UseHttpsRedirection() |> ignore
    app.MapControllers() |> ignore

    app.Run()

    0
