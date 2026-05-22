module DroneOps.Actors.ActorNames

// Явные имена акторов — используются при создании через ActorOf.
// Литералы позволяют использовать их как константы без вычислений.
// Пути в runtime: /user/{name}
[<Literal>]
let CommandGateway = "command-gateway"

[<Literal>]
let SimulationClock = "simulation-clock"

[<Literal>]
let World = "world"

[<Literal>]
let Fleet = "fleet"

[<Literal>]
let Missions = "missions"

[<Literal>]
let Charging = "charging"

[<Literal>]
let Telemetry = "telemetry"

[<Literal>]
let SignalRBridge = "signalr-bridge"

[<Literal>]
let DeadLetterMonitor = "dead-letter-monitor"
