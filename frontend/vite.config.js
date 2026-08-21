import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'
import { fileURLToPath, URL } from 'node:url'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const apiBaseUrl = env.VITE_API_BASE_URL || '/api'
  const appBase = env.VITE_APP_BASE || '/'
  const apiProxyTarget = env.API_PROXY_TARGET || 'http://localhost:5000'
  const devPort = Number(env.DEV_PORT) || 3000

  return {
    base: appBase,
    plugins: [vue()],

    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url))
      }
    },

    server: {
      port: devPort,
      proxy: apiBaseUrl.startsWith('/')
        ? {
            [apiBaseUrl]: {
              target: apiProxyTarget,
              changeOrigin: true
            }
          }
        : undefined
    },

    build: {
      outDir: 'dist',
      assetsDir: 'assets',
      emptyOutDir: true,
      sourcemap: false
    }
  }
})
