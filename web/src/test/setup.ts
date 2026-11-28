import { vi } from 'vitest'

vi.mock('@microsoft/signalr', async () => (await import('./liveServer')).fakeSignalR)
