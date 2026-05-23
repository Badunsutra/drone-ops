module DroneOps.Api.WorldEndpoints

open Giraffe
open DroneOps.Domain.World
open DroneOps.Domain.Dtos

let router : HttpHandler =
    subRoute "/world" (
        GET >=> route "" >=> fun next ctx ->
            let world = ctx.GetService<WorldMap>()
            let dto   = Mapping.toInitialWorldDto world
            json dto next ctx)
