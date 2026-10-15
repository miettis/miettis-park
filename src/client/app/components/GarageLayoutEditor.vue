<script setup lang="ts">
import AppSidebar from './AppSidebar.vue'
import { computed, inject, onBeforeUnmount, onMounted, ref } from 'vue'
import { onBeforeRouteLeave, useRoute } from 'vue-router'
import { useLayoutDraft } from '../composables/useGarageAdmin'
import { garageApiKey, HttpGarageApi } from '../services/garageApi'
import type { LayoutCellType } from '../types/garage'

const props = defineProps<{ garageId?: number, embedded?: boolean }>()
const route = useRoute()
const garageId = props.garageId ?? Number(route.params.id)
const api = inject(garageApiKey, new HttpGarageApi())
const editor = useLayoutDraft(api, garageId)
const dragging = ref(false)
const visited = new Set<number>()
const widthInput = computed(() => editor.width.value)
const heightInput = computed(() => editor.height.value)
const brushOptions: { type: LayoutCellType, label: string }[] = [
  { type: 'Empty', label: 'Pyyhekumi' },
  { type: 'Road', label: 'Ajorata' },
  { type: 'ParkingLot', label: 'Pysäköintipaikka' }
]

function resizeWidth(value: Event): void {
  const next = Number((value.target as HTMLInputElement).value)
  editor.resize(next, editor.height.value)
  ;(value.target as HTMLInputElement).value = String(editor.width.value)
}
function resizeHeight(value: Event): void {
  const next = Number((value.target as HTMLInputElement).value)
  editor.resize(editor.width.value, next)
  ;(value.target as HTMLInputElement).value = String(editor.height.value)
}
function cellLabel(index: number): string {
  const cell = editor.cells.value[index]
  const x = index % editor.width.value + 1
  const y = Math.floor(index / editor.width.value) + 1
  if (cell?.lotId !== null && cell?.lotId !== undefined) {
    return `Rivi ${y}, sarake ${x}, pysäköintipaikka ${editor.lotNumbers.value[cell.lotId] ?? ''}, ${lotStatus(cell.lotId)}`
  }
  const type = cell?.type === 'Road' ? 'ajoväylä' : cell?.type === 'ParkingLot' ? 'uusi pysäköintipaikka' : 'tyhjä'
  return `Rivi ${y}, sarake ${x}, ${type}`
}
function lotStatus(lotId: number): string {
  const status = editor.original.value?.lots.find(lot => lot.id === lotId)?.currentStatus
  return status === 'Occupied' ? 'Käytössä' : status === 'Unknown' ? 'Tuntematon' : 'Vapaa'
}
function keydown(event: KeyboardEvent, index: number): void {
  const dx = event.key === 'ArrowRight' ? 1 : event.key === 'ArrowLeft' ? -1 : 0
  const dy = event.key === 'ArrowDown' ? 1 : event.key === 'ArrowUp' ? -1 : 0
  if (dx || dy) {
    event.preventDefault()
    const x = index % editor.width.value
    const y = Math.floor(index / editor.width.value)
    const nextX = Math.max(0, Math.min(editor.width.value - 1, x + dx))
    const nextY = Math.max(0, Math.min(editor.height.value - 1, y + dy))
    document.querySelector<HTMLButtonElement>(`[data-cell-index="${nextY * editor.width.value + nextX}"]`)?.focus()
  }
  else if (event.key === ' ' || event.key === 'Enter') {
    event.preventDefault()
    editor.paint(index)
  }
}
function pointerDown(index: number, event: PointerEvent): void {
  if (event.button !== 0) return
  event.preventDefault()
  ;(event.currentTarget as HTMLElement).focus({ preventScroll: true })
  dragging.value = true
  visited.clear()
  visited.add(index)
  editor.paint(index)
}
function pointerEnter(index: number): void {
  if (dragging.value && !visited.has(index)) {
    visited.add(index)
    editor.paint(index)
  }
}
function pointerMove(event: PointerEvent): void {
  if (!dragging.value) return
  const cell = document.elementFromPoint?.(event.clientX, event.clientY)?.closest<HTMLElement>('[data-cell-index]')
  if (cell && (event.currentTarget as HTMLElement).contains(cell)) pointerEnter(Number(cell.dataset.cellIndex))
}
function stopDragging(): void {
  dragging.value = false
}
function clickCell(index: number, event: MouseEvent): void {
  if (event.detail === 0) editor.paint(index)
}
function beforeUnload(event: BeforeUnloadEvent): void {
  if (!editor.dirty.value) return
  event.preventDefault()
  event.returnValue = ''
}

onMounted(() => {
  window.addEventListener('pointerup', stopDragging)
  window.addEventListener('pointercancel', stopDragging)
})
onBeforeUnmount(() => {
  window.removeEventListener('pointerup', stopDragging)
  window.removeEventListener('pointercancel', stopDragging)
  window.removeEventListener('beforeunload', beforeUnload)
})

onMounted(() => window.addEventListener('beforeunload', beforeUnload))
onBeforeRouteLeave(() => !editor.dirty.value || window.confirm('Pohjassa on tallentamattomia muutoksia. Poistutaanko sivulta?'))
</script>

<template>
  <div :class="props.embedded ? 'embedded-settings' : 'app-shell'">
    <AppSidebar v-if="!props.embedded" />
    <component :is="props.embedded ? 'section' : 'main'" :id="props.embedded ? undefined : 'main-content'" :class="props.embedded ? undefined : 'main-content'"><header v-if="!props.embedded" class="topbar"><div class="breadcrumb">Miettis Park <span>/</span> <a href="/admin/garages">Hallit</a> <span>/</span> <strong>Pohjaeditori</strong></div><a class="secondary-button back" :href="`/admin/garages/${garageId}`">Hallin tiedot</a></header>
      <div :class="props.embedded ? undefined : 'page-content'"><section class="intro"><div><p class="eyebrow">HALLI #{{ garageId }} · POHJA</p><h1>Ruudukkoeditori</h1><p>Maalaa ruutuja hiirellä tai valitse ne näppäimistöllä. Muutokset tallentuvat vasta tallennettaessa.</p></div></section>
        <div v-if="editor.loading.value && !editor.initialized.value" class="state-panel" role="status">Haetaan pohjaa…</div>
        <div v-else-if="editor.error.value && !editor.original.value" class="state-panel state-error" role="alert"><strong>Pohjaa ei voitu hakea</strong><p>{{ editor.error.value }}</p><button class="secondary-button" @click="editor.load">Yritä uudelleen</button></div>
        <div v-else-if="editor.initialized.value && !editor.original.value" class="state-panel state-error" role="alert">Hallia tai sen pohjaa ei löytynyt.</div>
        <template v-else-if="editor.original.value">
          <button class="secondary-button" type="button" :disabled="editor.loading.value || editor.saving.value" @click="editor.load">Lataa pohja uudelleen</button>
          <p v-if="editor.error.value" class="editor-error" role="alert">{{ editor.error.value }}</p><p v-if="editor.notice.value" class="editor-success" role="status">{{ editor.notice.value }}</p>
          <section class="panel editor-tools" aria-label="Ruudukon asetukset">
            <label>Leveys<input aria-label="Ruudukon leveys" type="number" min="1" max="50" :value="widthInput" @change="resizeWidth"></label>
            <label>Korkeus<input aria-label="Ruudukon korkeus" type="number" min="1" max="50" :value="heightInput" @change="resizeHeight"></label>
            <fieldset><legend>Maalaustyökalu</legend><label v-for="option in brushOptions" :key="option.type" class="radio-label"><input v-model="editor.mode.value" type="radio" name="brush" :value="option.type">{{ option.label }}</label></fieldset>
            <p v-if="editor.movingLotId.value !== null" role="status">Valitse tyhjä ruutu siirrettäväksi kohteeksi. <button class="text-button" @click="editor.movingLotId.value = null">Peruuta siirto</button></p>
          </section>
          <section class="panel grid-panel" aria-labelledby="grid-heading"><div class="panel-heading"><div><h2 id="grid-heading">Hallin pohja</h2><p class="muted">{{ editor.width.value }} × {{ editor.height.value }} ruutua · versio {{ editor.original.value.version }}</p></div><span v-if="editor.dirty.value" class="dirty-pill">Tallentamattomia muutoksia</span></div>
            <div class="grid-scroll"><div class="layout-grid" role="grid" aria-label="Hallin pohja" :style="{ gridTemplateColumns: `repeat(${editor.width.value}, 64px)` }" @pointermove="pointerMove">
              <button v-for="(cell, index) in editor.cells.value" :key="`${index}-${editor.width.value}`" :data-cell-index="index" class="grid-cell" :class="[`cell-${cell.type}`, { 'cell-lot': cell.type === 'ParkingLot', 'cell-occupied': cell.lotId !== null && lotStatus(cell.lotId) === 'Käytössä', 'cell-unknown': cell.lotId !== null && lotStatus(cell.lotId) === 'Tuntematon' }]" type="button" role="gridcell" :aria-label="cellLabel(index)" @pointerdown="pointerDown(index, $event)" @pointerenter="pointerEnter(index)" @click="clickCell(index, $event)" @keydown="keydown($event, index)">
                <span v-if="cell.type === 'Road'">Ajoväylä</span><span v-else-if="cell.type === 'ParkingLot'">{{ cell.lotId === null ? `Uusi ${editor.previewNumbers.value[index]}` : editor.lotNumbers.value[cell.lotId] }}</span><span v-else aria-hidden="true">·</span>
                <small v-if="cell.lotId !== null">{{ lotStatus(cell.lotId) }}</small>
              </button>
            </div></div>
          </section>
          <section class="panel lots-panel" aria-labelledby="lot-heading"><div class="panel-heading"><div><h2 id="lot-heading">Paikkanumerot</h2><p class="muted">Uudet paikat numeroidaan automaattisesti tallennuksessa. Olemassa olevan paikan numeroa voi muuttaa.</p></div></div>
            <p v-if="editor.original.value.lots.length === 0" class="muted">Pohjassa ei ole ennestään paikkoja.</p>
            <div v-else class="lot-list"><div v-for="lot in editor.original.value.lots" :key="lot.id" class="lot-row"><span>Paikka #{{ lot.number }} · {{ lot.currentStatus === 'Free' ? 'Vapaa' : lot.currentStatus === 'Occupied' ? 'Käytössä' : 'Ei tiedossa' }}</span><label>Numero<input type="number" min="1" :value="editor.lotNumbers.value[lot.id]" @change="editor.setLotNumber(lot.id, Number(($event.target as HTMLInputElement).value))"></label><button class="secondary-button" type="button" @click="editor.moveLot(lot.id)">Siirrä ruutua</button><span v-if="!lot.canRemove" class="muted">Ei poistettavissa</span></div></div>
          </section>
          <div class="editor-actions"><button class="secondary-button" type="button" :disabled="!editor.dirty.value || editor.saving.value" @click="editor.undo">Peruuta muutokset</button><button class="primary-button" type="button" :disabled="!editor.dirty.value || editor.saving.value" @click="editor.save">{{ editor.saving.value ? 'Tallennetaan…' : 'Tallenna pohja' }}</button></div>
        </template>
        <footer v-if="!props.embedded" class="footer">Miettis Park · Ruudukkoeditori</footer>
      </div>
    </component>
  </div>
</template>

<style scoped>
.back { display: inline-flex; align-items: center; text-decoration: none; }
.editor-tools { display: flex; flex-wrap: wrap; align-items: end; gap: 14px; margin-bottom: 15px; }
.editor-tools > label, .lot-row > label { display: grid; gap: 5px; font-size: 12px; font-weight: 700; }
.editor-tools input[type=number], .lot-row input { width: 95px; min-height: 37px; padding: 0 8px; border: 1px solid var(--line); border-radius: 7px; }
.editor-tools fieldset { display: flex; flex-wrap: wrap; gap: 10px; border: 1px solid var(--line); border-radius: 8px; }
.editor-tools legend { font-size: 12px; font-weight: 700; }
.radio-label { display: flex; gap: 5px; align-items: center; font-size: 12px; }
.editor-tools p { flex-basis: 100%; margin: 0; color: var(--amber); }
.grid-panel { margin-bottom: 15px; }
.grid-scroll { max-width: 100%; overflow: auto; padding: 4px; }
.layout-grid { display: grid; width: max-content; gap: 5px; }
.grid-cell { display: grid; min-height: 58px; place-items: center; padding: 3px; border: 1px solid #dfe5dc; border-radius: 7px; background: #f8faf7; color: #526259; touch-action: none; cursor: crosshair; font-size: 10px; font-weight: 700; }
.cell-Road { background: #e7e9e7; }
.cell-ParkingLot { border-color: #a8d4b8; background: #e7f3eb; color: #176d50; }
.cell-lot { border-color: #73b38f; }
.cell-occupied { background: #fff0ef; border-color: #c14c43; color: #9a322c; }
.cell-unknown { background: #e5e7e6; border-color: #777; color: #444; }
.grid-cell:focus-visible { outline: 3px solid #47718c; }
.dirty-pill { padding: 6px 10px; border-radius: 20px; background: #fff8e8; color: #976417; font-size: 12px; }
.lots-panel { margin-bottom: 15px; }
.lot-list { display: grid; gap: 9px; }
.lot-row { display: flex; flex-wrap: wrap; align-items: center; gap: 12px; padding: 10px 0; border-top: 1px solid var(--line); }
.lot-row > span:first-child { min-width: 180px; }
.lot-row .secondary-button { min-height: 36px; padding: 0 10px; }
.editor-actions { display: flex; justify-content: flex-end; gap: 10px; }
.editor-error, .editor-success { padding: 12px; border-radius: 8px; background: #fff3f1; color: var(--red); }
.editor-success { background: #f0f8f2; color: var(--green); }
.text-button { border: 0; background: transparent; color: var(--green); text-decoration: underline; cursor: pointer; }
@media (max-width: 620px) { .lot-row { align-items: flex-start; flex-direction: column; } .editor-actions { justify-content: stretch; } .editor-actions button { flex: 1; } }
</style>
