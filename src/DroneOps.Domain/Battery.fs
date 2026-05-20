module DroneOps.Domain.Battery

// Инвариант: значение всегда в диапазоне 0..100.
// private на конструкторе — нельзя создать BatteryLevel напрямую.
type BatteryLevel = private BatteryLevel of int

module BatteryLevel =

    // ─── Создание ─────────────────────────────────────────────

    let create percent =
        if percent >= 0 && percent <= 100 then
            Ok(BatteryLevel percent)
        else
            Error $"Battery level must be 0..100, got: {percent}"

    // Для случаев когда значение гарантированно валидно —
    // например константы в тестах или начальные конфиги.
    // Бросает исключение если значение невалидно.
    let unsafeCreate percent =
        match create percent with
        | Ok value -> value
        | Error msg -> invalidArg (nameof percent) msg

    // ─── Константы ────────────────────────────────────────────

    let empty = BatteryLevel 0
    let full = BatteryLevel 100

    // ─── Чтение ───────────────────────────────────────────────

    let value (BatteryLevel percent) = percent

    let isLow threshold (BatteryLevel percent) = percent <= value threshold

    let isFull (BatteryLevel percent) = percent = 100

    // ─── Операции ─────────────────────────────────────────────

    // clamp гарантирует что результат остаётся в 0..100.
    // Дрон не может "перезарядиться" выше 100
    // или разрядиться ниже 0.
    let drain amount (BatteryLevel percent) =
        max 0 (percent - amount) |> BatteryLevel

    let charge amount (BatteryLevel percent) =
        min 100 (percent + amount) |> BatteryLevel
