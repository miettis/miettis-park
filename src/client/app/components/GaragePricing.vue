<script setup lang="ts">
import { inject, onMounted, ref } from 'vue'
import { garageApiKey, HttpGarageApi } from '../services/garageApi'
import type { FeeTierDto } from '../types/garage'
const props = defineProps<{ garageId: number }>()
const api = inject(garageApiKey, new HttpGarageApi())
const tiers = ref<FeeTierDto[]>([])
const expected = ref<FeeTierDto[]>([])
const busy = ref(false)
const loaded = ref(false)
const error = ref('')
const saved = ref(false)
async function load() {
  busy.value = true; error.value = ''; saved.value = false
  try {
    const result = await api.getPricing(props.garageId)
    tiers.value = structuredClone(result.tiers)
    expected.value = structuredClone(result.tiers)
    loaded.value = true
  } catch (cause) { error.value = cause instanceof Error ? cause.message : 'Hinnaston haku epäonnistui.' }
  finally { busy.value = false }
}
function add() {
  const last = tiers.value.at(-1)
  if (!last) return
  last.endDuration = '03:00:00'
  tiers.value.push({ amount: 0.3, perUnit: '00:10:00', startDuration: last.endDuration, endDuration: null })
}
function remove(index: number) {
  tiers.value.splice(index, 1)
  if (tiers.value.length) tiers.value[tiers.value.length - 1]!.endDuration = null
}
async function save() {
  busy.value = true; error.value = ''; saved.value = false
  try {
    const result = await api.savePricing(props.garageId, JSON.parse(JSON.stringify(tiers.value)), JSON.parse(JSON.stringify(expected.value)))
    tiers.value = structuredClone(result.tiers); expected.value = structuredClone(result.tiers); saved.value = true
  } catch (cause) { error.value = cause instanceof Error ? cause.message : 'Tallennus epäonnistui.' }
  finally { busy.value = false }
}
onMounted(() => void load())
</script>
<template>
  <section class="panel"><h2>Hinnasto</h2><p>Euroa per alkava aikayksikkö. Ajat muodossa hh:mm:ss (yli vuorokausi: d.hh:mm:ss). Portaat jatkuvat ilman aukkoja nollasta; viimeinen on rajaton. Hinnastoa voi muuttaa, kun aktiivisia hallikäyntejä ei ole.</p>
    <p v-if="error" role="alert">{{ error }}</p><p v-if="saved" role="status">Hinnasto tallennettiin.</p><p v-if="busy" role="status">Käsitellään hinnastoa…</p>
    <form v-if="loaded" @submit.prevent="save"><fieldset :disabled="busy"><div v-for="(tier, index) in tiers" :key="index" class="tier"><label>Alkaa<input v-model="tier.startDuration" required></label><label>Päättyy<input v-if="index < tiers.length - 1" v-model="tier.endDuration" required><span v-else>Rajaton</span></label><label>€/yksikkö<input v-model.number="tier.amount" type="number" min="0" step="0.01" required></label><label>Aikayksikkö<input v-model="tier.perUnit" required></label><button type="button" class="secondary-button" :disabled="tiers.length === 1" @click="remove(index)">Poista porras</button></div><div class="actions"><button type="button" class="secondary-button" :disabled="tiers.length >= 20" @click="add">Lisää porras</button><button class="primary-button" type="submit">Tallenna hinnasto</button></div></fieldset></form>
    <button type="button" class="secondary-button" :disabled="busy" @click="load">Lataa hinnasto uudelleen</button>
  </section>
</template>
<style scoped>
fieldset { border: 0; padding: 0; } .tier { display: flex; flex-wrap: wrap; align-items: end; gap: 12px; padding: 15px 0; border-bottom: 1px solid var(--line); } label { display: grid; gap: 6px; } input { width: 145px; padding: 10px; border: 1px solid var(--line); border-radius: 6px; } .actions { display: flex; gap: 12px; margin: 20px 0; }
</style>
