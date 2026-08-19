import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  base: '/ops/',
  plugins: [
    react(),
    {
      name: 'ops-root-redirect',
      configureServer(server) {
        server.middlewares.use((req, res, next) => {
          const path = req.url?.split('?')[0] ?? ''
          if (path === '/' || path === '/index.html') {
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
    host: '127.0.0.1',
    port: 5173,
    strictPort: true,
    hmr: {
      clientPort: 5174,
    },
    proxy: {
      '/api': 'http://localhost:5088',
      '/uploads': 'http://localhost:5088',
      '/health': 'http://localhost:5088',
      '/hubs': {
        target: 'http://localhost:5088',
        ws: true,
      },
    },
  },
  preview: {
    port: 5173,
    strictPort: true,
  },
})
