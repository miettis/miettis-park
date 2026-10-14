<script setup lang="ts">
import { inject, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import type { Map as MapLibreMap, Marker, MapMouseEvent } from 'maplibre-gl'
import { loadMapLibre as defaultLoader, mapLibreLoaderKey, type MapLibreLoader } from '../services/mapLibre'

const props = defineProps<{ latitude: number | null, longitude: number | null, label: string, selectable?: boolean }>()
const emit = defineEmits<{ select: [point: { latitude: number, longitude: number }] }>()
const loadMapLibre = inject<MapLibreLoader>(mapLibreLoaderKey, defaultLoader)
const element = ref<HTMLDivElement | null>(null)
const loading = ref(false)
const error = ref('')
const ready = ref(false)
let map: MapLibreMap | null = null
let marker: Marker | null = null
let library: Awaited<ReturnType<MapLibreLoader>> | null = null
let generation = 0
let timeout: ReturnType<typeof setTimeout> | undefined
let disposed = false

function clearMap(): void {
  clearTimeout(timeout)
  ready.value = false
  marker?.remove()
  map?.remove()
  marker = null
  map = null
}
function updateMarker(): void {
  if (!map || !library) return
  if (props.latitude === null || props.longitude === null) {
    marker?.remove()
    marker = null
    return
  }
  marker ??= new library.Marker().setLngLat([props.longitude, props.latitude]).addTo(map)
  marker.setLngLat([props.longitude, props.latitude])
}
function selectPoint(event: MapMouseEvent): void {
  if (props.selectable) emit('select', { latitude: event.lngLat.lat, longitude: event.lngLat.lng })
}
function selectCenter(): void {
  if (!map || !props.selectable) return
  const center = map.getCenter()
  emit('select', { latitude: center.lat, longitude: center.lng })
}
async function mountMap(): Promise<void> {
  const current = ++generation
  clearMap()
  loading.value = false
  error.value = ''
  if (!element.value || !props.selectable && (props.latitude === null || props.longitude === null)) return
  loading.value = true
  try {
    const maplibre = await loadMapLibre()
    if (disposed || current !== generation || !element.value) return
    library = maplibre
    map = new maplibre.Map({
      container: element.value,
      style: 'https://tiles.openfreemap.org/styles/liberty',
      // A viewport is not a selected point. Missing locations never create a marker.
      center: props.latitude !== null && props.longitude !== null ? [props.longitude, props.latitude] : [24.94, 60.17],
      zoom: props.latitude === null ? 5 : 14,
      attributionControl: {}
    })
    updateMarker()
    map.on('error', () => {
      clearTimeout(timeout)
      error.value = 'Karttapalveluun ei saatu yhteyttä. Lomake ja hallilista ovat edelleen käytettävissä.'
      loading.value = false
    })
    map.on('click', selectPoint)
    map.once('load', () => {
      clearTimeout(timeout)
      loading.value = false
      ready.value = true
    })
    timeout = setTimeout(() => {
      error.value = 'Kartan lataus aikakatkaistiin. Yritä uudelleen tai jatka hallilistan käyttöä.'
      loading.value = false
    }, 15000)
  }
  catch {
    if (current !== generation || disposed) return
    clearMap()
    loading.value = false
    error.value = 'Karttaa ei voitu alustaa (WebGL tai verkkoyhteys). Voit yrittää uudelleen.'
  }
}
watch(() => [props.latitude, props.longitude], () => {
  if (map) updateMarker()
  else void mountMap()
})
onMounted(() => void mountMap())
onBeforeUnmount(() => {
  disposed = true
  generation++
  clearMap()
})
</script>

<template>
  <section class="map-panel" aria-label="Hallin sijaintikartta">
    <div v-show="selectable || latitude !== null && longitude !== null" ref="element" class="map-canvas" :aria-label="`${label} kartalla`" />
    <p v-if="latitude === null || longitude === null" class="map-fallback" role="status">Hallille ei ole määritetty karttakoordinaatteja.</p>
    <p v-if="selectable" class="map-help">Valitse piste klikkaamalla karttaa. Näppäimistöllä: kohdista karttaan, siirrä nuolinäppäimillä ja valitse keskipiste alla olevalla painikkeella.</p>
    <button v-if="selectable" type="button" :disabled="!ready" @click="selectCenter">Valitse kartan keskipiste</button>
    <p v-if="loading" class="map-status" role="status">Ladataan karttaa…</p>
    <div v-if="error" class="map-error" role="alert"><span>{{ error }}</span><button type="button" @click="mountMap">Yritä karttaa uudelleen</button></div>
    <p class="map-attribution">© <a href="https://openfreemap.org" target="_blank" rel="noreferrer">OpenFreeMap</a> · © <a href="https://www.openmaptiles.org/" target="_blank" rel="noreferrer">OpenMapTiles</a> · Data © <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noreferrer">OpenStreetMap</a></p>
  </section>
</template>

<style scoped>
.map-panel { position: relative; overflow: hidden; border: 1px solid #dfe5dc; border-radius: 12px; background: white; }
.map-canvas { width: 100%; height: 310px; }
.map-fallback, .map-help { padding: 12px; color: #526259; }
.map-status, .map-error { position: absolute; z-index: 1; top: 8px; left: 8px; margin: 0; padding: 8px 10px; border-radius: 7px; background: white; box-shadow: 0 1px 5px #0002; font-size: 12px; }
.map-error { display: flex; right: 8px; align-items: center; justify-content: space-between; gap: 10px; color: #ad4540; }
button { min-height: 36px; margin: 6px; padding: 6px 9px; border: 1px solid #dfe5dc; border-radius: 6px; background: white; cursor: pointer; }
.map-attribution { margin: 0; padding: 4px; background: #ffffffd9; color: #33443a; font: 11px/1.5 Arial, sans-serif; }
.map-attribution a { color: inherit; }
</style>
