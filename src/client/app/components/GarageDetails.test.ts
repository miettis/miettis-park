import { mount, flushPromises } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import GaragePricing from './GaragePricing.vue'
import GarageVisitLog from './GarageVisitLog.vue'
import { garageApiKey } from '../services/garageApi'
import { garageEvents } from '../services/garageEvents'

afterEach(() => garageEvents.dispose())
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
