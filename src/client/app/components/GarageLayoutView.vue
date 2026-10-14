<script setup lang="ts">
import { computed } from 'vue'
import type { GarageLayoutDto, LayoutCellDto, LayoutLotDto } from '../types/garage'

const props = defineProps<{
  layout: GarageLayoutDto
  selectedLotId?: number | null
}>()

const emit = defineEmits<{ select: [lotId: number] }>()

const lotsById = computed(() => new Map(props.layout.lots.map(lot => [lot.id, lot])))
const gridStyle = computed(() => ({
  gridTemplateColumns: `repeat(${props.layout.width}, minmax(54px, 1fr))`,
  gridTemplateRows: `repeat(${props.layout.height}, minmax(58px, auto))`
}))

function lotForCell(cell: LayoutCellDto): LayoutLotDto | undefined {
  return cell.lotId === null ? undefined : lotsById.value.get(cell.lotId)
}

function lotLabel(lot: LayoutLotDto | undefined): string {
  if (!lot) {
    return 'Ei tiedossa'
  }

  if (lot.currentStatus === 'Free') {
    return 'Vapaa'
  }

  if (lot.currentStatus === 'Occupied') {
    return 'Varattu'
  }

  return 'Ei tiedossa'
}

function cellLabel(cell: LayoutCellDto): string {
  if (cell.type === 'Road') {
    return `Rivi ${cell.y + 1}, sarake ${cell.x + 1}, ajoväylä`
  }

  if (cell.type === 'Empty') {
    return `Rivi ${cell.y + 1}, sarake ${cell.x + 1}, tyhjä`
  }

  const lot = lotForCell(cell)
  if (!lot) {
    return `Rivi ${cell.y + 1}, sarake ${cell.x + 1}, paikan tila ei tiedossa`
  }

  return `Rivi ${cell.y + 1}, sarake ${cell.x + 1}, paikka ${lot.number}, ${lotLabel(lot).toLowerCase()}`
}

function selectLot(cell: LayoutCellDto): void {
  if (cell.type !== 'ParkingLot' || cell.lotId === null) {
    return
  }

  emit('select', cell.lotId)
}
</script>

<template>
  <div class="garage-layout-view" role="grid" aria-label="Hallin paikkaruudukko" :style="gridStyle">
    <template v-for="(cell, index) in layout.cells" :key="`${cell.x}-${cell.y}-${index}`">
      <button
        v-if="cell.type === 'ParkingLot' && cell.lotId !== null"
        class="layout-cell parking-lot"
        :class="[
          `status-${lotForCell(cell)?.currentStatus ?? 'Unknown'}`,
          { selected: selectedLotId === cell.lotId }
        ]"
        :style="{ gridColumn: cell.x + 1, gridRow: cell.y + 1 }"
        type="button"
        role="gridcell"
        :aria-label="cellLabel(cell)"
        :aria-pressed="selectedLotId === cell.lotId"
        @click="selectLot(cell)"
      >
        <span>{{ lotForCell(cell)?.number ?? '—' }}</span>
        <small>{{ lotLabel(lotForCell(cell)) }}</small>
      </button>
      <div
        v-else
        class="layout-cell"
        :class="cell.type === 'Road' ? 'road-cell' : 'empty-cell'"
        :style="{ gridColumn: cell.x + 1, gridRow: cell.y + 1 }"
        role="gridcell"
        :aria-label="cellLabel(cell)"
      >
        {{ cell.type === 'Road' ? 'Ajoväylä' : 'Tyhjä' }}
      </div>
    </template>
  </div>
</template>

<style scoped>
.garage-layout-view { display: grid; width: max-content; max-width: 100%; gap: 6px; overflow: auto; }
.layout-cell { display: grid; min-width: 54px; min-height: 58px; place-content: center; justify-items: center; gap: 3px; padding: 4px; border: 1px solid #a8d4b8; border-radius: 7px; font: inherit; font-size: 12px; text-align: center; }
.parking-lot { cursor: pointer; }
.parking-lot:focus-visible { outline: 3px solid #47718c; outline-offset: 2px; }
.status-Free { border-color: #73b38f; background: #e7f3eb; color: #176d50; }
.status-Occupied { border-color: #c14c43; background: #fff0ef; color: #9a322c; }
.status-Unknown { border-color: #777; background: #e5e7e6; color: #444; }
.selected { box-shadow: inset 0 0 0 2px currentColor; }
.road-cell { border-color: #c6cbc7; background: #e7e9e7; color: #526259; }
.empty-cell { border-color: #dfe5dc; background: #f8faf7; color: #526259; }
</style>
