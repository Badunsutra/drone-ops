# ADR 0011: Commit DroneActor state before publishing events

## Status

Accepted.

## Context

`DroneActor` converts `MovementResult + DroneStatus` into domain events such as `DronePickupReached`, `DroneDropoffReached` and `DroneChargingStarted`.

Some events describe state transitions. For example, when a drone reaches its assigned charging station, the logical transition is:

```text
ReturningToCharge (Some stationId) -> Charging stationId -> DroneChargingStarted
```

If the actor publishes `DroneChargingStarted` before committing its internal state, subscribers may process the event while the drone is still observable as `ReturningToCharge (Some stationId)`. This creates inconsistent read models and confusing telemetry.

## Decision

`DroneActor` must use the following order when a domain event depends on a state transition:

```text
1. Compute nextState.
2. Apply nextState through become/context.Become.
3. Publish the domain event to ActorSystem.EventStream.
```

`Movement.fs` remains a pure module and does not publish events. `DroneActor` is responsible for interpreting movement results and committing the corresponding state transition before publishing events.

## Consequences

- Subscribers observe events after the actor has already committed the related state change.
- `DroneChargingStarted` means the actor is already in `Charging stationId` state.
- Tests for `DroneActor` should assert both the emitted event and the resulting state.
- This rule applies to mission waypoint events and charging events produced by `DroneActor`.
