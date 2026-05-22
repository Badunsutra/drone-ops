module DroneOps.Actors.SimulationClockActor

open System
open Akka.Actor
open Akka.Event
open DroneOps.Domain.Events
open DroneOps.Actors.Messages

// Базовый интервал тика при speed=1.0
// 500мс = 2 тика в секунду
[<Literal>]
let private BaseIntervalMs = 500

type SimulationClockActor() =
    inherit UntypedActor()

    let mutable log: ILoggingAdapter = Unchecked.defaultof<_>
    let mutable isRunning = false
    let mutable tickNumber = 0L
    let mutable speed = 1.0m
    let mutable scheduled: ICancelable option = None

    // Интервал уменьшается при увеличении speed.
    // speed=2.0 → 250мс, speed=0.5 → 1000мс
    let intervalFor spd =
        TimeSpan.FromMilliseconds(float BaseIntervalMs / float spd)

    member private this.StartTicking() =
        let interval = intervalFor speed

        let c =
            UntypedActor.Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                interval,
                interval,
                this.Self,
                ClockTick,
                this.Self
            )

        scheduled <- Some c
        isRunning <- true
        log.Info("SimulationClock started — speed={0}, interval={1}ms", speed, interval.TotalMilliseconds)

    member private this.StopTicking() =
        scheduled |> Option.iter (fun c -> c.Cancel())
        scheduled <- None
        isRunning <- false
        log.Info("SimulationClock paused at tick {0}", tickNumber)

    override this.PreStart() =
        log <- Logging.GetLogger(UntypedActor.Context)
        log.Info("SimulationClock ready")

    override this.PostStop() = this.StopTicking()

    override this.OnReceive(message: obj) =
        match message with
        | :? SimulationClockCommand as cmd ->
            match cmd with
            | StartSimulation ->
                if not isRunning then
                    this.StartTicking()

            | PauseSimulation ->
                if isRunning then
                    this.StopTicking()

            | ResumeSimulation ->
                if not isRunning then
                    this.StartTicking()

            | ChangeSpeed newSpeed ->
                speed <- newSpeed
                // Перезапускаем с новым интервалом если уже работаем
                if isRunning then
                    this.StopTicking()
                    this.StartTicking()

        | :? ClockTick ->
            tickNumber <- tickNumber + 1L

            let tick =
                { Tick = tickNumber
                  DeltaMs = BaseIntervalMs
                  Speed = speed }
            // EventStream — без знания подписчиков
            UntypedActor.Context.System.EventStream.Publish(tick)

        | _ -> this.Unhandled(message)
