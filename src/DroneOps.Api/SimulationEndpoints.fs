module DroneOps.Api.SimulationEndpoints

open Akka.Actor
open Giraffe
open Akka.Hosting
open DroneOps.Actors.SimulationClockActor
open DroneOps.Actors.Messages

let private startHandler: HttpHandler =
    fun next ctx ->
        let registry = ctx.GetService<IActorRegistry>()
        registry.Get<SimulationClockActor>().Tell(StartSimulation, ActorRefs.NoSender)
        json {| status = "started" |} next ctx

let private pauseHandler: HttpHandler =
    fun next ctx ->
        let registry = ctx.GetService<IActorRegistry>()
        registry.Get<SimulationClockActor>().Tell(PauseSimulation, ActorRefs.NoSender)
        json {| status = "paused" |} next ctx

let private resumeHandler: HttpHandler =
    fun next ctx ->
        let registry = ctx.GetService<IActorRegistry>()
        registry.Get<SimulationClockActor>().Tell(ResumeSimulation, ActorRefs.NoSender)
        json {| status = "resumed" |} next ctx

let router: HttpHandler =
    subRoute
        "/simulation"
        (choose
            [ POST >=> route "/start" >=> startHandler
              POST >=> route "/pause" >=> pauseHandler
              POST >=> route "/resume" >=> resumeHandler ])
