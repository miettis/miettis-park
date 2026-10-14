import process from 'node:process'

export default defineNuxtConfig({
  compatibilityDate: '2026-10-14',
  modules: ['@nuxt/eslint'],
  css: ['maplibre-gl/dist/maplibre-gl.css'],
  ssr: false,
  devtools: { enabled: true },
  app: {
    head: {
      title: 'Miettis Park',
      htmlAttrs: { lang: 'fi' },
      meta: [{ name: 'description', content: 'Pysäköintihallisimulaatio' }]
    }
  },
  nitro: {
    devProxy: {
      '/api': {
        target: `${process.env.API_PROXY_TARGET || 'http://localhost:5080'}/api`,
        changeOrigin: true
      }
    }
  }
})
