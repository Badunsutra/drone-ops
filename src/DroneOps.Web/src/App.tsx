import { WorldGrid } from "./components/WorldGrid";
import { useWorldHub } from "./hooks/useWorldHub";

function StatusBadge({ connected }: { connected: boolean }) {
  return (
    <span
      style={{
        display: "inline-block",
        padding: "2px 8px",
        borderRadius: 12,
        fontSize: 12,
        backgroundColor: connected ? "#15803d" : "#b91c1c",
        color: "#fff",
      }}
    >
      {connected ? "● Connected" : "○ Disconnected"}
    </span>
  );
}

export default function App() {
  const { snapshot, initialWorld, connected } = useWorldHub();

  return (
    <div
      style={{
        minHeight: "100vh",
        backgroundColor: "#030712",
        color: "#f9fafb",
        fontFamily: "monospace",
        padding: 24,
      }}
    >
      <div
        style={{
          display: "flex",
          alignItems: "center",
          gap: 16,
          marginBottom: 24,
        }}
      >
        <h1 style={{ margin: 0, fontSize: 20 }}>DroneOps Playground</h1>
        <StatusBadge connected={connected} />
        {snapshot && (
          <span style={{ fontSize: 12, color: "#6b7280" }}>
            tick #{snapshot.tick}
          </span>
        )}
      </div>

      <div style={{ display: "flex", gap: 24, alignItems: "flex-start" }}>
        {/* Grid */}
        {initialWorld ? (
          <WorldGrid world={initialWorld} snapshot={snapshot} />
        ) : (
          <div style={{ color: "#6b7280" }}>Loading map...</div>
        )}

        {/* Side panels */}
        <div
          style={{
            display: "flex",
            flexDirection: "column",
            gap: 16,
            minWidth: 280,
          }}
        >
          {/* Drones */}
          <div
            style={{ backgroundColor: "#111827", borderRadius: 8, padding: 12 }}
          >
            <div style={{ fontSize: 12, color: "#6b7280", marginBottom: 8 }}>
              DRONES
            </div>
            {snapshot?.drones.length === 0 && (
              <div style={{ fontSize: 12, color: "#374151" }}>No drones</div>
            )}
            {snapshot?.drones.map((d) => (
              <div
                key={d.id}
                style={{
                  fontSize: 12,
                  marginBottom: 6,
                  display: "flex",
                  justifyContent: "space-between",
                }}
              >
                <span style={{ color: "#e5e7eb" }}>{d.id}</span>
                <span style={{ color: "#6b7280" }}>🔋{d.batteryPercent}%</span>
                <span style={{ color: "#9ca3af" }}>{d.status}</span>
              </div>
            ))}
          </div>

          {/* Missions */}
          <div
            style={{ backgroundColor: "#111827", borderRadius: 8, padding: 12 }}
          >
            <div style={{ fontSize: 12, color: "#6b7280", marginBottom: 8 }}>
              MISSIONS
            </div>
            {snapshot?.missions.length === 0 && (
              <div style={{ fontSize: 12, color: "#374151" }}>No missions</div>
            )}
            {snapshot?.missions.map((m) => (
              <div key={m.id} style={{ fontSize: 11, marginBottom: 6 }}>
                <div style={{ color: "#e5e7eb" }}>{m.id.slice(0, 16)}…</div>
                <div style={{ color: "#6b7280" }}>
                  [{m.pickupX},{m.pickupY}] → [{m.dropoffX},{m.dropoffY}] ·{" "}
                  {m.status}
                </div>
              </div>
            ))}
          </div>

          {/* Event log */}
          <div
            style={{ backgroundColor: "#111827", borderRadius: 8, padding: 12 }}
          >
            <div style={{ fontSize: 12, color: "#6b7280", marginBottom: 8 }}>
              EVENTS
            </div>
            {snapshot?.recentEvents.map((e, i) => (
              <div
                key={i}
                style={{ fontSize: 11, marginBottom: 4, color: "#9ca3af" }}
              >
                <span style={{ color: "#4b5563" }}>[{e.tick ?? "?"}]</span>{" "}
                {e.message}
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
