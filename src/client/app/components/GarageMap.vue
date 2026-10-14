<script setup lang="ts">
import { computed, inject, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import type { Map as MapLibreMap, Marker, MapMouseEvent } from 'maplibre-gl'
import { loadMapLibre as defaultLoader, mapLibreLoaderKey, type MapLibreLoader } from '../services/mapLibre'
import type { GarageDto } from '../types/garage'
import { formatGarageOccupancy } from '../utils/occupancy'

const props = withDefaults(defineProps<{
  latitude?: number | null
  longitude?: number | null
  label?: string
  selectable?: boolean
  garages?: GarageDto[]
}>(), {
  latitude: null, longitude: null, label: 'Hallit kartalla', garages: () => []
})
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
let lastFittedPointSet: string | null = null
const garageMarkers = new Map<number, { marker: Marker, element: HTMLAnchorElement, point: string }>()

function hasLegacyPoint(): boolean {
  return props.latitude !== null && props.longitude !== null
}

function isMappableGarage(garage: GarageDto): boolean {
  return garage.latitude !== null && garage.longitude !== null &&
    Number.isFinite(garage.latitude) && Number.isFinite(garage.longitude) &&
    garage.latitude >= -90 && garage.latitude <= 90 && garage.longitude >= -180 && garage.longitude <= 180
}

const locatedGarages = computed(() => props.garages.filter(isMappableGarage))
const hasMapPoints = computed(() => locatedGarages.value.length > 0)
const showMap = computed(() => Boolean(props.selectable || hasLegacyPoint() || hasMapPoints.value))

function clearMap(): void {
  clearTimeout(timeout)
  ready.value = false
  marker?.remove()
  for (const entry of garageMarkers.values()) {
    entry.marker.remove()
  }
  garageMarkers.clear()
  map?.remove()
  marker = null
  map = null
  lastFittedPointSet = null
}

function updateLegacyMarker(): void {
  if (!map || !library) {
    return
  }

  if (!hasLegacyPoint()) {
    marker?.remove()
    marker = null
    return
  }

  const coordinates: [number, number] = [props.longitude!, props.latitude!]
  if (!marker) {
    marker = new library.Marker().setLngLat(coordinates).addTo(map)
    return
  }

  marker.setLngLat(coordinates)
}

function updateGarageMarkerContent(element: HTMLAnchorElement, garage: GarageDto): void {
  const name = document.createElement('strong')
  name.textContent = garage.name
  const address = document.createElement('span')
  address.textContent = garage.address?.trim() || 'Osoite ei tiedossa'
  const occupancy = document.createElement('span')
  occupancy.textContent = formatGarageOccupancy(garage)
  element.replaceChildren(name, address, occupancy)
  element.href = `/garages/${garage.id}`
  element.setAttribute('aria-label', `${garage.name}, ${address.textContent}, ${formatGarageOccupancy(garage)}`)
  element.title = `${garage.name} - ${address.textContent} - ${formatGarageOccupancy(garage)}`
}

function garagePoint(garage: GarageDto): string {
  return `${garage.longitude},${garage.latitude}`
}

function fitGaragePoints(points: GarageDto[]): void {
  if (!map) {
    return
  }

  const currentPointSet = points
    .map(garage => `${garage.id}:${garagePoint(garage)}`)
    .sort()
    .join('|')
  if (currentPointSet === lastFittedPointSet) {
    return
  }

  lastFittedPointSet = currentPointSet
  if (points.length === 0) {
    return
  }

  const longitudes = points.map(garage => garage.longitude!)
  const latitudes = points.map(garage => garage.latitude!)
  const bounds: [[number, number], [number, number]] = [
    [Math.min(...longitudes), Math.min(...latitudes)],
    [Math.max(...longitudes), Math.max(...latitudes)]
  ]
  map.fitBounds(bounds, { padding: 40, maxZoom: 14 })
}

function updateGarageMarkers(): void {
  if (!map || !library) {
    return
  }

  const currentIds = new Set(locatedGarages.value.map(garage => garage.id))
  for (const [id, entry] of garageMarkers) {
    if (!currentIds.has(id)) {
      entry.marker.remove()
      garageMarkers.delete(id)
    }
  }

  for (const garage of locatedGarages.value) {
    const point = garagePoint(garage)
    let entry = garageMarkers.get(garage.id)
    if (!entry) {
      const anchor = document.createElement('a')
      anchor.className = 'garage-map-marker'
      updateGarageMarkerContent(anchor, garage)
      const garageMarker = new library.Marker({ element: anchor, anchor: 'bottom' })
        .setLngLat([garage.longitude!, garage.latitude!])
        .addTo(map)
      entry = { marker: garageMarker, element: anchor, point }
      garageMarkers.set(garage.id, entry)
    }
    else {
      if (entry.point !== point) {
        entry.marker.setLngLat([garage.longitude!, garage.latitude!])
        entry.point = point
      }
      updateGarageMarkerContent(entry.element, garage)
    }
  }

  fitGaragePoints(locatedGarages.value)
}

function updateMarkers(): void {
  updateLegacyMarker()
  updateGarageMarkers()
}

function selectPoint(event: MapMouseEvent): void {
  if (props.selectable) {
    emit('select', { latitude: event.lngLat.lat, longitude: event.lngLat.lng })
  }
}

function selectCenter(): void {
  if (!map || !props.selectable) {
    return
  }

  const center = map.getCenter()
  emit('select', { latitude: center.lat, longitude: center.lng })
}

async function mountMap(): Promise<void> {
  const current = ++generation
  clearMap()
  loading.value = false
  error.value = ''
  if (!element.value || !showMap.value) {
    return
  }

  loading.value = true
  try {
    const maplibre = await loadMapLibre()
    if (disposed || current !== generation || !element.value) {
      return
    }

    library = maplibre
    const initialGarage = locatedGarages.value[0]
    const center: [number, number] = hasLegacyPoint()
      ? [props.longitude!, props.latitude!]
      : initialGarage
        ? [initialGarage.longitude!, initialGarage.latitude!]
        : [24.94, 60.17]
    map = new maplibre.Map({
      container: element.value,
      style: 'https://tiles.openfreemap.org/styles/liberty',
      // A viewport is not a selected point. Missing locations never create a marker.
      center,
      zoom: hasLegacyPoint() || initialGarage ? 14 : 5,
      attributionControl: {}
    })
    updateMarkers()
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
    if (current !== generation || disposed) {
      return
    }

    clearMap()
    loading.value = false
    error.value = 'Karttaa ei voitu alustaa (WebGL tai verkkoyhteys). Voit yrittää uudelleen.'
  }
}

watch(() => [props.latitude, props.longitude], () => {
  if (map) {
    updateLegacyMarker()
  }
  else if (!loading.value) {
    void mountMap()
  }
})
watch(() => props.garages, () => {
  if (map) {
    updateGarageMarkers()
  }
  else if (!loading.value) {
    void mountMap()
  }
}, { deep: true })
onMounted(() => void mountMap())
onBeforeUnmount(() => {
  disposed = true
  generation++
  clearMap()
})
</script>

<template>
  <section class="map-panel" aria-label="Hallin sijaintikartta">
    <div v-show="showMap" ref="element" class="map-canvas" :aria-label="`${label} kartalla`" />
    <p v-if="!hasLegacyPoint() && !hasMapPoints" class="map-fallback" role="status">Hallille ei ole määritetty karttakoordinaatteja.</p>
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
:global(.garage-map-marker) { display: grid; gap: 2px; min-width: 140px; max-width: 210px; padding: 7px 9px; border: 1px solid #176d50; border-radius: 8px; background: white; color: #24372d; box-shadow: 0 2px 8px #0003; font: 12px/1.35 Arial, sans-serif; text-decoration: none; }
:global(.garage-map-marker strong) { color: #176d50; }
:global(.garage-map-marker:focus-visible) { outline: 3px solid #47718c; outline-offset: 2px; }
</style>
