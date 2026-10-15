<script setup lang="ts">
import AppSidebar from '../components/AppSidebar.vue'
import GarageMap from '../components/GarageMap.vue'
import { useGarageList } from '../composables/useGarageList'
const { garages, loading, initialized, error, fresh, connected, refresh } = useGarageList()
</script>
<template>
  <div class="app-shell">
    <AppSidebar :fresh="fresh" :garages="garages" />
    <main id="main-content" class="main-content"><header class="topbar"><h1>Hallit kartalla</h1><button class="refresh-button" :disabled="loading" @click="refresh">{{ loading ? 'Päivitetään…' : 'Päivitä tilanne' }}</button></header>
      <div class="page-content"><p v-if="!connected || !fresh" role="status">{{ !connected ? 'Yhteys katkennut, yhdistetään…' : 'Tilanne voi olla vanhentunut' }}</p><p v-if="error" role="alert">Tilanne voi olla vanhentunut. {{ error }}</p><p v-if="loading && !initialized" role="status">Haetaan halleja…</p>
        <GarageMap class="overview-map" :garages="garages" />
        <p v-if="initialized && !garages.length">Halleja ei löytynyt.</p>
      </div>
    </main>
  </div>
</template>
<style scoped>
.overview-map :deep(.map-canvas) { height: auto; aspect-ratio: 1; min-height: 360px; }
</style>
