module DroneOps.Domain.Mission

open Identifiers
open Position
open Drone

// ─── Retry and failure reasons ────────────────────────────────

// MissionRetryReason — причина неудачной ПОПЫТКИ.
// Не финальный отказ — миссия может вернуться в Pending.
// Решение о retry принимает только MissionDispatcherActor.
type MissionRetryReason =
    | DroneNeedsCharging of droneId: DroneId
    | AssignmentRejected of droneId: DroneId
    | AssignmentTimeout of droneId: DroneId

// MissionFailureReason — ФИНАЛЬНЫЙ отказ миссии.
// После этого миссия не назначается повторно автоматически.
//
// Важно: AssignmentTimeout не входит сюда напрямую.
// Таймаут — это retry reason. Если лимит исчерпан,
// MissionDispatcherActor оборачивает его в RetryLimitExceeded.
type MissionFailureReason =
    | NoAvailableDrone
    | DroneFailed of droneId: DroneId * reason: DroneFailureReason
    | RoutePlanningFailed
    | RetryLimitExceeded of lastReason: MissionRetryReason
    | CancelledByOperator of operator: OperatorId

// ─── Priority ─────────────────────────────────────────────────

// Порядок имеет значение для сортировки:
// Critical > High > Normal > Low.
// F# DU сравниваются по порядку объявления — Low < Normal < High < Critical.
type MissionPriority =
    | Low
    | Normal
    | High
    | Critical

// ─── Status ───────────────────────────────────────────────────

// MissionStatus — state machine миссии.
// Переходами управляет MissionDispatcherActor
// на основании DroneEvent из EventStream.
//
// Pending -> Assigned                  после назначения дрону
// Assigned -> PickupInProgress         после DroneMissionAcknowledged
// PickupInProgress -> DropoffInProgress после DronePickupReached
// DropoffInProgress -> Completed        после DroneDropoffReached
type MissionStatus =
    | Pending
    | Assigned of droneId: DroneId
    | PickupInProgress of droneId: DroneId
    | DropoffInProgress of droneId: DroneId
    | Completed
    | Failed of reason: MissionFailureReason

// ─── Mission ──────────────────────────────────────────────────

type Mission =
    { Id: MissionId
      Pickup: Position
      Dropoff: Position
      PayloadKg: decimal option
      Priority: MissionPriority
      Status: MissionStatus }

module Mission =

    let create id pickup dropoff priority =
        { Id = id
          Pickup = pickup
          Dropoff = dropoff
          PayloadKg = None
          Priority = priority
          Status = Pending }

    let withPayload kg mission = { mission with PayloadKg = Some kg }

    let isActive mission =
        match mission.Status with
        | Assigned _
        | PickupInProgress _
        | DropoffInProgress _ -> true
        | _ -> false

    let isTerminal mission =
        match mission.Status with
        | Completed
        | Failed _ -> true
        | _ -> false

    // Приоритет для сортировки очереди.
    // Чем выше число — тем выше приоритет.
    // Critical=3, High=2, Normal=1, Low=0
    let priorityWeight mission =
        match mission.Priority with
        | Critical -> 3
        | High -> 2
        | Normal -> 1
        | Low -> 0
