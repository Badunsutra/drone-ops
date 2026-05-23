module DroneOps.Api.SignalRBridgeActor

open Akka.Actor
open Akka.Event
open Microsoft.AspNetCore.SignalR
open DroneOps.Domain.Dtos
open DroneOps.Actors.ActorNames
open DroneOps.Api.WorldHub

// SignalRBridgeActor изолирует вызовы SignalR от остальных акторов.
// Подписывается на WorldSnapshotDto из EventStream и пушит клиентам.
// Если отправка упала — только логируем, не блокируем mailbox.
type SignalRBridgeActor(hubContext: IHubContext<WorldHub>) =
    inherit UntypedActor()

    let mutable log = Unchecked.defaultof<ILoggingAdapter>

    override this.PreStart() =
        log <- Logging.GetLogger(UntypedActor.Context)
        UntypedActor.Context.System.EventStream.Subscribe(this.Self, typeof<WorldSnapshotDto>)
        |> ignore
        log.Info("SignalRBridge started")

    override this.PostStop() =
        UntypedActor.Context.System.EventStream.Unsubscribe(this.Self, typeof<WorldSnapshotDto>)
        |> ignore

    override this.OnReceive(message: obj) =
        match message with
        | :? WorldSnapshotDto as snapshot ->
            // Fire-and-forget: не блокируем mailbox ожиданием Task.
            // Для MVP достаточно — данные телеметрии не критичны к потере одного кадра.
            hubContext.Clients.All.SendAsync("worldSnapshot", snapshot)
            |> ignore

        | _ -> this.Unhandled(message)
