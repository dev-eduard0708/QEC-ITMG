import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { spawn } from 'node:child_process'
import type { IncomingMessage, ServerResponse } from 'node:http'
import type { Plugin } from 'vite'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

const rootDir = path.dirname(fileURLToPath(import.meta.url))
const repoRoot = path.resolve(rootDir, '../..')

/** Proxy API/auth/OIDC to the ASP.NET host while keeping the browser Host as localhost:5173. */
function backendProxy(preserveBrowserHost: boolean) {
  return {
    target: 'http://localhost:5080',
    // changeOrigin:false keeps Host: localhost:5173 so OIDC redirect_uri is http://localhost:5173/signin-oidc
    changeOrigin: !preserveBrowserHost,
  }
}

function sendJson(res: ServerResponse, status: number, body: unknown) {
  res.statusCode = status
  res.setHeader('Content-Type', 'application/json; charset=utf-8')
  res.end(JSON.stringify(body))
}

/**
 * Development-only helpers used by the login page:
 * - GET  /__dev/api-health   → { status: 'up' | 'down' }
 * - POST /__dev/restart-api  → launches scripts/restart-local-api.ps1
 * - POST /__dev/rebuild-api  → launches scripts/rebuild-local-api.ps1
 */
function localApiControlPlugin(): Plugin {
  return {
    name: 'qec-local-api-control',
    configureServer(server) {
      server.middlewares.use(async (req: IncomingMessage, res: ServerResponse, next) => {
        const url = req.url?.split('?')[0] ?? ''
        if (url !== '/__dev/api-health' && url !== '/__dev/restart-api' && url !== '/__dev/rebuild-api') {
          next()
          return
        }

        if (url === '/__dev/api-health' && (req.method === 'GET' || req.method === 'HEAD')) {
          try {
            const response = await fetch('http://127.0.0.1:5080/health/live', {
              signal: AbortSignal.timeout(2500),
            })
            sendJson(res, 200, {
              status: response.ok ? 'up' : 'down',
              httpStatus: response.status,
            })
          } catch {
            sendJson(res, 200, { status: 'down', httpStatus: null })
          }
          return
        }

        if (
          (url === '/__dev/restart-api' || url === '/__dev/rebuild-api') &&
          req.method === 'POST'
        ) {
          const isRebuild = url === '/__dev/rebuild-api'
          const script = path.join(
            repoRoot,
            'scripts',
            isRebuild ? 'rebuild-local-api.ps1' : 'restart-local-api.ps1',
          )
          try {
            const child = spawn(
              'powershell.exe',
              ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', script],
              {
                cwd: repoRoot,
                detached: true,
                stdio: 'ignore',
                windowsHide: true,
              },
            )
            child.unref()
            sendJson(res, 202, {
              ok: true,
              message: isRebuild
                ? 'API rebuild launched. Waiting for health…'
                : 'API restart launched. Waiting for health…',
            })
          } catch (error) {
            sendJson(res, 500, {
              ok: false,
              message:
                error instanceof Error
                  ? error.message
                  : isRebuild
                    ? 'Failed to launch API rebuild.'
                    : 'Failed to launch API restart.',
            })
          }
          return
        }

        sendJson(res, 405, { ok: false, message: 'Method not allowed.' })
      })
    },
  }
}

export default defineConfig({
  plugins: [react(), tailwindcss(), localApiControlPlugin()],
  resolve: {
    alias: {
      '@': path.resolve(rootDir, './src'),
    },
  },
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      // Large Support Helper EXE (~70MB+); raise timeout so download is not cut off.
      '/api/v1/me/remote-support/helper': {
        ...backendProxy(false),
        timeout: 600_000,
        proxyTimeout: 600_000,
      },
      '/api': {
        ...backendProxy(false),
        timeout: 120_000,
        proxyTimeout: 120_000,
      },
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
