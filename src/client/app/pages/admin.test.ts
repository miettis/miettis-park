import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import AdminPage from './admin.vue'
import IndexPage from './index.vue'
import { elapsedLabel } from '../composables/useAdminDashboard'
import { garageApiKey, HttpGarageApi, type GarageApi } from '../services/garageApi'
import type { ActiveParkingDto, ActiveVisitDto, AvailabilityDto } from '../types/garage'

function stub() {
  return {
    getGarages: vi.fn(async () => [{ id: 1, name: 'Testihalli' }]),
    getAvailability: vi.fn(async (): Promise<AvailabilityDto> => ({ capacity: 9, freeLots: 0, occupiedLots: 7, unknownLots: 2, activeVisits: 12, isFull: true, lots: [] })),
    getActiveParkings: vi.fn(async (): Promise<ActiveParkingDto[]> => [
      { parkingId: 1, lotId: 1, lotNumber: 1, visitId: 10, licenseNumber: 'HIST-123', startAt: '2026-10-14T10:15:00Z' },
      { parkingId: 2, lotId: 2, lotNumber: 2, visitId: null, licenseNumber: null, startAt: '2026-10-14T11:59:30Z' }
    ]),
    getActiveVisits: vi.fn(async (): Promise<ActiveVisitDto[]> => [
      { visitId: 11, licenseNumber: 'HIST-123', startAt: '2026-10-14T11:55:00Z', lotId: null, lotNumber: null },
      { visitId: 12, licenseNumber: 'LINK-123', startAt: '2026-10-14T11:00:00Z', lotId: 3, lotNumber: 3 }
    ])
  }
}

function render(api: Pick<GarageApi, 'getGarages' | 'getAvailability' | 'getActiveVisits' | 'getActiveParkings'> = stub()) {
  return mount(AdminPage, { global: { provide: { [garageApiKey]: api } } })
}

function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((done) => { resolve = done })
  return { promise, resolve }
}

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date'] })
  vi.setSystemTime(new Date('2026-10-14T12:00:00Z'))
})
afterEach(() => {
  vi.useRealTimers()
  vi.unstubAllGlobals()
})

describe('read-only admin dashboard', () => {
  it('uses API counts and independent parking/visit rows including historical and unknown links', async () => {
    const wrapper = render()
    await flushPromises()
    expect(wrapper.text()).toContain('Testihalli')
    expect(wrapper.findAll('.stat-card strong').map(item => item.text())).toEqual(['0 / 9', '7', '2', '12'])
    expect(wrapper.text()).toContain('Halli on täynnä')
    const parkings = wrapper.findAll('table')[0]!
    const visits = wrapper.findAll('table')[1]!
    expect(parkings.findAll('tbody tr')).toHaveLength(2)
    expect(parkings.findAll('tbody tr')[0]!.text()).toContain('HIST-123')
    expect(parkings.findAll('tbody tr')[0]!.text()).toContain('1 h 45 min')
    expect(parkings.findAll('tbody tr')[1]!.text()).toContain('Ei tiedossa')
    expect(parkings.findAll('tbody tr')[1]!.text()).toContain('Alle minuutti')
    expect(parkings.get('time').attributes('datetime')).toBe('2026-10-14T10:15:00Z')
    expect(visits.findAll('tbody tr')[0]!.text()).toContain('Paikka ei tiedossa')
    expect(visits.findAll('tbody tr')[1]!.text()).toContain('Paikka 3')
    expect(visits.get('time').attributes('datetime')).toBe('2026-10-14T11:55:00Z')
    expect(wrapper.text()).toContain('ei laskutusperuste')
    expect(wrapper.get('[role="status"] time').attributes('datetime')).toBe('2026-10-14T12:00:00.000Z')
    vi.setSystemTime(new Date('2026-10-14T15:00:00Z'))
    expect(parkings.text()).toContain('1 h 45 min')
  })

  it('clamps future timestamps and labels malformed timestamps as unknown without invalid time elements', async () => {
    const api = stub()
    api.getActiveParkings.mockResolvedValue([
      { parkingId: 1, lotId: 1, lotNumber: 1, visitId: null, licenseNumber: null, startAt: '2026-10-15T12:00:00Z' },
      { parkingId: 2, lotId: 2, lotNumber: 2, visitId: null, licenseNumber: null, startAt: 'invalid' }
    ])
    const wrapper = render(api)
    await flushPromises()
    const rows = wrapper.findAll('table tbody tr')
    expect(rows[0]!.text()).toContain('Alle minuutti')
    expect(rows[1]!.findAll('td').slice(1).map(cell => cell.text())).toEqual(['Ei tiedossa', 'Ei tiedossa'])
    expect(rows[1]!.find('time').exists()).toBe(false)
    expect(elapsedLabel('2026-10-14T11:50:00Z', new Date())).toBe('10 min')
    expect(elapsedLabel('invalid', null)).toBe('Ei tiedossa')
  })

  it('shows initial loading and separately empty parking and visit states', async () => {
    const api = stub()
    const garages = deferred<{ id: number, name: string }[]>()
    api.getGarages.mockReturnValueOnce(garages.promise)
    api.getActiveParkings.mockResolvedValue([])
    api.getActiveVisits.mockResolvedValue([])
    const wrapper = render(api)
    await flushPromises()
    expect(wrapper.get('[role="status"]').text()).toContain('Haetaan')
    expect(wrapper.get('.refresh-button').attributes('disabled')).toBeDefined()
    garages.resolve([{ id: 1, name: 'Testihalli' }])
    await flushPromises()
    expect(wrapper.text()).toContain('Aktiivisia pysäköintejä ei ole.')
    expect(wrapper.text()).toContain('Aktiivisia hallikäyntejä ei ole.')
    expect(wrapper.find('table').exists()).toBe(false)
  })

  it('shows an empty garage list without querying situation endpoints', async () => {
    const api = stub()
    api.getGarages.mockResolvedValue([])
    const wrapper = render(api)
    await flushPromises()
    expect(wrapper.get('[role="status"]').text()).toBe('Halleja ei löytynyt.')
    expect(api.getAvailability).not.toHaveBeenCalled()
    await wrapper.get('.refresh-button').trigger('click')
    await flushPromises()
    expect(api.getGarages).toHaveBeenCalledTimes(2)
  })

  it.each(['getGarages', 'getAvailability', 'getActiveVisits', 'getActiveParkings'] as const)('recovers from an initial %s error', async (method) => {
    const api = stub()
    api[method].mockRejectedValueOnce(new Error('Yhteys katkesi.'))
    const wrapper = render(api)
    await flushPromises()
    expect(wrapper.get('[role="alert"]').text()).toContain('Yhteys katkesi.')
    expect(wrapper.find('.stats').exists()).toBe(false)
    await wrapper.get('.state-error button').trigger('click')
    await flushPromises()
    expect(wrapper.find('[role="alert"]').exists()).toBe(false)
    expect(wrapper.findAll('table')).toHaveLength(2)
  })

  it('refreshes all resources once, hides stale data, and waits for every response before publication', async () => {
    const api = stub()
    const wrapper = render(api)
    await flushPromises()
    const visits = deferred<ActiveVisitDto[]>()
    api.getActiveVisits.mockReturnValueOnce(visits.promise)
    api.getActiveParkings.mockResolvedValueOnce([])
    api.getAvailability.mockResolvedValueOnce({ capacity: 9, freeLots: 9, occupiedLots: 0, unknownLots: 0, activeVisits: 0, isFull: false, lots: [] })
    vi.setSystemTime(new Date('2026-10-14T13:00:00Z'))
    await wrapper.get('.refresh-button').trigger('click')
    await wrapper.get('.refresh-button').trigger('click')
    await flushPromises()
    expect(wrapper.get('[role="status"]').text()).toContain('Edellinen tilanne ei ole ajantasainen')
    expect(wrapper.find('.stats').exists()).toBe(false)
    for (const method of [api.getAvailability, api.getActiveVisits, api.getActiveParkings]) {
      expect(method).toHaveBeenCalledTimes(2)
      expect(method).toHaveBeenLastCalledWith(1)
    }
    visits.resolve([])
    await flushPromises()
    expect(wrapper.get('.stat-primary strong').text()).toBe('9 / 9')
    expect(wrapper.get('[role="status"] time').attributes('datetime')).toBe('2026-10-14T13:00:00.000Z')
  })

  it.each(['getAvailability', 'getActiveVisits', 'getActiveParkings'] as const)('hides old data after a refresh %s failure and retries all resources', async (method) => {
    const api = stub()
    const wrapper = render(api)
    await flushPromises()
    api[method].mockRejectedValueOnce(new Error('Päivitys epäonnistui.'))
    await wrapper.get('.refresh-button').trigger('click')
    await flushPromises()
    expect(wrapper.get('[role="alert"]').text()).toContain('ei voida näyttää ajantasaisena')
    expect(wrapper.find('table').exists()).toBe(false)
    expect(wrapper.find('.stats').exists()).toBe(false)
    await wrapper.get('.state-error button').trigger('click')
    await flushPromises()
    for (const read of [api.getAvailability, api.getActiveVisits, api.getActiveParkings]) {
      expect(read).toHaveBeenCalledTimes(3)
    }
    expect(wrapper.findAll('table')).toHaveLength(2)
  })

  it('keeps refresh guarded until outstanding reads finish even when another read fails', async () => {
    const api = stub()
    const wrapper = render(api)
    await flushPromises()
    const visits = deferred<ActiveVisitDto[]>()
    api.getActiveVisits.mockReturnValueOnce(visits.promise)
    api.getAvailability.mockRejectedValueOnce(new Error('Hakuvika'))
    await wrapper.get('.refresh-button').trigger('click')
    await flushPromises()
    expect(wrapper.get('.refresh-button').attributes('disabled')).toBeDefined()
    await wrapper.get('.refresh-button').trigger('click')
    expect(api.getAvailability).toHaveBeenCalledTimes(2)
    visits.resolve([])
    await flushPromises()
    expect(wrapper.get('[role="alert"]').text()).toContain('Hakuvika')
    expect(wrapper.find('.stats').exists()).toBe(false)
  })
  it('provides semantic route links, captions, column headings, and focusable scroll regions', async () => {
    const wrapper = render()
    await flushPromises()
    expect(wrapper.get('nav a[href="/"]').text()).toContain('Porttisimulaatio')
    expect(wrapper.get('nav a[href="/admin"]').attributes('aria-current')).toBe('page')
    expect(wrapper.find('a[href="#main-content"]').exists()).toBe(true)
    for (const region of wrapper.findAll('.admin-table-scroll')) {
      expect(region.attributes('tabindex')).toBe('0')
      expect(region.attributes('aria-label')).toContain('vieritettävä')
      expect(region.get('caption').text()).not.toBe('')
      expect(region.findAll('thead th').every(th => th.attributes('scope') === 'col')).toBe(true)
    }
    const index = mount(IndexPage, { global: { provide: { [garageApiKey]: stub() } } })
    await flushPromises()
    expect(index.get('nav a[href="/admin"]').text()).toContain('Hallinta')
  })

  it('uses only the four existing relative GET paths with the real HTTP adapter', async () => {
    const api = stub()
    const bodies = [await api.getGarages(), await api.getAvailability(), await api.getActiveVisits(), await api.getActiveParkings()]
    const fetchStub = vi.fn(async (_path: string, _init: RequestInit) => ({ ok: true, status: 200, json: async () => bodies.shift() }))
    vi.stubGlobal('fetch', fetchStub)
    const wrapper = render(new HttpGarageApi())
    await flushPromises()
    expect(wrapper.findAll('table')).toHaveLength(2)
    expect(fetchStub.mock.calls.map(call => call[0])).toEqual(['/api/garages', '/api/garages/1/availability', '/api/garages/1/visits/active', '/api/garages/1/parkings/active'])
    expect(fetchStub.mock.calls.every(call => !(call[1] as RequestInit).method && !(call[1] as RequestInit).body)).toBe(true)
  })
})
