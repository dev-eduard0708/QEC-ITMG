import path from 'node:path'
import { fileURLToPath } from 'node:url'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

const rootDir = path.dirname(fileURLToPath(import.meta.url))

/** Proxy API/auth/OIDC to the ASP.NET host while keeping the browser Host as localhost:5173. */
function backendProxy(preserveBrowserHost: boolean) {
  return {
    target: 'http://localhost:5080',
    // changeOrigin:false keeps Host: localhost:5173 so OIDC redirect_uri is http://localhost:5173/signin-oidc
    changeOrigin: !preserveBrowserHost,
  }
}

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(rootDir, './src'),
    },
  },
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': backendProxy(false),
      '/health': backendProxy(false),
      // SignalR hubs need websocket upgrade forwarding.
      '/hubs': { ...backendProxy(false), ws: true },
      // Auth challenge + OIDC callbacks must preserve the Vite origin for redirect_uri / cookies.
      '/auth': backendProxy(true),
      '/signin-oidc': backendProxy(true),
      '/signout-callback-oidc': backendProxy(true),
    },
  },
})
