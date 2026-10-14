import { mount, flushPromises } from '@vue/test-utils'
import { defineComponent, h, provide } from 'vue'
import { describe, expect, it, vi } from 'vitest'
import IndexPage from './index.vue'
import LotCard from '../components/LotCard.vue'
import { garageApiKey, type GarageApi } from '../services/garageApi'
import type { ActiveParkingDto, ActiveVisitDto, AvailabilityDto, EntryDto, GarageDto, LotDto, ReceiptDto } from '../types/garage'

const garages: GarageDto[] = [{ id: 1, name: 'Keskustan pysäköinti' }]
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
    return { visitId, licenseNumber: normalized, startAt, outcome: this.entryOutcome, gateOpened: true }
  })
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
      gateOpened: true
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
    return { parkingId: this.parkings.length, lotId, visitId, startAt: '2026-10-14T10:10:00Z' }
  })
  exitLot = vi.fn(async (_garageId: number, lotId: number) => {
    this.lots.find(lot => lot.id === lotId)!.currentStatus = 'Free'
    this.parkings = this.parkings.filter(parking => parking.lotId !== lotId)
    return { lotId, parkingId: 1, missingParkingObservation: false }
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
      return () => h(IndexPage)
    }
  })
  return { wrapper: mount(TestHost), api }
}

async function settle(): Promise<void> {
  await flushPromises()
}

function readRefreshCounts(api: StubGarageApi) {
  return [api.getAvailability.mock.calls.length, api.getActiveVisits.mock.calls.length, api.getActiveParkings.mock.calls.length]
}

describe('garage dashboard with API stubs', () => {
  it('supports gate entry, linked parking, moving lots, and server receipt', async () => {
    const { wrapper, api } = render()
    await settle()
    expect(api.getAvailability).toHaveBeenCalledWith(1)

    await wrapper.get('#license-number').setValue(' abc-123 ')
    const beforeEntry = readRefreshCounts(api)
    await wrapper.get('form.gate-form').trigger('submit')
    await settle()
    expect(api.enterGarage).toHaveBeenCalledWith(1, 'abc-123')
    expect(wrapper.text()).toContain('Sisäänajo kirjattu. ABC-123')
    expect(readRefreshCounts(api)).toEqual(beforeEntry.map(count => count + 1))

    await wrapper.get('nav button:nth-child(2)').trigger('click')
    await settle()
    await wrapper.get('.lot-card:nth-child(1) button').trigger('click')
    await wrapper.get('.lot-entry-form select').setValue('31')
    await wrapper.get('.lot-entry-form').trigger('submit')
    await settle()
    expect(api.enterLot).toHaveBeenCalledWith(1, 11, 31)
    expect(api.lots[0]?.currentStatus).toBe('Occupied')

    await wrapper.get('.lot-card:nth-child(1) button').trigger('click')
    await settle()
    expect(api.exitLot).toHaveBeenCalledWith(1, 11)
    expect(api.lots[0]?.currentStatus).toBe('Free')
    await wrapper.get('.lot-card:nth-child(4) button').trigger('click')
    await wrapper.get('.lot-entry-form select').setValue('31')
    await wrapper.get('.lot-entry-form').trigger('submit')
    await settle()
    expect(api.enterLot).toHaveBeenLastCalledWith(1, 14, 31)

    await wrapper.get('nav button:nth-child(1)').trigger('click')
    await settle()
    await wrapper.get('.segmented button:nth-child(2)').trigger('click')
    await wrapper.get('#license-number').setValue('abc-123')
    await wrapper.get('form.gate-form').trigger('submit')
    await settle()
    expect(api.exitGarage).toHaveBeenCalledWith(1, 'abc-123')
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
    expect(api.enterLot).toHaveBeenCalledWith(1, 11, null)
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
    await wrapper.get('.lot-card:nth-child(1) button').trigger('click')
    await wrapper.get('.lot-entry-form select').setValue('31')
    api.visits = []
    api.failNextLotEntry = true
    const before = readRefreshCounts(api)
    await wrapper.get('.lot-entry-form').trigger('submit')
    await settle()
    expect(wrapper.get('[role="alert"]').text()).toContain('LOT_UNAVAILABLE')
    expect(wrapper.get('.lot-card:nth-child(1)').text()).toContain('Varattu')
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
    expect(api.getAvailability).toHaveBeenCalledTimes(2)
    expect(second.wrapper.get('.stat-primary strong').text()).toContain('2')
    await second.wrapper.get('nav button:nth-child(2)').trigger('click')
    expect(second.wrapper.get('.lot-grid').text()).toContain('Varattu')
    expect(api.getActiveVisits).toHaveBeenCalledTimes(2)
    expect(api.getActiveParkings).toHaveBeenCalledTimes(2)
    await second.wrapper.get('.lot-card:nth-child(1) button').trigger('click')
    await settle()
    expect(api.exitLot).toHaveBeenCalledWith(1, 11)
    await second.wrapper.get('nav button:nth-child(1)').trigger('click')
    expect(second.wrapper.text()).toContain('RELOAD')
    await second.wrapper.get('.segmented button:nth-child(2)').trigger('click')
    await second.wrapper.get('#license-number').setValue('reload')
    await second.wrapper.get('form.gate-form').trigger('submit')
    await settle()
    expect(api.exitGarage).toHaveBeenCalledWith(1, 'reload')
  })
})

it('records entry in a full garage and prevents concurrent submissions', async () => {
  const api = new StubGarageApi()
  api.lots.forEach(lot => { lot.currentStatus = 'Unknown' })
  let finish!: () => void
  api.enterGarage.mockImplementationOnce(async (_id: number, licenseNumber: string) => {
    await new Promise<void>((resolve) => { finish = resolve })
    return { visitId: 31, licenseNumber, startAt: '2026-10-14T10:00:00Z', outcome: 'Entry' as const, gateOpened: true }
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
  expect(wrapper.text()).toContain('Halleja ei löytynyt.')
  expect(wrapper.find('.gate-form').exists()).toBe(false)
})

it('clears a closed visit selection in an already open lot form after API refresh', async () => {
  const wrapper = mount(LotCard, {
    props: {
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
