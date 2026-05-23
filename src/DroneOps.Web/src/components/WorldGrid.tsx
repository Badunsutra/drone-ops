import { useMemo } from 'react'
import type { InitialWorldDto, WorldSnapshotDto, DroneViewDto } from '../types/world'

interface Props {
  world: InitialWorldDto
  snapshot: WorldSnapshotDto | null
}

type CellType =
  | { kind: 'empty' }
  | { kind: 'obstacle' }
  | { kind: 'station'; id: string }
  | { kind: 'drone'; drone: DroneViewDto }

const CELL_SIZE = 28

const droneColor: Record<string, string> = {
  idle: '#22c55e',
  'moving-to-pickup': '#3b82f6',
  'moving-to-dropoff': '#8b5cf6',
  'returning-to-charge': '#f59e0b',
  charging: '#facc15',
  failed: '#ef4444',
}

function Cell({ cell }: { cell: CellType }) {
  const base: React.CSSProperties = {
    width: CELL_SIZE,
    height: CELL_SIZE,
    border: '1px solid #1f2937',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    fontSize: 10,
    fontWeight: 'bold',
    borderRadius: 2,
    transition: 'background-color 0.2s',
  }

  switch (cell.kind) {
    case 'obstacle':
      return <div style={{ ...base, backgroundColor: '#374151' }} title="Obstacle" />

    case 'station':
      return (
        <div style={{ ...base, backgroundColor: '#1e3a5f' }} title={`Station ${cell.id}`}>
          ⚡
        </div>
      )

    case 'drone': {
      const d = cell.drone
      const color = droneColor[d.status] ?? '#6b7280'
      return (
        <div
          style={{
            ...base,
            backgroundColor: color,
            color: '#fff',
            cursor: 'default',
          }}
          title={`${d.id} | ${d.status} | 🔋${d.batteryPercent}%`}
        >
          {d.id.replace('drone-', '')}
        </div>
      )
    }

    default:
      return <div style={{ ...base, backgroundColor: '#111827' }} />
  }
}

export function WorldGrid({ world, snapshot }: Props) {
  const grid = useMemo<CellType[][]>(() => {
    const g: CellType[][] = Array.from({ length: world.height }, () =>
      Array.from({ length: world.width }, () => ({ kind: 'empty' })),
    )

    world.obstacles.forEach((o) => {
      if (g[o.y]?.[o.x]) g[o.y][o.x] = { kind: 'obstacle' }
    })

    world.stations.forEach((s) => {
      if (g[s.y]?.[s.x]) g[s.y][s.x] = { kind: 'station', id: s.id }
    })

    snapshot?.drones.forEach((d) => {
      if (g[d.y]?.[d.x]) g[d.y][d.x] = { kind: 'drone', drone: d }
    })

    return g
  }, [world, snapshot])

  return (
    <div
      style={{
        display: 'inline-block',
        border: '2px solid #374151',
        borderRadius: 4,
      }}
    >
      {grid.map((row, y) => (
        <div key={y} style={{ display: 'flex' }}>
          {row.map((cell, x) => (
            <Cell key={`${x}-${y}`} cell={cell} />
          ))}
        </div>
      ))}
    </div>
  )
}
