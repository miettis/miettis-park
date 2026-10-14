<script setup lang="ts">
import { ref } from 'vue'

const props = defineProps<{ submitting: boolean, full: boolean }>()
const emit = defineEmits<{ enter: [licenseNumber: string], exit: [licenseNumber: string] }>()
const mode = ref<'entry' | 'exit'>('entry')
const licenseNumber = ref('')

function submit(): void {
  const normalized = licenseNumber.value.trim()
  if (!normalized || props.submitting) {
    return
  }
  if (mode.value === 'entry') {
    emit('enter', normalized)
  } else {
    emit('exit', normalized)
  }
}
</script>

<template>
  <section class="panel" aria-labelledby="gate-title">
    <div class="panel-heading">
      <div>
        <p class="eyebrow">01 / PORTTI</p>
        <h2 id="gate-title">Hallin portti</h2>
      </div>
      <span class="tag">Simulaatio</span>
    </div>
    <p class="muted">Kirjaa hallikäynti rekisteritunnuksella. Paikan valinta on erillinen havainto.</p>
    <div v-if="props.full" class="full-message" role="status">
      Halli on täynnä, mutta sisäänajohavainnon voi silti kirjata.
    </div>
    <div class="segmented" aria-label="Portin toiminto">
      <button type="button" :aria-pressed="mode === 'entry'" :class="{ active: mode === 'entry' }" @click="mode = 'entry'">
        Sisäänajo
      </button>
      <button type="button" :aria-pressed="mode === 'exit'" :class="{ active: mode === 'exit' }" @click="mode = 'exit'">
        Ulosajo
      </button>
    </div>
    <form class="gate-form" @submit.prevent="submit">
      <label for="license-number">Rekisterinumero</label>
      <input
        id="license-number"
        v-model="licenseNumber"
        autocomplete="off"
        maxlength="20"
        placeholder="esim. ABC-123"
        required
      >
      <button class="primary-button" type="submit" :disabled="props.submitting || !licenseNumber.trim()">
        {{ props.submitting ? 'Tallennetaan…' : mode === 'entry' ? 'Kirjaa sisäänajo →' : 'Kirjaa ulosajo →' }}
      </button>
    </form>
  </section>
</template>
