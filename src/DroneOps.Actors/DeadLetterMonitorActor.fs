module DroneOps.Actors.DeadLetterMonitorActor

open Akka.Actor
open Akka.Event

type DeadLetterMonitorActor() =
    inherit UntypedActor()

    let mutable log: ILoggingAdapter = Unchecked.defaultof<_>

    override this.PreStart() =
        // Context — protected static на ActorBase.
        // В F# static члены базового класса доступны через имя класса, не через this.
        log <- Logging.GetLogger(UntypedActor.Context)

        UntypedActor.Context.System.EventStream.Subscribe(this.Self, typeof<DeadLetter>)
        |> ignore

        log.Info("DeadLetterMonitor started")

    override this.PostStop() =
        UntypedActor.Context.System.EventStream.Unsubscribe(this.Self, typeof<DeadLetter>)
        |> ignore

    override this.OnReceive(message: obj) =
        match message with
        | :? DeadLetter as dl ->
            log.Warning("Dead letter: [{0}] from [{1}] to [{2}]", dl.Message, dl.Sender, dl.Recipient)
        | _ -> this.Unhandled(message)
