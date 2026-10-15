<script setup lang="ts">
import { inject, onBeforeUnmount, onMounted, ref } from 'vue'
import { garageApiKey, HttpGarageApi } from '../services/garageApi'
import { useGarageEvents } from '../services/garageEvents'
import type { VisitHistoryDto } from '../types/garage'
const props = defineProps<{ garageId: number }>()
const api = inject(garageApiKey, new HttpGarageApi())
const visits = ref<VisitHistoryDto[]>([])
const error = ref('')
const loading = ref(false)
const skip = ref(0)
let request: AbortController | undefined
async function load(nextSkip = skip.value) {
  request?.abort()
  const current = new AbortController()
  request = current
  loading.value = true
  error.value = ''
  try {
    const result = await api.getVisitHistory(props.garageId, nextSkip, current.signal)
    if (!current.signal.aborted) { visits.value = result; skip.value = nextSkip }
  } catch (cause) {
    if (!current.signal.aborted) error.value = cause instanceof Error ? cause.message : 'Lokin haku epäonnistui.'
  } finally { if (!current.signal.aborted) loading.value = false }
}
useGarageEvents(() => load())
onMounted(() => void load())
onBeforeUnmount(() => request?.abort())
const time = (value: string | null) => value ? new Date(value).toLocaleString('fi-FI') : '–'
const status = { Active: 'Aktiivinen', Completed: 'Päättynyt', Interrupted: 'Keskeytynyt' }
</script>
<template>
  <section id="panel-log" class="panel" role="tabpanel" aria-labelledby="tab-log">
    <div class="panel-heading"><h2>Vierailut</h2><button type="button" class="secondary-button" :disabled="loading" @click="load()">Päivitä loki</button></div>
    <p v-if="error" role="alert">{{ error }}</p><p v-if="loading" role="status">Haetaan vierailuja…</p>
    <div class="table-scroll"><table><thead><tr><th>Rekisteri</th><th>Saapuminen</th><th>Poistuminen / suljettu</th><th>Tila</th><th>Hinta</th></tr></thead><tbody><tr v-for="visit in visits" :key="visit.visitId"><td>{{ visit.licenseNumber }}</td><td>{{ time(visit.startAt) }}</td><td>{{ time(visit.endAt ?? visit.closedAt) }}</td><td>{{ status[visit.status] }}<small v-if="visit.closureReason"> · {{ visit.closureReason }}</small></td><td>{{ visit.cost === null ? '–' : visit.cost.toLocaleString('fi-FI', { style: 'currency', currency: 'EUR' }) }}</td></tr></tbody></table></div>
    <p v-if="!loading && !error && visits.length === 0">Ei vierailuja tällä sivulla.</p>
    <div class="pagination"><button type="button" class="secondary-button" :disabled="loading || skip === 0" @click="load(Math.max(0, skip - 50))">Edellinen</button><span>Sivu {{ skip / 50 + 1 }}</span><button type="button" class="secondary-button" :disabled="loading || visits.length < 50" @click="load(skip + 50)">Seuraava</button></div>
  </section>
</template>
<style scoped>
.table-scroll { overflow-x: auto; } table { width: 100%; border-collapse: collapse; } th, td { padding: 12px; border-bottom: 1px solid var(--line); text-align: left; } .pagination { display: flex; gap: 12px; align-items: center; margin-top: 20px; }
</style>
