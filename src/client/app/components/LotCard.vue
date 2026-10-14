<script setup lang="ts">
import { ref, watch } from 'vue'
import type { ActiveVisitDto, LotDto } from '../types/garage'

const props = defineProps<{
  lot: LotDto
  visits: ActiveVisitDto[]
  submitting: boolean
}>()
const emit = defineEmits<{
  enter: [lotId: number, visitId: number | null]
  exit: [lotId: number]
}>()
const expanded = ref(false)
const visitId = ref('')
const statusLabels = { Free: 'Vapaa', Occupied: 'Varattu', Unknown: 'Ei tiedossa' } as const
const canEnter = () => props.lot.currentStatus === 'Free'

watch(() => props.lot.currentStatus, (status) => {
  if (status !== 'Free') {
    expanded.value = false
  }
})

function enter(): void {
  if (!canEnter() || props.submitting) {
    return
  }
  emit('enter', props.lot.id, visitId.value ? Number(visitId.value) : null)
  expanded.value = false
}
</script>

<template>
  <article class="lot-card" :class="`lot-${props.lot.currentStatus.toLowerCase()}`">
    <div class="lot-number">PAIKKA {{ String(props.lot.number).padStart(2, '0') }}</div>
    <span class="status-pill" :class="`status-${props.lot.currentStatus.toLowerCase()}`">
      {{ statusLabels[props.lot.currentStatus] }}
    </span>
    <p class="lot-detail">
      {{ props.lot.currentStatus === 'Free' ? 'Valmis käyttöön' : props.lot.currentStatus === 'Unknown' ? 'Tilanne ei ole luotettavasti tiedossa.' : 'Paikkahavainto kertoo käytöstä.' }}
    </p>
    <template v-if="props.lot.currentStatus === 'Free'">
      <button v-if="!expanded" class="secondary-button lot-action" type="button" @click="expanded = true">
        Kirjaa käyttö
      </button>
      <form v-else class="lot-entry-form" @submit.prevent="enter">
        <label :for="`visit-${props.lot.id}`">Yhdistä hallikäyntiin <span>(valinnainen)</span></label>
        <select :id="`visit-${props.lot.id}`" v-model="visitId">
          <option value="">Ei yhdistetä</option>
          <option v-for="visit in props.visits" :key="visit.visitId" :value="String(visit.visitId)">
            {{ visit.licenseNumber }} · käynti {{ visit.visitId }}
          </option>
        </select>
        <div class="lot-form-actions">
          <button class="primary-button" type="submit" :disabled="props.submitting">Vahvista</button>
          <button class="text-button" type="button" @click="expanded = false">Peruuta</button>
        </div>
      </form>
    </template>
    <button
      v-else
      class="secondary-button lot-action"
      type="button"
      :disabled="props.submitting"
      @click="emit('exit', props.lot.id)"
    >
      Simuloi paikan vapautuminen
    </button>
  </article>
</template>
