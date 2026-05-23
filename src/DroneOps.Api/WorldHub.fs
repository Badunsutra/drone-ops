module DroneOps.Api.WorldHub

open Microsoft.AspNetCore.SignalR

// Hub — точка подключения SignalR клиентов.
// В MVP клиенты только получают данные (server-push).
// Метод "worldSnapshot" будет вызываться со стороны сервера.
type WorldHub() =
    inherit Hub()
