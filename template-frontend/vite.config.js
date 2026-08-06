<<<<<<< HEAD
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { fileURLToPath, URL } from 'node:url'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue()],

  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url))
    }
  },

  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true
      }
=======
import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'
import { fileURLToPath, URL } from 'node:url'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const apiBaseUrl = env.VITE_API_BASE_URL || '/api'
  const apiProxyTarget = env.API_PROXY_TARGET || 'http://localhost:5000'
  const devPort = Number(env.DEV_PORT) || 3000

  return {
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
>>>>>>> b275658530f4ec665d7608e04a9c6a80140ce075
    }
  }
})
