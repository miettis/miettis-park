import { computed, inject, onMounted, onBeforeUnmount, ref } from 'vue'
import { createResync, useGarageEvents } from '../services/garageEvents'
import { garageApiKey, HttpGarageApi, type GarageManagementApi } from '../services/garageApi'
import type { GarageDto, GarageInputDto, GarageLayoutDto, GarageLayoutInputDto, LayoutCellInputDto, LayoutCellType } from '../types/garage'

export function useGarageAdmin(api: GarageManagementApi = inject(garageApiKey, new HttpGarageApi()) as GarageManagementApi) {
  const garages = ref<GarageDto[]>([])
  const loading = ref(false)
  const initialized = ref(false)
  const error = ref('')
  const submitting = ref(false)
  const savedGarage = ref<GarageDto | null>(null)

  const sync = createResync(async signal => {
    loading.value = true
    const next = await api.getGarages(signal)
    return () => { garages.value = next; error.value = ''; initialized.value = true; loading.value = false }
  }, cause => { error.value = cause instanceof Error ? cause.message : 'Halleja ei voitu hakea.'; loading.value = false; initialized.value = true })
  const refresh = () => sync.refresh()
  useGarageEvents(() => refresh())
  onBeforeUnmount(() => sync.dispose())
  async function save(input: GarageInputDto, id: number | null): Promise<GarageDto | null> {
    submitting.value = true
    error.value = ''
    savedGarage.value = null
    try {
      savedGarage.value = id === null ? await api.createGarage(input) : await api.updateGarage(id, input)
      return savedGarage.value
    }
    catch (cause) {
      error.value = describeApiError(cause)
      return null
    }
    finally {
      submitting.value = false
    }
  }

  onMounted(() => void refresh())
  return { garages, loading, initialized, error, submitting, savedGarage, refresh, save }
}

export function describeApiError(cause: unknown): string {
  const error = cause as Error & { status?: number, problem?: { code?: string } }
  const code = error.problem?.code
  const message = error.message || 'Pyyntö epäonnistui.'
  if (code === 'LAYOUT_VERSION_CONFLICT') {
    return `${message} Hallin pohjaa on muutettu muualla. Luonnos on säilytetty; lataa palvelimen uusin pohja ennen uutta tallennusta.`
  }
  if (code === 'LOT_REMOVAL_CONFLICT') {
    return `${message} Paikkaa ei voi poistaa nykytilanteessa. Luonnos on säilytetty.`
  }
  if (code === 'LOT_NUMBER_CONFLICT') {
    return `${message} Paikkanumero on jo käytössä. Luonnos on säilytetty.`
  }
  if (code === 'GARAGE_NOT_FOUND' || code === 'LOT_NOT_FOUND' || error.status === 404) {
    return `${message} Kohdetta ei löytynyt. Luonnos on säilytetty eikä sitä ylikirjoitettu.`
  }
  if (error.status === 409) {
    return `${message} Ristiriita palvelimen nykytilan kanssa. Luonnos on säilytetty.`
  }
  return `${message} Luonnos on säilytetty.`
}

export type DraftCell = { type: LayoutCellType, lotId: number | null }
type EditorSnapshot = { width: number, height: number, cells: DraftCell[], lotNumbers: Record<number, number> }

function snapshot(layout: GarageLayoutDto): EditorSnapshot {
  const cellMap = new Map(layout.cells.map(cell => [`${cell.x},${cell.y}`, { type: cell.type, lotId: cell.lotId }]))
  const cells: DraftCell[] = []
  for (let y = 0; y < layout.height; y++) {
    for (let x = 0; x < layout.width; x++) {
      cells.push(cellMap.get(`${x},${y}`) ?? { type: 'Empty', lotId: null })
    }
  }
  return { width: layout.width, height: layout.height, cells, lotNumbers: Object.fromEntries(layout.lots.map(lot => [lot.id, lot.number])) }
}

export function useLayoutDraft(api: GarageManagementApi, garageId: number) {
  const original = ref<GarageLayoutDto | null>(null)
  const width = ref(1)
  const height = ref(1)
  const cells = ref<DraftCell[]>([{ type: 'Empty', lotId: null }])
  const lotNumbers = ref<Record<number, number>>({})
  const loading = ref(false)
  const saving = ref(false)
  const error = ref('')
  const notice = ref('')
  const initialized = ref(false)
  const mode = ref<LayoutCellType>('ParkingLot')
  const movingLotId = ref<number | null>(null)
  const dirty = computed(() => {
    if (!original.value) return false
    const base = snapshot(original.value)
    return width.value !== base.width || height.value !== base.height
      || JSON.stringify(cells.value) !== JSON.stringify(base.cells)
      || JSON.stringify(lotNumbers.value) !== JSON.stringify(base.lotNumbers)
  })

  function applyLayout(layout: GarageLayoutDto): void {
    original.value = layout
    const draft = snapshot(layout)
    width.value = draft.width
    height.value = draft.height
    cells.value = draft.cells
    lotNumbers.value = draft.lotNumbers
    notice.value = ''
    movingLotId.value = null
  }

  async function load(): Promise<void> {
    if (saving.value || loading.value) return
    if (dirty.value && !window.confirm('Hylätäänkö tallentamaton luonnos ja ladataanko pohja uudelleen?')) return
    loading.value = true
    error.value = ''
    try {
      applyLayout(await api.getLayout(garageId))
      await live.refresh()
    }
    catch (cause) {
      error.value = describeApiError(cause)
    }
    finally {
      initialized.value = true
      loading.value = false
    }
  }

  function resize(nextWidth: number, nextHeight: number): void {
    if (saving.value || loading.value) return
    if (!Number.isInteger(nextWidth) || !Number.isInteger(nextHeight) || nextWidth < 1 || nextWidth > 50 || nextHeight < 1 || nextHeight > 50) {
      error.value = 'Mitat ovat kokonaislukuja väliltä 1–50.'
      return
    }
    const outside = cells.value.filter((_, index) => index % width.value >= nextWidth || Math.floor(index / width.value) >= nextHeight)
    if (outside.some(cell => cell.type === 'ParkingLot')) {
      error.value = 'Siirrä tai poista ulkopuolelle jäävät paikat nimenomaisesti ennen pienennystä.'
      return
    }
    if (outside.some(cell => cell.type === 'Road') && !window.confirm('Pienennys poistaa ulkopuolelle jäävät ajorataruudut. Jatketaanko?')) return
    error.value = ''
    const safeWidth = Math.max(1, Math.min(50, Math.floor(nextWidth)))
    const safeHeight = Math.max(1, Math.min(50, Math.floor(nextHeight)))
    const next: DraftCell[] = Array.from({ length: safeWidth * safeHeight }, (_, index) => {
      const x = index % safeWidth
      const y = Math.floor(index / safeWidth)
      return x < width.value && y < height.value
        ? cells.value[y * width.value + x] ?? { type: 'Empty', lotId: null }
        : { type: 'Empty', lotId: null }
    })
    width.value = safeWidth
    height.value = safeHeight
    cells.value = next
  }

  function paint(index: number): void {
    if (saving.value || loading.value) return
    if (!Number.isInteger(index) || index < 0 || index >= cells.value.length) return
    const cell = cells.value[index]
    if (!cell) return
    if (movingLotId.value !== null) {
      if (cell.type !== 'Empty') return
      cells.value[index] = { type: 'ParkingLot', lotId: movingLotId.value }
      const oldIndex = cells.value.findIndex((candidate, candidateIndex) => candidateIndex !== index && candidate.lotId === movingLotId.value)
      if (oldIndex >= 0) cells.value[oldIndex] = { type: 'Empty', lotId: null }
      movingLotId.value = null
      return
    }
    if (mode.value !== 'ParkingLot') {
      if (cell.lotId !== null) {
        const lot = original.value?.lots.find(item => item.id === cell.lotId)
        if (lot && !lot.canRemove) {
          error.value = `Paikka ${lot.number} ei ole poistettavissa nykytilanteessa.`
          return
        }
      }
      cells.value[index] = { type: mode.value, lotId: null }
      return
    }
    if (cell.type !== 'ParkingLot') {
      cells.value[index] = { type: 'ParkingLot', lotId: null }
    }
  }

  function moveLot(lotId: number): void {
    if (saving.value || !cells.value.some(cell => cell.lotId === lotId)) return
    movingLotId.value = lotId
    mode.value = 'ParkingLot'
    notice.value = `Valitse ruudukosta tyhjä ruutu paikan ${lotNumbers.value[lotId] ?? ''} siirtämiseksi.`
  }

  function setLotNumber(lotId: number, number: number): void {
    if (saving.value) return
    if (Number.isInteger(number) && number > 0) lotNumbers.value[lotId] = number
  }

  function undo(): void {
    if (saving.value) return
    if (original.value) applyLayout(original.value)
    error.value = ''
    movingLotId.value = null
  }

  const previewNumbers = computed(() => {
    const used = new Set(cells.value.flatMap(cell => cell.lotId === null ? [] : [lotNumbers.value[cell.lotId]!]))
    let candidate = 1
    return cells.value.map(cell => {
      if (cell.type !== 'ParkingLot') return null
      if (cell.lotId !== null) return lotNumbers.value[cell.lotId]
      while (used.has(candidate)) candidate++
      used.add(candidate)
      return candidate
    })
  })

  function createPayload(): GarageLayoutInputDto {
    const baseLots = original.value?.lots ?? []
    const activeOldIds = new Set(cells.value.flatMap(cell => cell.lotId === null ? [] : [cell.lotId]))
    const removedLotIds = baseLots.filter(lot => !activeOldIds.has(lot.id)).map(lot => lot.id).sort((a, b) => a - b)
    const payloadCells: LayoutCellInputDto[] = cells.value.map((cell, index) => {
      const x = index % width.value
      const y = Math.floor(index / width.value)
      return {
        x,
        y,
        type: cell.type,
        lotId: cell.lotId,
        number: cell.lotId === null ? null : lotNumbers.value[cell.lotId] ?? null
      }
    })
    return { expectedVersion: original.value?.version ?? 0, width: width.value, height: height.value, cells: payloadCells, removedLotIds }
  }

  async function save(): Promise<boolean> {
    if (!original.value || saving.value || loading.value) return false
    saving.value = true
    error.value = ''
    notice.value = ''
    try {
      const updated = await api.saveLayout(garageId, createPayload())
      applyLayout(updated)
      notice.value = 'Pohja tallennettiin.'
      return true
    }
    catch (cause) {
      error.value = describeApiError(cause)
      return false
    }
    finally {
      saving.value = false
    }
  }

  const live = createResync(async signal => {
    const next = await api.getLayout(garageId, signal)
    return () => {
      if (!original.value) return
      if (next.version !== original.value.version) notice.value = 'Hallin pohjaa on muutettu muualla. Luonnos säilyy; lataa pohja uudelleen.'
      const meta = new Map(next.lots.map(lot => [lot.id, lot]))
      original.value = { ...original.value, lots: original.value.lots.map(lot => {
        const current = meta.get(lot.id)
        return { ...lot, currentStatus: current?.currentStatus ?? lot.currentStatus, canRemove: current?.canRemove ?? false }
      }) }
    }
  }, cause => { error.value = describeApiError(cause) })
  useGarageEvents(change => { if (!change || change.garageId === garageId) return live.refresh() })
  onBeforeUnmount(() => live.dispose())
  onMounted(() => void load())
  return { original, width, height, cells, previewNumbers, lotNumbers, loading, saving, error, notice, initialized, mode, movingLotId, dirty, load, resize, paint, moveLot, setLotNumber, undo, createPayload, save }
}
