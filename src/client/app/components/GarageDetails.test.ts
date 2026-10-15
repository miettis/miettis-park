import { mount, flushPromises } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import GaragePricing from './GaragePricing.vue'
import GarageVisitLog from './GarageVisitLog.vue'
import { garageApiKey } from '../services/garageApi'
import { garageEvents } from '../services/garageEvents'

afterEach(() => { garageEvents.dispose(); vi.unstubAllGlobals() })
describe('garage details', () => {
  it('edits prices with their original concurrency baseline and preserves input after rejection', async () => {
    const original = [{ amount: 0.5, startDuration: '00:00:00', endDuration: null, perUnit: '00:10:00' }]
    const api = {
      getPricing: vi.fn(async () => ({ garageId: 1, tiers: original })),
      savePricing: vi.fn().mockRejectedValueOnce(new Error('Päätä aktiiviset hallikäynnit')).mockImplementationOnce(async (_id, tiers) => ({ garageId: 1, tiers }))
    }
    const wrapper = mount(GaragePricing, { props: { garageId: 1 }, global: { provide: { [garageApiKey]: api } } })
    await flushPromises()
    await wrapper.get('input[type=number]').setValue('0.75')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(wrapper.get('[role=alert]').text()).toContain('aktiiviset hallikäynnit')
    expect((wrapper.get('input[type=number]').element as HTMLInputElement).value).toBe('0.75')
    expect(api.savePricing).toHaveBeenCalledWith(1, [{ ...original[0], amount: 0.75 }], original)
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(wrapper.get('[role=status]').text()).toBe('Hinnasto tallennettiin.')
    wrapper.unmount()
  })

  it('shows completed, interrupted and active visits and paginates by garage', async () => {
    const visits = Array.from({ length: 50 }, (_, index) => ({ visitId: index + 1, licenseNumber: `CAR-${index}`, startAt: '2026-10-14T10:00:00Z', endAt: index === 0 ? '2026-10-14T11:00:00Z' : null, closedAt: index === 1 ? '2026-10-14T11:00:00Z' : null, status: index === 0 ? 'Completed' : index === 1 ? 'Interrupted' : 'Active', closureReason: null, cost: index === 0 ? 3 : null }))
    const api = { getVisitHistory: vi.fn().mockResolvedValueOnce(visits).mockResolvedValueOnce([]) }
    const wrapper = mount(GarageVisitLog, { props: { garageId: 7 }, global: { provide: { [garageApiKey]: api } } })
    await flushPromises()
    expect(api.getVisitHistory).toHaveBeenCalledWith(7, 0, expect.any(AbortSignal))
    expect(wrapper.text()).toContain('Päättynyt')
    expect(wrapper.text()).toContain('Keskeytynyt')
    expect(wrapper.text()).toContain('Aktiivinen')
    expect(wrapper.text()).toContain('3,00')
    await wrapper.get('.pagination button:last-child').trigger('click')
    await flushPromises()
    expect(api.getVisitHistory).toHaveBeenLastCalledWith(7, 50, expect.any(AbortSignal))
    expect(wrapper.text()).toContain('Ei vierailuja tällä sivulla.')
    wrapper.unmount()
  })
})


function pending<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>(done => { resolve = done })
  return { promise, resolve }
}
const baseTiers = () => [
  { amount: 0.5, startDuration: '00:00:00', endDuration: '03:00:00', perUnit: '00:10:00' },
  { amount: 0.3, startDuration: '03:00:00', endDuration: null, perUnit: '00:10:00' }
]

it('adds increasing price boundaries and removes middle or first tiers without gaps', async () => {
  const api = { getPricing: vi.fn(async () => ({ garageId: 1, tiers: baseTiers() })), savePricing: vi.fn(async (_id, tiers) => ({ garageId: 1, tiers })) }
  const wrapper = mount(GaragePricing, { props: { garageId: 1 }, global: { provide: { [garageApiKey]: api } } })
  await flushPromises()
  await wrapper.get('.actions button').trigger('click')
  await wrapper.get('form').trigger('submit')
  await flushPromises()
  const saved = api.savePricing.mock.calls[0]![1]
  expect(saved.map((tier: { startDuration: string, endDuration: string | null }) => [tier.startDuration, tier.endDuration])).toEqual([
    ['00:00:00', '03:00:00'], ['03:00:00', '06:00:00'], ['06:00:00', null]
  ])
  await wrapper.findAll('.tier')[1]!.get('button').trigger('click')
  await wrapper.get('form').trigger('submit')
  await flushPromises()
  expect(api.savePricing.mock.lastCall![1][1].startDuration).toBe('03:00:00')
  await wrapper.findAll('.tier')[0]!.get('button').trigger('click')
  await wrapper.get('form').trigger('submit')
  await flushPromises()
  expect(api.savePricing.mock.lastCall![1][0]).toMatchObject({ startDuration: '00:00:00', endDuration: null })
  expect(wrapper.get('.tier button').attributes('disabled')).toBeDefined()
  wrapper.unmount()
})

it('recovers a failed pricing load and prevents duplicate submissions while saving', async () => {
  const saving = pending<{ garageId: number, tiers: ReturnType<typeof baseTiers> }>()
  const api = { getPricing: vi.fn().mockRejectedValueOnce(new Error('Offline')).mockResolvedValueOnce({ garageId: 1, tiers: baseTiers() }), savePricing: vi.fn(() => saving.promise) }
  const wrapper = mount(GaragePricing, { props: { garageId: 1 }, global: { provide: { [garageApiKey]: api } } })
  await flushPromises()
  expect(wrapper.get('[role=alert]').text()).toBe('Offline')
  expect(wrapper.find('form').exists()).toBe(false)
  await wrapper.get('button').trigger('click')
  await flushPromises()
  await wrapper.get('form').trigger('submit')
  await wrapper.get('form').trigger('submit')
  expect(api.savePricing).toHaveBeenCalledTimes(1)
  expect(wrapper.get('fieldset').attributes('disabled')).toBeDefined()
  saving.resolve({ garageId: 1, tiers: baseTiers() })
  await flushPromises()
  expect(wrapper.get('fieldset').attributes('disabled')).toBeUndefined()
  wrapper.unmount()
})

it('ignores a superseded history response and aborts outstanding work on unmount', async () => {
  const source = { onopen: null as (() => void) | null, onerror: null, addEventListener: vi.fn(), close: vi.fn() }
  vi.stubGlobal('EventSource', vi.fn(function EventSource() { return source }))
  const first = pending<unknown[]>(), second = pending<unknown[]>(), third = pending<unknown[]>()
  const api = { getVisitHistory: vi.fn().mockReturnValueOnce(first.promise).mockReturnValueOnce(second.promise).mockReturnValueOnce(third.promise) }
  const wrapper = mount(GarageVisitLog, { props: { garageId: 1 }, global: { provide: { [garageApiKey]: api } } })
  const oldSignal = api.getVisitHistory.mock.calls[0]![2] as AbortSignal
  const vm = wrapper.vm as unknown as { load: () => Promise<void> }
  source.onopen?.()
  expect(oldSignal.aborted).toBe(true)
  second.resolve([])
  await flushPromises()
  first.resolve([{ visitId: 1, licenseNumber: 'STALE', startAt: '2026-10-14T10:00:00Z', status: 'Active', endAt: null, closedAt: null, cost: null }])
  await flushPromises()
  expect(wrapper.text()).not.toContain('STALE')
  expect(wrapper.text()).toContain('Ei vierailuja')
  source.onopen?.()
  const signal = api.getVisitHistory.mock.calls[2]![2] as AbortSignal
  wrapper.unmount()
  expect(signal.aborted).toBe(true)
  third.resolve([])
  await flushPromises()
})

it('retries a failed history request without advancing its page', async () => {
  const api = { getVisitHistory: vi.fn().mockRejectedValueOnce(new Error('Loki offline')).mockResolvedValueOnce([]) }
  const wrapper = mount(GarageVisitLog, { props: { garageId: 8 }, global: { provide: { [garageApiKey]: api } } })
  await flushPromises()
  expect(wrapper.get('[role=alert]').text()).toBe('Loki offline')
  await wrapper.get('.panel-heading button').trigger('click')
  await flushPromises()
  expect(api.getVisitHistory).toHaveBeenLastCalledWith(8, 0, expect.any(AbortSignal))
  expect(wrapper.find('[role=alert]').exists()).toBe(false)
  wrapper.unmount()
})
