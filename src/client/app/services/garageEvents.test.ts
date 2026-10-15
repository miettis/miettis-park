import { mount, flushPromises } from '@vue/test-utils'
import { defineComponent, h, ref } from 'vue'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createGarageEvents, createResync, garageEvents } from './garageEvents'
import { useGarageDashboard } from '../composables/useGarageDashboard'
import { useLayoutDraft } from '../composables/useGarageAdmin'
import GarageSimulation from '../components/GarageSimulation.vue'
import IndexPage from '../pages/index.vue'
import { garageApiKey, type GarageApi } from './garageApi'
import type { GarageLayoutDto, ReceiptDto } from '../types/garage'

class Source {
  static instances: Source[] = []
  onopen: (() => void) | null = null
  onerror: (() => void) | null = null
  listener: ((event: { data: string }) => void) | null = null
  close = vi.fn()
  constructor(public url: string) { Source.instances.push(this) }
  addEventListener(_type: string, listener: (event: { data: string }) => void) { this.listener = listener }
  emit(garageId = 1, kind = 'occupancy') { this.listener?.({ data: JSON.stringify({ garageId, kind }) }) }
}
function deferred<T>() { let resolve!: (value: T) => void; const promise = new Promise<T>(done => { resolve = done }); return { promise, resolve } }
function layout(id = 1, status: 'Free' | 'Occupied' = 'Free'): GarageLayoutDto {
  return { garageId: id, version: 1, width: 1, height: 1, cells: [{ x: 0, y: 0, type: 'ParkingLot', lotId: id * 10 }], lots: [{ id: id * 10, number: 1, currentStatus: status, canRemove: status === 'Free' }] }
}
function stub() {
  return {
    getGarages: vi.fn(async () => [1, 2].map(id => ({ id, name: `Halli ${id}`, address: 'Katu', latitude: null, longitude: null, capacity: 1, occupiedLots: 0, freeLots: 1, unknownLots: 0, isFull: false }))),
    getLayout: vi.fn(async (id: number) => layout(id)),
    getAvailability: vi.fn(), getActiveVisits: vi.fn(async () => []), getActiveParkings: vi.fn(async () => []),
    enterGarage: vi.fn(async () => ({ visitId: 1, licenseNumber: 'ABC', startAt: '2026-10-14T10:00:00Z', outcome: 'Entry' as const, gateOpened: true, effectiveOccurredAt: '2026-10-14T10:00:00Z' })),
    exitGarage: vi.fn(), enterGarageFromImage: vi.fn(), enterLot: vi.fn(), exitLot: vi.fn(), createGarage: vi.fn(), updateGarage: vi.fn(), saveLayout: vi.fn()
  }
}
beforeEach(() => { Source.instances = []; vi.stubGlobal('EventSource', Source) })
afterEach(() => { garageEvents.dispose(); vi.unstubAllGlobals() })

describe('shared SSE invalidation and race-safe reads', () => {
  it('opens one source, validates anonymous events, reconnects, unsubscribes and disposes', async () => {
    const events = createGarageEvents(url => new Source(url) as unknown as EventSource)
    const first = vi.fn(), second = vi.fn()
    const unsubscribe = events.subscribe(first)
    events.subscribe(second)
    events.start(); events.start()
    expect(Source.instances).toHaveLength(1)
    const source = Source.instances[0]!
    expect(source.url).toBe('/api/garages/events')
    source.onopen?.(); source.emit(2, 'layout')
    expect(first).toHaveBeenCalledWith(null)
    expect(second).toHaveBeenCalledWith({ garageId: 2, kind: 'layout' })
    unsubscribe(); source.onerror?.()
    expect(events.connected.value).toBe(false)
    source.onopen?.()
    expect(second).toHaveBeenCalledTimes(3)
    expect(first).toHaveBeenCalledTimes(2)
    events.dispose()
    expect(source.close).toHaveBeenCalledOnce()
  })

  it('discards initial stale response and coalesces duplicate invalidations into a fresh snapshot', async () => {
    const initial = deferred<() => void>()
    const publishOld = vi.fn(), publishNew = vi.fn(), failed = vi.fn()
    const read = vi.fn().mockReturnValueOnce(initial.promise).mockResolvedValue(() => publishNew())
    const sync = createResync(read, failed)
    const done = sync.refresh()
    void sync.refresh(); void sync.refresh()
    initial.resolve(publishOld)
    await done
    expect(publishOld).not.toHaveBeenCalled()
    expect(read).toHaveBeenCalledTimes(2)
    expect(publishNew).toHaveBeenCalledOnce()
    sync.dispose()
    await sync.refresh()
    expect(read).toHaveBeenCalledTimes(2)
  })

  it('restarts when an invalidation arrives between drain completion and promise cleanup', async () => {
    const published = vi.fn()
    const read = vi.fn().mockResolvedValueOnce(() => queueMicrotask(() => { void sync.refresh() })).mockResolvedValue(published)
    const sync = createResync(read, vi.fn())
    await sync.refresh()
    expect(read).toHaveBeenCalledTimes(2)
    expect(published).toHaveBeenCalledOnce()
    sync.dispose()
  })

  it('aborts pending reads and suppresses their publication after disposal', async () => {
    const pending = deferred<() => void>(), publish = vi.fn()
    let signal: AbortSignal | undefined
    const sync = createResync(async token => { signal = token; return pending.promise }, vi.fn())
    const done = sync.refresh()
    sync.dispose()
    expect(signal?.aborted).toBe(true)
    pending.resolve(publish)
    await done
    expect(publish).not.toHaveBeenCalled()
  })

  it('uses each route ID for gate, lot commands and receipts in both garages', async () => {
    const api = stub()
    api.exitGarage.mockImplementation(async (id: number) => ({ licenseNumber: 'CAR', startAt: '2026-10-14T10:00:00Z', endAt: '2026-10-14T10:01:00Z', duration: '00:01:00', cost: id, currency: 'EUR', effectiveOccurredAt: '2026-10-14T10:01:00Z' }))
    const wrapper = mount(GarageSimulation, { props: { garageId: 1 }, global: { provide: { [garageApiKey]: api } } })
    for (const id of [1, 2]) {
      await wrapper.setProps({ garageId: id }); await flushPromises()
      await wrapper.get('nav button:first-child').trigger('click')
      await wrapper.get('#license-number').setValue('CAR')
      await wrapper.get('.gate-form').trigger('submit'); await flushPromises()
      expect(api.enterGarage).toHaveBeenLastCalledWith(id, 'CAR', null)
      await wrapper.get('nav button:nth-child(2)').trigger('click')
      await wrapper.get('.lot-card button').trigger('click')
      await wrapper.get('.lot-entry-form').trigger('submit'); await flushPromises()
      expect(api.enterLot).toHaveBeenLastCalledWith(id, id * 10, null, null)
      await wrapper.get('nav button:first-child').trigger('click')
      await wrapper.get('.segmented button:nth-child(2)').trigger('click')
      await wrapper.get('#license-number').setValue('CAR')
      await wrapper.get('.gate-form').trigger('submit'); await flushPromises()
      expect(api.exitGarage).toHaveBeenLastCalledWith(id, 'CAR', null)
      expect(wrapper.get('.receipt-total').text()).toContain(String(id))
      Source.instances[0]!.emit(id); await flushPromises()
      expect(wrapper.find('.receipt-panel').exists()).toBe(true)
    }
    wrapper.unmount()
  })

  it('keeps active input, lot selection and last snapshot through SSE error and reconnect', async () => {
    const api = stub()
    const wrapper = mount(GarageSimulation, { props: { garageId: 1 }, global: { provide: { [garageApiKey]: api } } })
    await flushPromises()
    const source = Source.instances[0]!
    source.onopen?.(); await flushPromises()
    await wrapper.get('#license-number').setValue('DRAFT-123')
    await wrapper.get('#event-time').setValue('2026-10-14T13:30:45')
    source.emit(); await flushPromises()
    expect((wrapper.get('#license-number').element as HTMLInputElement).value).toBe('DRAFT-123')
    expect((wrapper.get('#event-time').element as HTMLInputElement).value).toBe('2026-10-14T13:30:45.000')
    await wrapper.get('nav button:nth-child(2)').trigger('click')
    await wrapper.get('.layout-cell').trigger('click')
    source.emit(); await flushPromises()
    expect(wrapper.text()).toContain('valittu')
    source.onerror?.(); await flushPromises()
    expect(wrapper.text()).toContain('Yhteys katkennut')
    expect(wrapper.findAll('.lot-card')).toHaveLength(1)
    api.getLayout.mockRejectedValueOnce(new Error('offline'))
    source.onopen?.(); await flushPromises()
    expect(wrapper.text()).toContain('Tilanne voi olla vanhentunut')
    expect(wrapper.findAll('.lot-card')).toHaveLength(1)
    await wrapper.get('.refresh-button').trigger('click'); await flushPromises()
    expect(wrapper.text()).toContain('Reaaliaikainen')
    expect((wrapper.get('#event-time').element as HTMLInputElement).value).toBe('2026-10-14T13:30:45.000')
    wrapper.unmount()
  })

  it('never transfers a late receipt or old read into another garage', async () => {
    const api = stub(), exit = deferred<ReceiptDto>()
    api.exitGarage.mockReturnValueOnce(exit.promise)
    const wrapper = mount(GarageSimulation, { props: { garageId: 1 }, global: { provide: { [garageApiKey]: api } } })
    await flushPromises()
    await wrapper.get('#license-number').setValue('OLD')
    await wrapper.get('.segmented button:nth-child(2)').trigger('click')
    await wrapper.get('.gate-form').trigger('submit')
    await wrapper.setProps({ garageId: 2 })
    exit.resolve({ licenseNumber: 'OLD' } as ReceiptDto)
    await flushPromises()
    expect(wrapper.text()).toContain('Halli 2')
    expect(wrapper.find('.receipt-panel').exists()).toBe(false)
    expect(api.getLayout).toHaveBeenCalledWith(2, expect.any(AbortSignal))
    wrapper.unmount()
  })

  it('invalid and missing garage IDs never default to the first garage or send commands', async () => {
    const api = stub()
    const wrapper = mount(GarageSimulation, { props: { garageId: null }, global: { provide: { [garageApiKey]: api } } })
    await flushPromises()
    expect(api.getLayout).not.toHaveBeenCalled()
    await wrapper.setProps({ garageId: 99 }); await flushPromises()
    expect(wrapper.text()).toContain('404')
    expect(wrapper.find('.gate-form').exists()).toBe(false)
    wrapper.unmount()
  })

  it('preserves editor draft and expected version while updating only live metadata', async () => {
    const api = stub()
    let editor!: ReturnType<typeof useLayoutDraft>
    const wrapper = mount(defineComponent({ setup() { editor = useLayoutDraft(api, 1); return () => h('p', editor.notice.value) } }))
    await flushPromises()
    editor.resize(2, 1); editor.paint(1)
    api.getLayout.mockResolvedValue({ ...layout(1, 'Occupied'), version: 2 })
    Source.instances[0]!.emit(1, 'layout'); await flushPromises()
    expect(editor.width.value).toBe(2)
    expect(editor.original.value?.version).toBe(1)
    expect(editor.original.value?.lots[0]?.currentStatus).toBe('Occupied')
    expect(editor.createPayload().expectedVersion).toBe(1)
    expect(wrapper.text()).toContain('muutettu muualla')
    expect(editor.dirty.value).toBe(true)
    wrapper.unmount()
  })

  it('list stays usable without coordinates or map service and displays 0/0 and Unknown separately', async () => {
    const api = stub()
    const data = await api.getGarages()
    api.getGarages.mockResolvedValue([{ ...data[0]!, capacity: 5, occupiedLots: 3, unknownLots: 1 }, { ...data[1]!, capacity: 0, freeLots: 0 }])
    const wrapper = mount(IndexPage, { global: { provide: { [garageApiKey]: api }, stubs: { GarageMap: { template: '<p>Kartan virhe</p>' } } } })
    await flushPromises()
    expect(wrapper.text()).toContain('3/5 varattu')
    expect(wrapper.text()).toContain('1 tuntematon')
    expect(wrapper.text()).toContain('0/0 varattu')
    expect(wrapper.get('a[href="/garages/2"]').text()).toBe('Halli 2')
    expect(wrapper.text()).toContain('Sijainti puuttuu')
    wrapper.unmount()
  })

  it('route changes during initial read discard the old batch and resync the current ID', async () => {
    const api = stub(), old = deferred<GarageLayoutDto>(), id = ref<number | null>(1)
    api.getLayout.mockReturnValueOnce(old.promise)
    let dashboard!: ReturnType<typeof useGarageDashboard>
    const wrapper = mount(defineComponent({ setup() { dashboard = useGarageDashboard(api as GarageApi, id); return () => h('p', dashboard.selectedGarage.value?.name) } }))
    id.value = 2; await flushPromises()
    old.resolve(layout(1)); await flushPromises()
    expect(dashboard.layout.value?.garageId).toBe(2)
    expect(wrapper.text()).toBe('Halli 2')
    wrapper.unmount()
  })
  it('keeps image preview and drafts during SSE, freezes time and shares the command lock', async () => {
    const api = stub(), pending = deferred<unknown>()
    api.enterGarageFromImage.mockReturnValueOnce(pending.promise)
    const revoke = vi.fn()
    vi.stubGlobal('URL', { createObjectURL: vi.fn(() => 'blob:preview'), revokeObjectURL: revoke })
    const wrapper = mount(GarageSimulation, { props: { garageId: 1 }, global: { provide: { [garageApiKey]: api } } })
    await flushPromises()
    const image = new File(['image'], 'car.png', { type: 'image/png' })
    Object.defineProperty(wrapper.get('#plate-image').element, 'files', { configurable: true, value: [image] })
    await wrapper.get('#plate-image').trigger('change')
    await wrapper.get('#license-number').setValue('DRAFT')
    await wrapper.get('#event-time').setValue('2026-10-14T13:00:00')
    Source.instances[0]!.emit(); await flushPromises()
    expect(wrapper.get('.image-preview').attributes('src')).toBe('blob:preview')
    expect((wrapper.get('#license-number').element as HTMLInputElement).value).toBe('DRAFT')
    expect(api.enterGarageFromImage).not.toHaveBeenCalled()
    await wrapper.get('.image-entry-panel button').trigger('click')
    const time = api.enterGarageFromImage.mock.calls[0]![2]
    expect(new Date(time).getTime()).toBe(new Date('2026-10-14T13:00:00').getTime())
    expect((wrapper.get('#event-time').element as HTMLInputElement).disabled).toBe(true)
    expect((wrapper.get('#plate-image').element as HTMLInputElement).disabled).toBe(true)
    await wrapper.get('.gate-form').trigger('submit')
    expect(api.enterGarage).not.toHaveBeenCalled()
    pending.resolve({ visitId: 44, licenseNumber: 'ABC', recognizedLicenseNumber: 'ABC', outcome: 'Duplicate', startAt: time, effectiveOccurredAt: time, gateOpened: true })
    await flushPromises()
    expect(wrapper.text()).toContain('Tunnistettu rekisteri: ABC')
    expect(wrapper.text()).toContain('VisitId: 44')
    expect(wrapper.text()).toContain('Alkuperäinen alkamisaika')
    expect(wrapper.find('.image-preview').exists()).toBe(true)
    wrapper.unmount()
    expect(revoke).toHaveBeenCalledWith('blob:preview')
  })

  it('discards old garage image results and offers retry/manual input on provider failure', async () => {
    const api = stub(), pending = deferred<unknown>()
    api.enterGarageFromImage.mockReturnValueOnce(pending.promise)
    vi.stubGlobal('URL', { createObjectURL: vi.fn(() => 'blob:preview'), revokeObjectURL: vi.fn() })
    const wrapper = mount(GarageSimulation, { props: { garageId: 1 }, global: { provide: { [garageApiKey]: api } } })
    await flushPromises()
    async function chooseAndSubmit() {
      Object.defineProperty(wrapper.get('#plate-image').element, 'files', { configurable: true, value: [new File(['image'], 'car.png', { type: 'image/png' })] })
      await wrapper.get('#plate-image').trigger('change')
      await wrapper.get('.image-entry-panel button').trigger('click')
    }
    await chooseAndSubmit()
    await wrapper.setProps({ garageId: 2 }); await flushPromises()
    pending.resolve({ visitId: 1, licenseNumber: 'OLD', recognizedLicenseNumber: 'OLD', startAt: '2026-10-14T10:00:00Z', effectiveOccurredAt: '2026-10-14T10:00:00Z', outcome: 'Entry', gateOpened: true })
    await flushPromises()
    expect(wrapper.text()).not.toContain('Tunnistettu rekisteri: OLD')
    expect(wrapper.find('.image-preview').exists()).toBe(false)
    api.enterGarageFromImage.mockRejectedValueOnce(new Error('provider error'))
    await chooseAndSubmit(); await flushPromises()
    expect(wrapper.text()).toContain('käsinsyöttöä')
    expect(wrapper.find('.image-preview').exists()).toBe(true)
    expect((wrapper.get('.image-entry-panel button').element as HTMLButtonElement).disabled).toBe(false)
    wrapper.unmount()
  })})
