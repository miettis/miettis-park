<script setup lang="ts">
import { computed } from 'vue'
import { formatOffset, toEventTime, toLocalInputValue } from '../utils/eventTime'

const props = defineProps<{ modelValue: string, disabled?: boolean }>()
const emit = defineEmits<{ 'update:modelValue': [value: string] }>()
const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone || 'paikallinen aikavyöhyke'
const result = computed(() => toEventTime(props.modelValue))
const offset = computed(() => {
  if (props.modelValue && !result.value.error) {
    return formatOffset(new Date(props.modelValue))
  }
  return formatOffset(new Date())
})

function update(value: string): void {
  emit('update:modelValue', value)
}

function clearTime(): void {
  update('')
}

function addMinutes(minutes: number): void {
  if (props.disabled || result.value.error) return
  const base = result.value.occurredAt ? new Date(result.value.occurredAt).getTime() : Date.now()
  update(toLocalInputValue(new Date(base + minutes * 60_000)))
}
</script>

<template>
  <section class="event-time panel" aria-labelledby="event-time-heading">
    <div class="panel-heading">
      <div>
        <p class="eyebrow">SIMULAATION ASETUS</p>
        <h2 id="event-time-heading">Tapahtuman aika</h2>
      </div>
      <span class="tag">Tyhjä = palvelimen aika</span>
    </div>
    <label for="event-time">Tapahtuman aika (paikallinen)</label>
    <input
      id="event-time"
      type="datetime-local"
      step="1"
      :value="props.modelValue"
      :disabled="props.disabled"
      :aria-invalid="result.error ? 'true' : undefined"
      :aria-describedby="result.error ? 'event-time-error event-time-zone' : 'event-time-zone'"
      @input="update(($event.target as HTMLInputElement).value)"
    >
    <p v-if="result.error" id="event-time-error" class="event-time-error" role="alert">{{ result.error }}</p>
    <p id="event-time-zone" class="muted">Aikavyöhyke: {{ timeZone }} · Valitun ajan offset: {{ offset }}</p>
    <p v-if="result.occurredAt">Valittu tapahtuma-aika: {{ result.occurredAt }}</p>
    <p v-else-if="!result.error">Seuraava komento käyttää palvelimen nykyhetkeä.</p>
    <div class="event-time-actions" aria-label="Tapahtuma-ajan pikavalinnat">
      <button type="button" class="secondary-button" :disabled="props.disabled" @click="clearTime">Nykyhetki</button>
      <button type="button" class="secondary-button" :disabled="props.disabled" @click="addMinutes(10)">+10 min</button>
      <button type="button" class="secondary-button" :disabled="props.disabled" @click="addMinutes(60)">+1 h</button>
      <button type="button" class="secondary-button" :disabled="props.disabled" @click="addMinutes(180)">+3 h</button>
    </div>
  </section>
</template>

<style scoped>
.event-time {
  margin-bottom: 16px;
}

.event-time > label {
  display: block;
  margin-bottom: 6px;
  font-size: 0.9rem;
  font-weight: 650;
}

.event-time > input {
  width: min(100%, 25rem);
  padding: 10px 12px;
  border: 1px solid #cbd4c8;
  border-radius: 8px;
  font: inherit;
}

.event-time > input[aria-invalid="true"] {
  border-color: #a32929;
}

.event-time-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.event-time-error {
  color: #a32929;
}
</style>
