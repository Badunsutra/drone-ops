import { useEffect, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import type { InitialWorldDto, WorldSnapshotDto } from '../types/world'

export function useWorldHub() {
  const [snapshot, setSnapshot] = useState<WorldSnapshotDto | null>(null)
  const [initialWorld, setInitialWorld] = useState<InitialWorldDto | null>(null)
  const [connected, setConnected] = useState(false)

  useEffect(() => {
    // Загружаем статичную карту один раз
    fetch('/api/world')
      .then((r) => r.json())
      .then(setInitialWorld)
      .catch(console.error)

    // Подключаемся к SignalR хабу
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/world')
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    connection.on('worldSnapshot', (dto: WorldSnapshotDto) => {
      setSnapshot(dto)
    })

    connection.onclose(() => setConnected(false))
    connection.onreconnecting(() => setConnected(false))
    connection.onreconnected(() => setConnected(true))

    connection
      .start()
      .then(() => setConnected(true))
      .catch(console.error)

    return () => {
      connection.stop()
    }
  }, [])

  return { snapshot, initialWorld, connected }
}
