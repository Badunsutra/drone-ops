module DroneOps.Domain.Route

open Position

// private конструктор — нельзя создать Route с пустым списком.
// Инвариант: маршрут содержит хотя бы одну позицию.
//
// Конвенция MVP (из документации):
// - route НЕ содержит текущую позицию дрона
// - первый элемент — следующая клетка куда двигаться
// - последний элемент — целевая позиция
type Route = private Route of Position list

module Route =

    // ─── Создание ─────────────────────────────────────────────

    let create steps =
        match steps with
        | [] -> Error "Route must contain at least one position"
        | _ -> Ok(Route steps)

    let singleton pos = Route [ pos ]

    // ─── Чтение ───────────────────────────────────────────────

    let steps (Route steps) = steps

    let length (Route steps) = steps.Length

    let destination (Route steps) = List.last steps

    // ─── Навигация ────────────────────────────────────────────

    // Ключевая операция для Movement.fs:
    // возвращает следующий шаг и оставшийся маршрут.
    // Option — маршрут может быть исчерпан.
    let tryNext (Route steps) =
        match steps with
        | [] -> Option.None
        | next :: rest -> Some(next, Route rest)

    // Удобная альтернатива когда важен только факт завершения.
    let isCompleted (Route steps) = steps.IsEmpty
