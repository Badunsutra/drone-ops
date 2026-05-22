module DroneOps.Domain.Identifiers

open System.Text.RegularExpressions

// ─── Shared ───────────────────────────────────────────────────

let private makeId prefix (n: int) =
    if n > 0 then
        Ok $"{prefix}-{n:D3}"
    else
        Error $"ID number must be positive, got: {n}"

// lazy откладывает создание Regex до первого обращения через .Value
// и кэширует результат — создаётся ровно один раз на каждый модуль
let private makePattern prefix =
    lazy Regex($"^{Regex.Escape(prefix)}-\d+$", RegexOptions.Compiled)

let private validateId (pattern: Lazy<Regex>) prefix (s: string) =
    if pattern.Value.IsMatch(s) then
        Ok s
    else
        Error $"Invalid format: '{s}'. Expected '{prefix}-{{number}}'"

// ─── Types ────────────────────────────────────────────────────

type DroneId = private DroneId of string
type MissionId = private MissionId of string
type ChargingStationId = private ChargingStationId of string
type OperatorId = private OperatorId of string

// ─── Modules ──────────────────────────────────────────────────

module DroneId =
    [<Literal>]
    let Prefix = "drone"

    let private pattern = makePattern Prefix

    let fromNumber = makeId Prefix >> Result.map DroneId
    let fromString = validateId pattern Prefix >> Result.map DroneId
    let value (DroneId id) = id

module MissionId =
    [<Literal>]
    let Prefix = "mission"

    let private pattern = makePattern Prefix

    let fromNumber = makeId Prefix >> Result.map MissionId
    let fromString = validateId pattern Prefix >> Result.map MissionId

    // Генерация через Guid — для HTTP эндпоинтов
    let generate () =
        let id = System.Guid.NewGuid().ToString("N")[..7]
        MissionId $"{Prefix}-{id}"

    let value (MissionId id) = id

module ChargingStationId =
    [<Literal>]
    let Prefix = "station"

    let private pattern = makePattern Prefix

    let fromNumber = makeId Prefix >> Result.map ChargingStationId
    let fromString = validateId pattern Prefix >> Result.map ChargingStationId
    let value (ChargingStationId id) = id

module OperatorId =
    let fromString (s: string) =
        if System.String.IsNullOrWhiteSpace(s) then
            Error "OperatorId cannot be empty"
        else
            s.Trim() |> OperatorId |> Ok

    let value (OperatorId id) = id
