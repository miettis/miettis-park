<script setup lang="ts">
import { computed } from 'vue'
import type { GarageDto } from '../types/garage'
import { useRoute } from 'vue-router'
import { garageEvents } from '../services/garageEvents'
import { useGarageList } from '../composables/useGarageList'
const route = useRoute()
const props = defineProps<{ garages?: GarageDto[], fresh?: boolean }>()
const list = props.garages === undefined ? useGarageList() : null
const garages = computed(() => props.garages ?? list?.garages.value ?? [])
const error = computed(() => list?.error.value ?? '')
const connected = computed(() => garageEvents.connected.value)
const fresh = computed(() => props.fresh ?? list?.fresh.value ?? true)
const refresh = () => list?.refresh()
</script>
<template>
  <aside class="sidebar">
    <a class="brand" href="/"><span class="logo" aria-hidden="true">P</span><span>Miettis Park</span></a>
    <p class="sidebar-caption">Tilaa saapua.</p>
    <nav aria-label="Päänavigaatio">
      <a href="/" :aria-current="route?.path === '/' ? 'page' : undefined">Kartta</a>
      <a href="/admin/garages" :aria-current="route?.path === '/admin/garages' ? 'page' : undefined">Hallit</a>
      <ul class="garage-navigation">
        <li v-for="garage in garages" :key="garage.id"><a :href="`/garages/${garage.id}`" :aria-current="route?.path === `/garages/${garage.id}` ? 'page' : undefined">{{ garage.name }}</a></li>
      </ul>
      <p v-if="error" role="alert">Hallilistaa ei voitu hakea. <button type="button" @click="refresh">Yritä uudelleen</button></p>
    </nav>
    <div class="sidebar-bottom">{{ !connected ? 'Yhteys katkennut, yhdistetään…' : !fresh ? 'Tilanne voi olla vanhentunut' : 'Reaaliaikainen' }}</div>
  </aside>
</template>
<style scoped>
.garage-navigation { list-style: none; padding: 0 0 0 18px; margin: 0; display: grid; gap: 4px; }
.garage-navigation a { overflow-wrap: anywhere; }
.sidebar { overflow-y: auto; }
@media (max-width: 620px) { .sidebar nav { display: flex; flex-wrap: wrap; } .garage-navigation { display: flex; flex-wrap: wrap; flex-basis: 100%; } }
</style>
