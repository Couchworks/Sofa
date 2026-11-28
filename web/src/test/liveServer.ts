import { vi } from 'vitest'

type Handler = (...said: unknown[]) => void

export const liveServer = {
  handlers: new Map<string, Handler>(),
  started: 0,
  say(message: string, ...said: unknown[]) {
    liveServer.handlers.get(message)?.(...said)
  },
}

const connection = {
  state: 'Disconnected',
  on: (message: string, handler: Handler) => liveServer.handlers.set(message, handler),
  onreconnecting: vi.fn(),
  onreconnected: vi.fn(),
  onclose: vi.fn(),
  start: async () => {
    liveServer.started++
    connection.state = 'Connected'
  },
  stop: async () => {
    connection.state = 'Disconnected'
  },
}

export const fakeSignalR = {
  HubConnectionState: { Connected: 'Connected', Disconnected: 'Disconnected' },
  HubConnectionBuilder: class {
    withUrl() {
      return this
    }
    withAutomaticReconnect() {
      return this
    }
    build() {
      return connection
    }
  },
}
