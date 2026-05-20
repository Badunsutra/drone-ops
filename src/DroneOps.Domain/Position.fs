module DroneOps.Domain.Position

// Record type в F# — иммутабельный по умолчанию.
// Структурное равенство встроено автоматически:
// { X=1; Y=2 } = { X=1; Y=2 } // true, без override Equals
type Position = { X: int; Y: int }

module Position =
    let create x y = { X = x; Y = y }

    // Манхэттенское расстояние — стандартная метрика для grid-map.
    // Евклидово не нужно: дроны двигаются по клеткам, не по прямой.
    let manhattanDistance a b = abs (a.X - b.X) + abs (a.Y - b.Y)

    // Соседние клетки по четырём сторонам.
    // RouteEngine будет использовать это при поиске пути.
    let neighbors pos =
        [ { pos with X = pos.X - 1 } // left
          { pos with X = pos.X + 1 } // right
          { pos with Y = pos.Y - 1 } // down
          { pos with Y = pos.Y + 1 } ] // up

    // Проверка попадания в границы карты.
    // width/height — параметры, а не зависимость от WorldMap,
    // потому что Position не должен знать о WorldMap.
    let isWithinBounds width height pos =
        pos.X >= 0 && pos.X < width && pos.Y >= 0 && pos.Y < height

    // Смежность — дрон за один тик двигается ровно на одну клетку.
    let isAdjacent a b = manhattanDistance a b = 1
