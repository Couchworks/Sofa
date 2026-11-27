/// <reference types="vitest/config" />
import path from 'node:path'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// In development the App Gateway proxies every page request here, so the web client
// and the API it calls share one origin and /api needs no proxy of its own.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  // The integration tests start their own Vite beside a running app host; a cache of its own
  // keeps Vite's one-server-per-cache lock from refusing it.
  cacheDir: process.env.SOFA_VITE_CACHE_DIR || 'node_modules/.vite',
  resolve: {
    alias: {
      '@': path.resolve(import.meta.dirname, './src'),
    },
  },
  test: {
    environment: 'jsdom',
  },
})
