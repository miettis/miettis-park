import { mount, flushPromises } from '@vue/test-utils'
import { defineComponent, h, provide } from 'vue'
import { describe, expect, it, vi } from 'vitest'
import IndexPage from '../components/GarageSimulation.vue'
import LotCard from '../components/LotCard.vue'
import EventTimeInput from '../components/EventTimeInput.vue'
import { garageApiKey, type GarageApi } from '../services/garageApi'
import type { ActiveParkingDto, ActiveVisitDto, AvailabilityDto, EntryDto, GarageDto, ImageEntryDto, LotDto, ReceiptDto } from '../types/garage'
import { toEventTime } from '../utils/eventTime'

const garages: GarageDto[] = [{ id: 1, name: 'Keskustan pysäköinti', address: null, latitude: null, longitude: null, capacity: 0, freeLots: 0, occupiedLots: 0, unknownLots: 0, isFull: false }]
const initialLots: LotDto[] = [
  { id: 11, number: 1, currentStatus: 'Free' },
  { id: 12, number: 2, currentStatus: 'Occupied' },
  { id: 13, number: 3, currentStatus: 'Unknown' },
  { id: 14, number: 4, currentStatus: 'Free' },
  { id: 15, number: 5, currentStatus: 'Free' }
]

class StubGarageApi implements GarageApi {
  lots = initialLots.map(lot => ({ ...lot }))
  visits: ActiveVisitDto[] = []
  parkings: ActiveParkingDto[] = []
  failNextLotEntry = false
  failNextGarageExit = false
  entryOutcome: EntryDto['outcome'] = 'Entry'
  getLayout = vi.fn(async () => ({ garageId: 1, width: this.lots.length || 1, height: 1, version: 1, cells: this.lots.map((lot, x) => ({ x, y: 0, type: 'ParkingLot' as const, lotId: lot.id })), lots: this.lots.map(lot => ({ ...lot, canRemove: lot.currentStatus === 'Free' })) }))
  getGarages = vi.fn(async () => garages)
  getAvailability = vi.fn(async (): Promise<AvailabilityDto> => this.availability())
  getActiveVisits = vi.fn(async () => this.visits.map(visit => ({ ...visit })))
  getActiveParkings = vi.fn(async () => this.parkings.map(parking => ({ ...parking })))
  enterGarage = vi.fn(async (_garageId: number, licenseNumber: string): Promise<EntryDto> => {
    const normalized = licenseNumber.toUpperCase()
    const visitId = this.entryOutcome === 'RecoveredMissingExit' ? 32 : 31
    const startAt = this.entryOutcome === 'Duplicate' ? '2026-10-14T08:30:00Z' : '2026-10-14T12:00:00Z'
    if (this.entryOutcome !== 'Duplicate') {
      this.visits = this.visits.filter(visit => visit.licenseNumber !== normalized)
      this.visits.push({ visitId, licenseNumber: normalized, startAt, lotId: null, lotNumber: null })
    }
    return { visitId, licenseNumber: normalized, startAt, outcome: this.entryOutcome, gateOpened: true, effectiveOccurredAt: '2026-10-14T10:00:00Z' }
  })
  enterGarageFromImage = vi.fn(async (_garageId: number, _image: File, _occurredAt: string | null): Promise<ImageEntryDto> => ({ visitId: 31, licenseNumber: 'ABC123', startAt: '2026-10-14T12:00:00Z', outcome: 'Entry', effectiveOccurredAt: '2026-10-14T10:00:00Z', gateOpened: true, recognizedLicenseNumber: 'ABC123' }))
  exitGarage = vi.fn(async (_garageId: number, licenseNumber: string): Promise<ReceiptDto> => {
    if (this.failNextGarageExit) {
      this.failNextGarageExit = false
      throw Object.assign(new Error('Aktiivista käyntiä ei löytynyt.'), { status: 409, problem: { code: 'ACTIVE_VISIT_NOT_FOUND', gateOpened: false, requiresCustomerService: true } })
    }
    const receipt = {
      visitId: 31,
      licenseNumber: licenseNumber.toUpperCase(),
      startAt: '2026-10-14T10:00:00Z',
      endAt: '2026-10-14T11:15:00Z',
      closedAt: '2026-10-14T11:15:00Z',
      duration: '01:15:00',
      cost: 2.5,
      currency: 'EUR',
      gateOpened: true,
      effectiveOccurredAt: '2026-10-14T11:15:00Z'
    }
    this.visits = this.visits.filter(visit => visit.licenseNumber !== licenseNumber.toUpperCase())
    return receipt
  })
  enterLot = vi.fn(async (_garageId: number, lotId: number, visitId: number | null) => {
    if (this.failNextLotEntry) {
      this.failNextLotEntry = false
      this.lots.find(lot => lot.id === lotId)!.currentStatus = 'Occupied'
      throw Object.assign(new Error('Paikka ei ole enää vapaa.'), { status: 409, problem: { code: 'LOT_UNAVAILABLE' } })
    }
    this.lots.find(lot => lot.id === lotId)!.currentStatus = 'Occupied'
    const visit = this.visits.find(item => item.visitId === visitId)
    this.parkings.push({ parkingId: this.parkings.length + 1, lotId, lotNumber: this.lots.find(lot => lot.id === lotId)!.number, visitId, licenseNumber: visit?.licenseNumber ?? null, startAt: '2026-10-14T10:10:00Z' })
    return { parkingId: this.parkings.length, lotId, visitId, startAt: '2026-10-14T10:10:00Z', effectiveOccurredAt: '2026-10-14T10:10:00Z' }
  })
  exitLot = vi.fn(async (_garageId: number, lotId: number) => {
    this.lots.find(lot => lot.id === lotId)!.currentStatus = 'Free'
    this.parkings = this.parkings.filter(parking => parking.lotId !== lotId)
    return { lotId, parkingId: 1, missingParkingObservation: false, effectiveOccurredAt: '2026-10-14T10:00:00Z' }
  })

  private availability(): AvailabilityDto {
    const freeLots = this.lots.filter(lot => lot.currentStatus === 'Free').length
    const occupiedLots = this.lots.filter(lot => lot.currentStatus === 'Occupied').length
    const unknownLots = this.lots.filter(lot => lot.currentStatus === 'Unknown').length
    return { capacity: this.lots.length, activeVisits: this.visits.length, freeLots, occupiedLots, unknownLots, isFull: freeLots === 0, lots: this.lots.map(lot => ({ ...lot })) }
  }
}

function render(api = new StubGarageApi()) {
  const TestHost = defineComponent({
    setup() {
      provide(garageApiKey, api)
      return () => h(IndexPage, { garageId: 1 })
    }
  })
  return { wrapper: mount(TestHost), api }
}

async function settle(): Promise<void> {
  await flushPromises()
}

function readRefreshCounts(api: StubGarageApi) {
  return [api.getLayout.mock.calls.length, api.getActiveVisits.mock.calls.length, api.getActiveParkings.mock.calls.length]
}

describe('garage dashboard with API stubs', () => {
  it('supports gate entry, linked parking, moving lots, and server receipt', async () => {
    const { wrapper, api } = render()
    await settle()
    expect(api.getLayout).toHaveBeenCalledWith(1, expect.any(AbortSignal))

    await wrapper.get('#license-number').setValue(' abc-123 ')
    const beforeEntry = readRefreshCounts(api)
    await wrapper.get('form.gate-form').trigger('submit')
    await settle()
    expect(api.enterGarage).toHaveBeenCalledWith(1, 'abc-123', null)
    expect(wrapper.text()).toContain('Sisäänajo kirjattu. ABC-123')
    expect(readRefreshCounts(api)).toEqual(beforeEntry.map(count => count + 1))

    await wrapper.get('nav button:nth-child(2)').trigger('click')
    await settle()
    await wrapper.get('.lot-card:nth-child(1) button').trigger('click')
    await wrapper.get('.lot-entry-form select').setValue('31')
    await wrapper.get('.lot-entry-form').trigger('submit')
    await settle()
    expect(api.enterLot).toHaveBeenCalledWith(1, 11, 31, null)
    expect(api.lots[0]?.currentStatus).toBe('Occupied')

    await wrapper.get('.lot-card:nth-child(1) button').trigger('click')
    await settle()
    expect(api.exitLot).toHaveBeenCalledWith(1, 11, null)
    expect(api.lots[0]?.currentStatus).toBe('Free')
    await wrapper.get('.lot-card:nth-child(4) button').trigger('click')
    await wrapper.get('.lot-entry-form select').setValue('31')
    await wrapper.get('.lot-entry-form').trigger('submit')
    await settle()
    expect(api.enterLot).toHaveBeenLastCalledWith(1, 14, 31, null)

    await wrapper.get('nav button:nth-child(1)').trigger('click')
    await settle()
    await wrapper.get('.segmented button:nth-child(2)').trigger('click')
    await wrapper.get('#license-number').setValue('abc-123')
    await wrapper.get('form.gate-form').trigger('submit')
    await settle()
    expect(api.exitGarage).toHaveBeenCalledWith(1, 'abc-123', null)
    expect(wrapper.get('.receipt-panel').text()).toContain('2,50 €')
    expect(wrapper.get('.receipt-panel').text()).toContain('1 h 15 min')
  })

  it('shows duplicate entry information with the original start time and refreshes all resources', async () => {
    const api = new StubGarageApi()
    api.entryOutcome = 'Duplicate'
    api.visits = [{ visitId: 31, licenseNumber: 'DUP-123', startAt: '2026-10-14T08:30:00Z', lotId: null, lotNumber: null }]
    const { wrapper } = render(api)
    await settle()
    const before = readRefreshCounts(api)
    await wrapper.get('#license-number').setValue('dup-123')
    await wrapper.get('form.gate-form').trigger('submit')
    await settle()

    expect(wrapper.get('[role="status"]').text()).toContain('Aktiivinen käynti oli jo olemassa.')
    expect(wrapper.get('[role="status"]').text()).toContain('Alkuperäinen alkamisaika:')
    expect(wrapper.get('[role="status"]').text()).toContain('14.10.2026')
    expect(api.visits).toHaveLength(1)
    expect(readRefreshCounts(api)).toEqual(before.map(count => count + 1))
  })

  it('warns accessibly on recovery, restarts billing, retains parking, and refreshes all resources', async () => {
    const api = new StubGarageApi()
    api.entryOutcome = 'RecoveredMissingExit'
    api.lots[0]!.currentStatus = 'Occupied'
    api.visits = [{ visitId: 31, licenseNumber: 'REC-123', startAt: '2026-10-14T08:00:00Z', lotId: 11, lotNumber: 1 }]
    api.parkings = [{ parkingId: 7, lotId: 11, lotNumber: 1, visitId: 31, licenseNumber: 'REC-123', startAt: '2026-10-14T08:10:00Z' }]
    const { wrapper } = render(api)
    await settle()
    const before = readRefreshCounts(api)

    await wrapper.get('#license-number').setValue('rec-123')
    await wrapper.get('form.gate-form').trigger('submit')
    await settle()

    const status = wrapper.get('[role="alert"]').text()
    expect(status).toContain('Aiempi käynti keskeytettiin.')
    expect(status).toContain('Uusi laskutus alkoi')
    expect(status).toContain('Paikka ei vapautunut.')
    expect(status).toContain('14.10.2026')
    expect(api.visits).toHaveLength(1)
    expect(api.visits[0]?.visitId).toBe(32)
    expect(api.parkings).toHaveLength(1)
    expect(api.parkings[0]?.parkingId).toBe(7)
    expect(api.lots[0]?.currentStatus).toBe('Occupied')
    expect(readRefreshCounts(api)).toEqual(before.map(count => count + 1))
    await wrapper.get('nav button:nth-child(2)').trigger('click')
    expect(wrapper.get('.lot-card:nth-child(1)').text()).toContain('Varattu')
    await wrapper.get('.lot-card:nth-child(4) button').trigger('click')
    expect((wrapper.get('.lot-entry-form select').element as HTMLSelectElement).value).toBe('')
    const options = wrapper.findAll('.lot-entry-form option').map(option => option.attributes('value'))
    expect(options).toContain('32')
    expect(options).not.toContain('31')
  })

  it('supports independent parking and only offers entry for Free lots', async () => {
    const { wrapper, api } = render()
    await settle()
    await wrapper.get('nav button:nth-child(2)').trigger('click')
    await settle()

    const cards = wrapper.findAll('.lot-card')
    expect(cards[0]?.find('button').text()).toBe('Kirjaa käyttö')
    expect(cards[1]?.findAll('button').map(button => button.text()).join(' ')).toContain('Simuloi paikan vapautuminen')
    expect(cards[2]?.findAll('button').map(button => button.text()).join(' ')).toContain('Simuloi paikan vapautuminen')
    expect(cards[1]?.text()).not.toContain('Yhdistä hallikäyntiin')
    expect(cards[2]?.text()).not.toContain('Yhdistä hallikäyntiin')

    await cards[0]!.get('button').trigger('click')
    await cards[0]!.get('form').trigger('submit')
    await settle()
    expect(api.enterLot).toHaveBeenCalledWith(1, 11, null, null)
  })

  it('refreshes availability, active visits, and parkings after a 409 and leaves gate closed without fabricated contact details', async () => {
    const api = new StubGarageApi()
    api.failNextGarageExit = true
    const { wrapper } = render(api)
    await settle()
    api.lots[0]!.currentStatus = 'Occupied'
    api.visits = [{ visitId: 44, licenseNumber: 'CURRENT', startAt: '2026-10-14T10:00:00Z', lotId: 11, lotNumber: 1 }]
    api.parkings = [{ parkingId: 9, lotId: 11, lotNumber: 1, visitId: 44, licenseNumber: 'CURRENT', startAt: '2026-10-14T10:10:00Z' }]
    const before = readRefreshCounts(api)

    await wrapper.get('.segmented button:nth-child(2)').trigger('click')
    await wrapper.get('#license-number').setValue('MISSING')
    await wrapper.get('form.gate-form').trigger('submit')
    await settle()

    const alert = wrapper.get('[role="alert"]')
    expect(alert.text()).toContain('Porttia ei avattu.')
    expect(alert.text()).toContain('Ota yhteys asiakaspalveluun.')
    expect(alert.text()).not.toMatch(/\b\d{3,}\b|@|https?:/i)
    expect(wrapper.find('.receipt-panel').exists()).toBe(false)
    expect(wrapper.text()).toContain('CURRENT')
    expect(wrapper.get('.stat-card:nth-child(4) strong').text()).toBe('1')
    await wrapper.get('nav button:nth-child(2)').trigger('click')
    expect(wrapper.get('.lot-card:nth-child(1)').text()).toContain('Varattu')
    expect(readRefreshCounts(api)).toEqual(before.map(count => count + 1))
  })

  it('refreshes a conflicting lot selection and removes a concurrently closed visit from options', async () => {
    const api = new StubGarageApi()
    api.visits = [{ visitId: 31, licenseNumber: 'CLOSED', startAt: '2026-10-14T08:00:00Z', lotId: null, lotNumber: null }]
    const { wrapper } = render(api)
    await settle()
    await wrapper.get('nav button:nth-child(2)').trigger('click')
    await wrapper.get('.layout-cell').trigger('click')
    await wrapper.get('#event-time').setValue('2026-10-14T13:30:45')
    await wrapper.get('.lot-card:nth-child(1) button').trigger('click')
    await wrapper.get('.lot-entry-form select').setValue('31')
    api.visits = []
    api.failNextLotEntry = true
    const before = readRefreshCounts(api)
    await wrapper.get('.lot-entry-form').trigger('submit')
    await settle()
    expect(wrapper.get('[role="alert"]').text()).toContain('LOT_UNAVAILABLE')
    expect(wrapper.get('.lot-card:nth-child(1)').text()).toContain('Varattu')
    expect((wrapper.get('#event-time').element as HTMLInputElement).value).toBe('2026-10-14T13:30:45.000')
    expect(wrapper.find('[aria-pressed="true"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('valittu')
    expect(readRefreshCounts(api)).toEqual(before.map(count => count + 1))
    await wrapper.get('.lot-card:nth-child(4) button').trigger('click')
    expect(wrapper.findAll('.lot-entry-form option').map(option => option.attributes('value'))).not.toContain('31')
  })
  it('clears an earlier receipt as soon as a subsequent exit attempt starts and fails', async () => {
    const api = new StubGarageApi()
    const { wrapper } = render(api)
    await settle()
    await wrapper.get('.segmented button:nth-child(2)').trigger('click')
    await wrapper.get('#license-number').setValue('FIRST')
    await wrapper.get('form.gate-form').trigger('submit')
    await settle()
    expect(wrapper.find('.receipt-panel').exists()).toBe(true)

    api.failNextGarageExit = true
    await wrapper.get('#license-number').setValue('SECOND')
    await wrapper.get('form.gate-form').trigger('submit')
    await settle()
    expect(wrapper.find('.receipt-panel').exists()).toBe(false)
    expect(wrapper.get('[role="alert"]').text()).toContain('ACTIVE_VISIT_NOT_FOUND')
  })

  it('re-fetches all data on remount instead of retaining a client-side snapshot', async () => {
    const api = new StubGarageApi()
    const first = render(api)
    await settle()
    first.wrapper.unmount()
    api.lots[0]!.currentStatus = 'Occupied'
    api.visits = [{ visitId: 31, licenseNumber: 'RELOAD', startAt: '2026-10-14T10:00:00Z', lotId: 11, lotNumber: 1 }]
    api.parkings = [{ parkingId: 1, lotId: 11, lotNumber: 1, visitId: 31, licenseNumber: 'RELOAD', startAt: '2026-10-14T10:10:00Z' }]

    const second = render(api)
    await settle()
    expect(api.getGarages).toHaveBeenCalledTimes(2)
    expect(api.getLayout).toHaveBeenCalledTimes(2)
    expect(second.wrapper.get('.stat-primary strong').text()).toContain('2')
    await second.wrapper.get('nav button:nth-child(2)').trigger('click')
    expect(second.wrapper.get('.lot-grid').text()).toContain('Varattu')
    expect(api.getActiveVisits).toHaveBeenCalledTimes(2)
    expect(api.getActiveParkings).toHaveBeenCalledTimes(2)
    await second.wrapper.get('.lot-card:nth-child(1) button').trigger('click')
    await settle()
    expect(api.exitLot).toHaveBeenCalledWith(1, 11, null)
    await second.wrapper.get('nav button:nth-child(1)').trigger('click')
    expect(second.wrapper.text()).toContain('RELOAD')
    await second.wrapper.get('.segmented button:nth-child(2)').trigger('click')
    await second.wrapper.get('#license-number').setValue('reload')
    await second.wrapper.get('form.gate-form').trigger('submit')
    await settle()
    expect(api.exitGarage).toHaveBeenCalledWith(1, 'reload', null)
  })
})

it('records entry in a full garage and prevents concurrent submissions', async () => {
  const api = new StubGarageApi()
  api.lots.forEach(lot => { lot.currentStatus = 'Unknown' })
  let finish!: () => void
  api.enterGarage.mockImplementationOnce(async (_id: number, licenseNumber: string) => {
    await new Promise<void>((resolve) => { finish = resolve })
    return { visitId: 31, licenseNumber, startAt: '2026-10-14T10:00:00Z', outcome: 'Entry' as const, gateOpened: true, effectiveOccurredAt: '2026-10-14T10:00:00Z' }
  })
  const { wrapper } = render(api)
  await settle()
  expect(wrapper.text()).toContain('Halli on täynnä')
  await wrapper.get('#license-number').setValue('FULL')
  await wrapper.get('form.gate-form').trigger('submit')
  await wrapper.get('form.gate-form').trigger('submit')
  expect(api.enterGarage).toHaveBeenCalledTimes(1)
  expect(wrapper.get('.gate-form button').attributes('disabled')).toBeDefined()
  expect(wrapper.get('.refresh-button').attributes('disabled')).toBeDefined()
  finish()
  await settle()
  expect(wrapper.get('.gate-form button').attributes('disabled')).toBeUndefined()
})

it('shows an empty garage list and a recoverable load failure', async () => {
  const api = new StubGarageApi()
  api.getGarages.mockRejectedValueOnce(new Error('Yhteys katkesi.'))
  api.getGarages.mockResolvedValueOnce([])
  const { wrapper } = render(api)
  await settle()
  expect(wrapper.get('[role="alert"]').text()).toContain('Yhteys katkesi.')
  await wrapper.get('.state-error button').trigger('click')
  await settle()
  expect(wrapper.text()).toContain('Hallia ei löytynyt (404).')
  expect(wrapper.find('.gate-form').exists()).toBe(false)
})

it('clears a closed visit selection in an already open lot form after API refresh', async () => {
  const wrapper = mount(LotCard, {
    props: {
      eventTimeValid: true,
      lot: { id: 14, number: 4, currentStatus: 'Free' },
      visits: [{ visitId: 31, licenseNumber: 'OLD', startAt: '2026-10-14T08:00:00Z', lotId: null, lotNumber: null }],
      submitting: false
    }
  })
  await wrapper.get('button').trigger('click')
  await wrapper.get('select').setValue('31')
  await wrapper.setProps({ visits: [{ visitId: 32, licenseNumber: 'NEW', startAt: '2026-10-14T12:00:00Z', lotId: null, lotNumber: null }] })
  expect((wrapper.get('select').element as HTMLSelectElement).value).toBe('')
  expect(wrapper.find('option[value="31"]').exists()).toBe(false)
  await wrapper.get('form').trigger('submit')
  expect(wrapper.emitted('enter')).toEqual([[14, null]])
})


describe('event time on simulation commands', () => {
  it('snapshots the explicit offset before a pending request and locks the shared time controls', async () => {
    const api = new StubGarageApi()
    let finish!: (receipt: EntryDto) => void
    api.enterGarage.mockImplementationOnce(async (_id: number, _licenseNumber: string) => await new Promise<EntryDto>(resolve => { finish = resolve }).then(result => result))
    const { wrapper } = render(api)
    await settle()
    const localTime = '2026-10-14T13:30:45'
    await wrapper.get('#event-time').setValue(localTime)
    await wrapper.get('#license-number').setValue(' SNAP-123 ')
    await wrapper.get('.gate-form').trigger('submit')

    const expectedOccurredAt = toEventTime(localTime).occurredAt
    expect(api.enterGarage).toHaveBeenCalledWith(1, 'SNAP-123', expectedOccurredAt)
    expect(wrapper.get('#event-time').attributes('disabled')).toBeDefined()
    expect(wrapper.findAll('.event-time-actions button').every(button => button.attributes('disabled') !== undefined)).toBe(true)

    finish({ visitId: 50, licenseNumber: 'SNAP-123', startAt: '2026-10-14T10:30:45Z', outcome: 'Entry', gateOpened: true, effectiveOccurredAt: '2026-10-14T10:30:45Z' })
    await settle()
    expect(wrapper.get('#event-time').attributes('disabled')).toBeUndefined()
  })

  it('shows the minimum event time after 409 and preserves event time, registry and selected lot across refresh', async () => {
    const api = new StubGarageApi()
    const { wrapper } = render(api)
    await settle()
    const localTime = '2026-10-14T13:30:45'
    await wrapper.get('#event-time').setValue(localTime)
    await wrapper.get('#license-number').setValue('CONFLICT-1')
    api.enterGarage.mockRejectedValueOnce(Object.assign(new Error('Aika on liian aikainen.'), {
      status: 409,
      problem: { code: 'EVENT_TIME_CONFLICT', minimumOccurredAt: '2026-10-14T10:00:00Z' }
    }))
    await wrapper.get('.gate-form').trigger('submit')
    await settle()

    expect(api.enterGarage).toHaveBeenCalledWith(1, 'CONFLICT-1', toEventTime(localTime).occurredAt)
    expect(wrapper.get('#event-time').element).toHaveProperty('value', `${localTime}.000`)
    expect((wrapper.get('#license-number').element as HTMLInputElement).value).toBe('CONFLICT-1')
    expect(wrapper.get('[role="alert"]').text()).toContain('2026-10-14T10:00:00Z')

    await wrapper.get('nav button:nth-child(2)').trigger('click')
    await wrapper.get('.layout-cell').trigger('click')
    await wrapper.get('.refresh-button').trigger('click')
    await settle()
    expect(wrapper.get('#event-time').element).toHaveProperty('value', `${localTime}.000`)
    expect(wrapper.text()).toContain('valittu')
    wrapper.unmount()
  })

  it('rejects impossible local dates instead of producing an API timestamp', () => {
    const invalid = toEventTime('2026-02-30T10:20:30')
    expect(invalid.error).toBeTruthy()
    expect(invalid.occurredAt).toBeNull()
  })
})


it('blocks every parking and gate command for an impossible local event time', async () => {
  const api = new StubGarageApi()
  const { wrapper } = render(api)
  await settle()
  wrapper.findComponent(EventTimeInput).vm.$emit('update:modelValue', '2026-02-30T10:20:30')
  await settle()
  expect(wrapper.get('#event-time').attributes('aria-invalid')).toBe('true')
  await wrapper.get('#license-number').setValue('INVALID-TIME')
  expect(wrapper.get('.gate-form button[type="submit"]').attributes('disabled')).toBeDefined()
  await wrapper.get('.gate-form').trigger('submit')
  expect(api.enterGarage).not.toHaveBeenCalled()

  await wrapper.get('nav button:nth-child(2)').trigger('click')
  await wrapper.get('.lot-card:nth-child(1) button').trigger('click')
  expect(wrapper.get('.lot-entry-form button[type="submit"]').attributes('disabled')).toBeDefined()
  await wrapper.get('.lot-entry-form').trigger('submit')
  expect(api.enterLot).not.toHaveBeenCalled()
  expect(wrapper.get('.lot-card:nth-child(2) .lot-action').attributes('disabled')).toBeDefined()
  await wrapper.get('.lot-card:nth-child(2) .lot-action').trigger('click')
  expect(api.exitLot).not.toHaveBeenCalled()
  wrapper.unmount()
})


it('shows disabled simulation-time feedback without losing the selected time or license', async () => {
  const api = new StubGarageApi()
  const { wrapper } = render(api)
  await settle()
  await wrapper.get('#event-time').setValue('2026-10-14T13:30:45')
  await wrapper.get('#license-number').setValue('KEEP-TIME')
  api.enterGarage.mockRejectedValueOnce(Object.assign(new Error('Simulation time disabled'), {
    status: 400, problem: { code: 'SIMULATION_TIME_DISABLED', gateOpened: false }
  }))
  await wrapper.get('.gate-form').trigger('submit')
  await settle()
  expect(wrapper.get('[role="alert"]').text()).toContain('SIMULATION_TIME_DISABLED')
  expect((wrapper.get('#event-time').element as HTMLInputElement).value).toContain('2026-10-14T13:30:45')
  expect((wrapper.get('#license-number').element as HTMLInputElement).value).toBe('KEEP-TIME')
  expect(wrapper.get('#event-time').attributes('disabled')).toBeUndefined()
  wrapper.unmount()
})
