import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      // Intercept any request starting with /api and forward it to the backend API
      '/api': {
        target: 'https://localhost:7220', // Replace with your backend API URL
        changeOrigin: true,
        secure: false, // Set to true if your backend uses HTTPS
        rewrite: (path) => path.replace(/^\/api/, ''), // Remove the /api prefix when forwarding
      }
    }
  }
})
