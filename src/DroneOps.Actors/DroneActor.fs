module DroneOps.Actors.DroneActor

open Akka.Actor
open Akka.Event
open DroneOps.Domain.Identifiers
open DroneOps.Domain.Battery
open DroneOps.Domain.Drone
open DroneOps.Domain.Events
open DroneOps.Domain.World
open DroneOps.Simulation.RouteEngine
open DroneOps.Simulation.Movement
open DroneOps.Actors.Messages

type DroneActor(initialState: DroneState, world: WorldMap) =
    inherit UntypedActor()

    let mutable log = Unchecked.defaultof<ILoggingAdapter>
    let mutable state = initialState

    member private _.Publish(event: DroneEvent) =
        UntypedActor.Context.System.EventStream.Publish(event)

    // ─── Tick handling ────────────────────────────────────────

    member private this.HandleTick(_tick: SimulationTick) =
        match state.Status with
        | Failed _
        | Idle when state.CurrentRoute.IsNone -> ()

        | Charging stationId ->
            let newBattery = BatteryLevel.charge state.Config.BatteryChargePerTick state.Battery
            state <- { state with Battery = newBattery }
            this.Publish(DroneBatteryChanged(state.Id, newBattery))

            if BatteryLevel.isFull newBattery then
                state <-
                    { state with
                        Status = Idle
                        CurrentRoute = None
                        CurrentMission = None }

                this.Publish(DroneChargingCompleted(state.Id, stationId))
                this.Publish(DroneBecameIdle state.Id)

        | _ ->
            match state.CurrentRoute with
            | None -> ()
            | Some route ->
                match moveTick route state.Battery state.Config with
                | Error _ ->
                    state <-
                        { state with
                            Status = Failed RouteNotFound
                            CurrentRoute = None }

                    this.Publish(DroneFailed(state.Id, RouteNotFound))

                | Ok result ->
                    let newBattery =
                        BatteryLevel.drain (state.Config.StepsPerTick * state.Config.BatteryDrainPerStep) state.Battery

                    state <-
                        { state with
                            Position = result.Position
                            Battery = newBattery
                            CurrentRoute = result.RemainingRoute }

                    this.Publish(DronePositionChanged(state.Id, result.Position))
                    this.Publish(DroneBatteryChanged(state.Id, newBattery))

                    if result.RouteCompleted then
                        this.HandleRouteCompleted()
                    elif DroneState.needsCharging state then
                        this.HandleLowBattery()

    member private this.HandleRouteCompleted() =
        match state.Status with
        | MovingToPickup missionId -> this.Publish(DronePickupReached(missionId, state.Id))

        | MovingToDropoff missionId ->
            state <-
                { state with
                    Status = Idle
                    CurrentMission = None }

            this.Publish(DroneDropoffReached(missionId, state.Id))
            this.Publish(DroneBecameIdle state.Id)

        | ReturningToCharge(Some stationId) ->
            state <-
                { state with
                    Status = Charging stationId }

            this.Publish(DroneChargingStarted(state.Id, stationId))

        | _ -> ()

    member private this.HandleLowBattery() =
        state <-
            { state with
                Status = ReturningToCharge None
                CurrentMission = None
                CurrentRoute = None }

        this.Publish(DroneNeedsCharging(state.Id, state.Position, state.Battery))

    // ─── Mission handling ─────────────────────────────────────

    member private this.HandleAssignMission(mission: DroneOps.Domain.Mission.Mission) =
        match planRoute world state.Position mission.Pickup with
        | Error e -> log.Warning("Drone [{0}] cannot plan route: {1}", DroneId.value state.Id, e)
        | Ok route ->
            state <-
                { state with
                    Status = MovingToPickup mission.Id
                    CurrentMission = Some mission.Id
                    CurrentRoute = Some route }

            this.Publish(DroneMissionAcknowledged(mission.Id, state.Id))

    // ─── Charging station assignment ──────────────────────────

    member private this.HandleChargingEvent(evt: ChargingEvent) =
        match evt with
        | ChargingStationAssigned(droneId, stationId, position) when droneId = state.Id ->
            match planRoute world state.Position position with
            | Error e -> log.Warning("Drone [{0}] cannot plan route to station: {1}", DroneId.value state.Id, e)
            | Ok route ->
                state <-
                    { state with
                        Status = ReturningToCharge(Some stationId)
                        CurrentRoute = Some route }
        | _ -> ()

    // ─── Lifecycle ────────────────────────────────────────────

    override this.PreStart() =
        log <- Logging.GetLogger(UntypedActor.Context)
        let es = UntypedActor.Context.System.EventStream
        es.Subscribe(this.Self, typeof<SimulationTick>) |> ignore
        es.Subscribe(this.Self, typeof<ChargingEvent>) |> ignore
        this.Publish(DroneSpawned(state.Id, state.Position))
        log.Info("DroneActor [{0}] started at {1}", DroneId.value state.Id, state.Position)

    override this.PostStop() =
        let es = UntypedActor.Context.System.EventStream
        es.Unsubscribe(this.Self, typeof<SimulationTick>) |> ignore
        es.Unsubscribe(this.Self, typeof<ChargingEvent>) |> ignore

    override this.OnReceive(message: obj) =
        match message with
        | :? SimulationTick as tick -> this.HandleTick(tick)
        | :? ChargingEvent as evt -> this.HandleChargingEvent(evt)
        | :? DroneCommand as cmd ->
            match cmd with
            | AssignMission mission -> this.HandleAssignMission(mission)
            | ForceReturnToBase -> this.HandleLowBattery()
            | StopDroneCommand -> UntypedActor.Context.Stop(this.Self)
        | _ -> this.Unhandled(message)
