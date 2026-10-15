<script setup lang="ts">
import AppSidebar from '../components/AppSidebar.vue'
import GarageMap from '../components/GarageMap.vue'
import { useGarageList } from '../composables/useGarageList'
import { formatGarageOccupancy } from '../utils/occupancy'
const { garages, loading, initialized, error, fresh, connected, refresh } = useGarageList()
</script>
<template>
  <div class="app-shell">
    <AppSidebar :fresh="fresh" :garages="garages" />
    <main id="main-content" class="main-content"><header class="topbar"><h1>Hallit kartalla</h1><button class="refresh-button" :disabled="loading" @click="refresh">{{ loading ? 'Päivitetään…' : 'Päivitä tilanne' }}</button></header>
      <div class="page-content"><p role="status">{{ !connected ? 'Yhteys katkennut, yhdistetään…' : !fresh ? 'Tilanne voi olla vanhentunut' : 'Reaaliaikainen' }}</p><p v-if="error" role="alert">Tilanne voi olla vanhentunut. {{ error }}</p><p v-if="loading && !initialized" role="status">Haetaan halleja…</p>
        <GarageMap :garages="garages" />
        <section aria-labelledby="garages-heading"><h2 id="garages-heading">Kaikki hallit</h2><p v-if="initialized && !garages.length">Halleja ei löytynyt.</p><ul class="garage-list"><li v-for="garage in garages" :key="garage.id" class="panel"><h3><a :href="`/garages/${garage.id}`">{{ garage.name }}</a></h3><p>{{ garage.address || 'Osoite puuttuu' }}</p><p>{{ formatGarageOccupancy(garage) }}</p><p>{{ garage.freeLots }} vapaana</p><p v-if="garage.latitude === null || garage.longitude === null">Sijainti puuttuu</p></li></ul></section>
      </div>
    </main>
  </div>
</template>
<style scoped>.garage-list { list-style: none; padding: 0; display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 260px), 1fr)); gap: 16px; } a:focus-visible { outline: 3px solid #47718c; } </style>
