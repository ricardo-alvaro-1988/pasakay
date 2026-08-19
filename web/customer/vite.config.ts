import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [
    react(),
    {
      name: 'ops-slash',
      configureServer(server) {
        server.middlewares.use((req, res, next) => {
          const path = req.url?.split('?')[0] ?? ''
          if (path === '/ops') {
            res.statusCode = 302
            res.setHeader('Location', '/ops/')
            res.end()
            return
          }
          next()
        })
      },
    },
  ],
  server: {
    host: true,
    port: 5174,
    proxy: {
      '/ops': {
        target: 'http://127.0.0.1:5173',
        changeOrigin: true,
        ws: true,
      },
      '/api': 'http://127.0.0.1:5088',
      '/uploads': {
        target: 'http://127.0.0.1:5088',
        changeOrigin: true,
      },
      '/health': 'http://127.0.0.1:5088',
      '/hubs': {
        target: 'http://127.0.0.1:5088',
        ws: true,
        changeOrigin: true,
      },
    },
  },
})
