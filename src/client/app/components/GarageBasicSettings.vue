<script setup lang="ts">
import AppSidebar from './AppSidebar.vue'
import { computed, inject, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import GarageMap from './GarageMap.vue'
import { useGarageAdmin } from '../composables/useGarageAdmin'
import { garageApiKey, HttpGarageApi } from '../services/garageApi'
import type { GarageInputDto } from '../types/garage'

const props = defineProps<{ garageId?: number, embedded?: boolean }>()
const route = useRoute()
const router = useRouter()
const isNew = computed(() => props.garageId === undefined && route.params.id === 'new')
const id = computed(() => isNew.value ? null : props.garageId ?? Number(route.params.id))
const api = inject(garageApiKey, new HttpGarageApi())
const { garages, loading, initialized, error, submitting, savedGarage, refresh, save } = useGarageAdmin(api)
const name = ref('')
const address = ref('')
const latitude = ref<string | number>('')
const longitude = ref<string | number>('')
const fieldError = ref('')
const currentGarage = computed(() => id.value === null ? null : garages.value.find(garage => garage.id === id.value) ?? null)

const formGarageId = ref<number | null>(null)
const externalChange = ref(false)
watch([initialized, garages], () => {
  if (currentGarage.value && formGarageId.value === currentGarage.value.id) {
    externalChange.value = name.value !== currentGarage.value.name || address.value !== (currentGarage.value.address ?? '') || nullableNumber(latitude.value) !== currentGarage.value.latitude || nullableNumber(longitude.value) !== currentGarage.value.longitude
    return
  }
  if (currentGarage.value) {
    formGarageId.value = currentGarage.value.id; externalChange.value = false; name.value = currentGarage.value.name
    address.value = currentGarage.value.address ?? ''
    latitude.value = currentGarage.value.latitude === null ? '' : String(currentGarage.value.latitude)
    longitude.value = currentGarage.value.longitude === null ? '' : String(currentGarage.value.longitude)
  }
}, { immediate: true })

function nullableNumber(value: string | number): number | null {
  if (value === '') return null
  return typeof value === 'number' ? value : Number(value)
}

async function submit(): Promise<void> {
  fieldError.value = ''
  if (!name.value.trim()) {
    fieldError.value = 'Anna hallille nimi.'
    return
  }
  const lat = nullableNumber(latitude.value)
  const lon = nullableNumber(longitude.value)
  if ((lat !== null && !Number.isFinite(lat)) || (lon !== null && !Number.isFinite(lon)) || (lat !== null && (lat < -90 || lat > 90)) || (lon !== null && (lon < -180 || lon > 180))) {
    fieldError.value = 'Tarkista leveys- ja pituusasteen arvo.'
    return
  }
  if ((lat === null) !== (lon === null)) {
    fieldError.value = 'Anna molemmat koordinaatit tai jätä molemmat tyhjäksi.'
    return
  }
  const input: GarageInputDto = { name: name.value.trim(), address: address.value.trim() || null, latitude: lat, longitude: lon }
  const result = await save(input, id.value)
  if (!result) return
  if (id.value === null) {
    await router.replace(`/garages/${result.id}?tab=settings&section=details`)
  }
  formGarageId.value = null
  await refresh()
}
</script>

<template>
  <div :class="props.embedded ? 'embedded-settings' : 'app-shell'">
    <AppSidebar v-if="!props.embedded" :garages="garages" />
    <component :is="props.embedded ? 'section' : 'main'" :id="props.embedded ? undefined : 'main-content'" :class="props.embedded ? undefined : 'main-content'"><header v-if="!props.embedded" class="topbar"><div class="breadcrumb">Miettis Park <span>/</span> <a href="/admin/garages">Hallit</a> <span>/</span> <strong>{{ isNew ? 'Uusi halli' : currentGarage?.name ?? 'Muokkaa hallia' }}</strong></div><a class="secondary-button back" href="/admin/garages">Takaisin listoihin</a></header>
      <div :class="props.embedded ? undefined : 'page-content'"><section class="intro"><div><p class="eyebrow">HALLIN TIEDOT</p><h1>{{ isNew ? 'Lisää halli' : 'Muokkaa hallia' }}</h1><p>Nimi, osoite ja sijainti.</p></div></section>
        <div v-if="!isNew && loading && !initialized" class="state-panel" role="status">Haetaan hallin tietoja…</div>
        <div v-else-if="!isNew && error && !currentGarage" class="state-panel state-error" role="alert"><strong>Hallin tietoja ei voitu hakea</strong><p>{{ error }}</p><button class="secondary-button" @click="refresh">Yritä uudelleen</button></div>
        <div v-else-if="!isNew && initialized && !currentGarage" class="state-panel" role="alert">Hallia ei löytynyt.</div>
        <template v-else-if="isNew || currentGarage"><form class="panel garage-form" @submit.prevent="submit">
          <p v-if="error" class="form-error" role="alert">{{ error }}</p><p v-if="fieldError" class="form-error" role="alert">{{ fieldError }}</p><p v-if="savedGarage" class="form-success" role="status">Hallin tiedot tallennettiin.</p>
          <p v-if="externalChange" role="status">Hallin tiedot ovat muuttuneet tai lomake poikkeaa palvelimesta. Syötteesi on säilytetty.</p><label>Nimi<input v-model="name" required maxlength="120" autocomplete="organization"></label>
          <label>Osoite<input v-model="address" :required="isNew" maxlength="300" autocomplete="street-address"></label>
          <div class="coordinates"><label>Leveysaste (latitude)<input v-model="latitude" type="number" step="any" min="-90" max="90" inputmode="decimal"></label><label>Pituusaste (longitude)<input v-model="longitude" type="number" step="any" min="-180" max="180" inputmode="decimal"></label></div>
          <p class="muted">Kartalla käytetään OpenFreeMapin julkista karttapalvelua. Uusi halli tarvitsee osoitteen ja karttapisteen. Vanhan hallin puuttuvan sijainnin voi säilyttää.</p>
          <div class="form-actions"><button class="primary-button" type="submit" :disabled="submitting">{{ submitting ? 'Tallennetaan…' : 'Tallenna tiedot' }}</button><a v-if="id !== null && !props.embedded" class="secondary-button" :href="`/admin/garages/${id}/layout`">Siirry pohjaeditoriin</a></div>
        </form>
        <section class="map-section"><h2>Valitse sijainti kartalta</h2><GarageMap :latitude="nullableNumber(latitude)" :longitude="nullableNumber(longitude)" :label="name || 'Uusi halli'" selectable @select="point => { latitude = point.latitude; longitude = point.longitude }" /></section></template>
        <footer v-if="!props.embedded" class="footer">Miettis Park · Halliylläpito</footer>
      </div>
    </component>
  </div>
</template>

<style scoped>
.back { display: inline-flex; align-items: center; text-decoration: none; }
.garage-form { display: grid; max-width: 760px; gap: 16px; }
.garage-form label { display: grid; gap: 6px; font-weight: 650; font-size: 13px; }
.garage-form input { width: 100%; min-height: 43px; padding: 0 11px; border: 1px solid #d6ded7; border-radius: 8px; background: white; color: var(--ink); }
.coordinates { display: grid; grid-template-columns: 1fr 1fr; gap: 14px; }
.form-actions { display: flex; align-items: center; gap: 12px; }
.form-actions a { display: inline-flex; min-height: 40px; align-items: center; padding: 0 12px; border: 1px solid var(--line); border-radius: 8px; color: var(--green); text-decoration: none; }
.form-error { margin: 0; color: var(--red); }
.form-success { margin: 0; color: var(--green); }
.muted { margin: 0; color: var(--muted); font-size: 12px; }
.map-section { margin-top: 20px; }
.map-section h2 { font-size: 19px; }
@media (max-width: 620px) { .coordinates { grid-template-columns: 1fr; } }
</style>
