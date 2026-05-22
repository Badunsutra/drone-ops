module DroneOps.Api.MissionEndpoints

open Giraffe
open Akka.Actor
open Akka.Hosting
open DroneOps.Domain.Identifiers
open DroneOps.Domain.Mission
open DroneOps.Actors.MissionDispatcherActor
open DroneOps.Actors.Messages

[<CLIMutable>]
type CreateMissionRequest =
    { PickupX: int
      PickupY: int
      DropoffX: int
      DropoffY: int
      Priority: string }

// task {} — вычислительное выражение для async handler.
// BindJsonAsync десериализует тело запроса в наш DTO.
let private createMissionHandler: HttpHandler =
    fun next ctx ->
        task {
            let! req = ctx.BindJsonAsync<CreateMissionRequest>()

            let priority =
                match req.Priority with
                | "critical" -> Critical
                | "high" -> High
                | "low" -> Low
                | _ -> Normal

            let mission =
                Mission.create
                    (MissionId.generate ())
                    { X = req.PickupX; Y = req.PickupY }
                    { X = req.DropoffX; Y = req.DropoffY }
                    priority

            ctx
                .GetService<IActorRegistry>()
                .Get<MissionDispatcherActor>()
                .Tell(CreateMission mission, ActorRefs.NoSender)

            ctx.SetStatusCode 202
            return! json {| id = MissionId.value mission.Id |} next ctx
        }

let router: HttpHandler =
    subRoute "/missions" (POST >=> route "" >=> createMissionHandler)
