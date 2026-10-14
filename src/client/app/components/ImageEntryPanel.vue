<script setup lang="ts">
import { onBeforeUnmount, ref } from 'vue'

const props = defineProps<{ submitting: boolean, eventTimeValid: boolean, occurredAt?: string | null }>()
const emit = defineEmits<{ submit: [image: File] }>()
const selected = ref<File | null>(null)
const previewUrl = ref('')
const error = ref('')
const dragging = ref(false)
const input = ref<HTMLInputElement | null>(null)

function clearPreview(): void {
  if (previewUrl.value) URL.revokeObjectURL(previewUrl.value)
  previewUrl.value = ''
}
function choose(files: FileList | File[] | undefined): void {
  if (props.submitting || !files?.length) return
  error.value = ''
  const file = files[0]!
  if (files.length !== 1 || !['image/jpeg', 'image/png'].includes(file.type) || file.size === 0 || file.size > 8 * 1024 * 1024) {
    selected.value = null
    clearPreview()
    error.value = 'Valitse yksi JPEG- tai PNG-kuva, jonka koko on 1 tavusta 8 MiB:iin. Voit myös käyttää käsinsyöttöä.'
    return
  }
  selected.value = file
  clearPreview()
  previewUrl.value = URL.createObjectURL(file)
}
function onInput(event: Event): void {
  choose((event.target as HTMLInputElement).files ?? undefined)
  ;(event.target as HTMLInputElement).value = ''
}
function onDrop(event: DragEvent): void {
  dragging.value = false
  choose(event.dataTransfer?.files)
}
function submit(): void {
  if (selected.value && props.eventTimeValid && !props.submitting) emit('submit', selected.value)
}
onBeforeUnmount(clearPreview)
</script>

<template>
  <section class="panel image-entry-panel" aria-labelledby="image-entry-title">
    <div class="panel-heading"><div><p class="eyebrow">KUVA / PORTTI</p><h2 id="image-entry-title">Sisäänajo kuvasta</h2></div></div>
    <p class="muted">Valitse tai pudota yhden auton JPEG- tai PNG-kuva. Valinta ei lähetä kuvaa.</p>
    <p>Kuva lähetetään ulkoiselle OpenRouter- ja mallipalvelulle tunnistusta varten. Tämä sovellus ei säilytä kuvaa. Ulkoisen palvelun säilytysehdot voivat poiketa tästä.</p>
    <div class="drop-area" :class="{ 'drag-active': dragging }" @dragenter.prevent="dragging = !props.submitting" @dragover.prevent="dragging = !props.submitting" @dragleave.prevent="dragging = false" @drop.prevent="onDrop">
      <label for="plate-image">Pudota kuva tähän tai valitse tiedosto</label>
      <input id="plate-image" ref="input" aria-label="Valitse rekisterikilven kuva" type="file" accept="image/jpeg,image/png" :disabled="props.submitting" @change="onInput">
      <span v-if="dragging" role="status">Pudota kuva lähetysalueelle</span>
    </div>
    <img v-if="previewUrl" class="image-preview" :src="previewUrl" alt="Valitun kuvan esikatselu">
    <p>Tapahtuma-aika: {{ props.occurredAt ?? 'Palvelimen nykyhetki ennen tunnistusta' }}</p>
    <p v-if="error" role="alert">{{ error }}</p>
    <p>Jos tunnistus ei onnistu, valitse uusi kuva tai käytä yllä olevaa käsinsyöttöä.</p>
    <button class="primary-button" type="button" :disabled="!selected || props.submitting || !props.eventTimeValid" @click="submit">
      {{ props.submitting ? 'Käsitellään…' : 'Lue kilpi ja aja sisään' }}
    </button>
  </section>
</template>

<style scoped>
.drop-area { display: grid; gap: .75rem; padding: 1rem; border: 2px dashed #8195a2; border-radius: .5rem; }
.drag-active { border-color: #235e42; background: #e6f5ec; }
.image-preview { display: block; max-width: 100%; max-height: 18rem; object-fit: contain; margin: .75rem auto; }
</style>