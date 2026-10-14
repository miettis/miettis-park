import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import GarageListPage from './admin/garages/index.vue'
import GarageFormPage from './admin/garages/[id]/index.vue'
import LayoutPage from './admin/garages/[id]/layout.vue'
import GarageMap from '../components/GarageMap.vue'
import { mapLibreLoaderKey } from '../services/mapLibre'
import { garageApiKey, HttpGarageApi, type GarageManagementApi } from '../services/garageApi'
import type { GarageDto, GarageLayoutDto, GarageLayoutInputDto } from '../types/garage'

const mapMocks = {
  mapConstructor: vi.fn((_options: unknown) => undefined),
  markerConstructor: vi.fn(() => undefined),
  mapInstance: null as unknown,
  markerInstance: null as unknown
}

function mapLoader() {
  return async () => await Promise.resolve({
    Map: function Map(options: unknown) {
      mapMocks.mapConstructor(options)
      return mapMocks.mapInstance as never
    },
    Marker: function Marker() {
      mapMocks.markerConstructor()
      return mapMocks.markerInstance as never
    }
  } as unknown as typeof import('maplibre-gl'))
}
const routeMocks = vi.hoisted(() => ({
  id: '41',
  replace: vi.fn(),
  leave: undefined as ((...args: unknown[]) => unknown) | undefined
}))

vi.mock('vue-router', () => ({
  useRoute: () => ({ params: { id: routeMocks.id } }),
  useRouter: () => ({ replace: routeMocks.replace }),
  onBeforeRouteLeave: (guard: unknown) => {
    routeMocks.leave = guard as (...args: unknown[]) => unknown
  }
}))

const garage: GarageDto = {
  id: 41,
  name: 'Testihalli',
  address: 'Testikatu 1',
  latitude: 60.17,
  longitude: 24.94,
  capacity: 2,
  freeLots: 2,
  occupiedLots: 0,
  unknownLots: 0,
  isFull: false
}

function layout(): GarageLayoutDto {
  return {
    garageId: 41,
    width: 3,
    height: 2,
    version: 7,
    cells: [
      { x: 0, y: 0, type: 'ParkingLot', lotId: 101 },
      { x: 1, y: 0, type: 'Road', lotId: null },
      { x: 2, y: 0, type: 'ParkingLot', lotId: 102 },
      { x: 0, y: 1, type: 'Empty', lotId: null },
      { x: 1, y: 1, type: 'Empty', lotId: null },
      { x: 2, y: 1, type: 'Empty', lotId: null }
    ],
    lots: [
      { id: 101, number: 1, currentStatus: 'Free', canRemove: true },
      { id: 102, number: 2, currentStatus: 'Free', canRemove: true }
    ]
  }
}

function apiStub(): GarageManagementApi {
  return {
    getGarages: vi.fn(async () => [garage]),
    createGarage: vi.fn(async (input) => ({ ...garage, ...input, id: 83 })),
    updateGarage: vi.fn(async (id, input) => ({ ...garage, ...input, id })),
    getLayout: vi.fn(async () => layout()),
    saveLayout: vi.fn(async (_id, input) => savedLayout(input))
  }
}

function savedLayout(input: GarageLayoutInputDto): GarageLayoutDto {
  const lotsById = new Map(layout().lots.map(lot => [lot.id, { ...lot }]))
  for (const id of input.removedLotIds) lotsById.delete(id)
  let nextId = 500
  const ids = new Map<string, number>()
  for (const cell of input.cells) {
    if (cell.type !== 'ParkingLot') continue
    if (cell.lotId === null) {
      const id = nextId++
      ids.set(`${cell.x},${cell.y}`, id)
      lotsById.set(id, { id, number: cell.number ?? id - 499, currentStatus: 'Free', canRemove: true })
    }
    else {
      const lot = lotsById.get(cell.lotId)
      if (lot && cell.number !== null) lot.number = cell.number
    }
  }
  return {
    garageId: 41,
    width: input.width,
    height: input.height,
    version: input.expectedVersion + 1,
    cells: input.cells.map(cell => ({ x: cell.x, y: cell.y, type: cell.type, lotId: cell.type === 'ParkingLot' ? cell.lotId ?? ids.get(`${cell.x},${cell.y}`)! : null })),
    lots: Array.from(lotsById.values())
  }
}

beforeEach(() => {
  mapMocks.mapInstance = {
    remove: vi.fn(),
    on: vi.fn((_event: string, _handler: () => void) => undefined),
    once: vi.fn((_event: string, callback: () => void) => callback())
  }
  mapMocks.markerInstance = { setLngLat: vi.fn().mockReturnThis(), addTo: vi.fn().mockReturnThis(), remove: vi.fn() }
})

function provide(api: GarageManagementApi) {
  return { global: { provide: { [garageApiKey]: api, [mapLibreLoaderKey]: mapLoader() } } }
}

afterEach(() => {
  routeMocks.id = '41'
  routeMocks.replace.mockReset()
  routeMocks.leave = undefined
  mapMocks.mapConstructor.mockClear()
  mapMocks.markerConstructor.mockClear()
  mapMocks.mapInstance = null
  mapMocks.markerInstance = null
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

describe('hallien ylläpidon DOM-näkymät', () => {
  it('listaa hallit saatavuuslukujen kanssa ja näyttää hakuvirheessä uudelleenyrityksen', async () => {
    const api = apiStub()
    const wrapper = mount(GarageListPage, provide(api))
    await flushPromises()
    expect(wrapper.text()).toContain('Testihalli')
    expect(wrapper.text()).toContain('vapaita')
    expect(wrapper.find('a[href="/admin/garages/41/layout"]').exists()).toBe(true)
    api.getGarages = vi.fn().mockRejectedValueOnce(new Error('Yhteysvirhe')).mockResolvedValue([garage])
    await wrapper.get('button').trigger('click')
    await flushPromises()
    expect(wrapper.get('[role="alert"]').text()).toContain('Yhteysvirhe')
    await wrapper.get('[role="alert"] button').trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('Testihalli')
    wrapper.unmount()
  })

  it('luo hallin labeloidulla lomakkeella ja käyttää palvelimen palauttamaa ID:tä reititykseen', async () => {
    routeMocks.id = 'new'
    const api = apiStub()
    api.getGarages = vi.fn(async () => [])
    const wrapper = mount(GarageFormPage, provide(api))
    await flushPromises()
    expect(wrapper.get('h1').text()).toBe('Lisää halli')
    expect(wrapper.get('label').text()).toContain('Nimi')
    await wrapper.get('input[autocomplete="organization"]').setValue('Uusi halli')
    await wrapper.get('input[autocomplete="street-address"]').setValue('Testiosoite')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(api.createGarage).toHaveBeenCalledWith({ name: 'Uusi halli', address: 'Testiosoite', latitude: null, longitude: null })
    expect(routeMocks.replace).toHaveBeenCalledWith('/admin/garages/83')
    expect(wrapper.text()).toContain('Hallin tiedot tallennettiin')
    wrapper.unmount()
  })

  it('hallitsee koordinaattien kenttävirheet ja säilyttää editointilomakkeen API-virheessä', async () => {
    const api = apiStub()
    api.updateGarage = vi.fn().mockRejectedValue(new Error('Palvelu ei vastaa'))
    const wrapper = mount(GarageFormPage, provide(api))
    await flushPromises()
    expect((wrapper.get('input[autocomplete="organization"]').element as HTMLInputElement).value).toBe('Testihalli')
    await wrapper.get('input[autocomplete="organization"]').setValue('Muokattu nimi')
    await wrapper.get('input[min="-90"]').setValue('60')
    await wrapper.get('input[min="-180"]').setValue('')
    await wrapper.get('form').trigger('submit')
    expect(wrapper.get('[role="alert"]').text()).toContain('molemmat koordinaatit')
    await wrapper.get('input[min="-180"]').setValue('25')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(api.updateGarage).toHaveBeenCalledWith(41, { name: 'Muokattu nimi', address: 'Testikatu 1', latitude: 60, longitude: 25 })
    expect(wrapper.get('[role="alert"]').text()).toContain('Luonnos on säilytetty')
    expect((wrapper.get('input[autocomplete="organization"]').element as HTMLInputElement).value).toBe('Muokattu nimi')
    wrapper.unmount()
  })
})

describe('ruudukkoeditori', () => {
  it('maalaa pointerilla ja näppäimistöllä, siirtää paikkaa, numeroinnin ja peruutuksen', async () => {
    const api = apiStub()
    const wrapper = mount(LayoutPage, provide(api))
    await flushPromises()
    const cells = wrapper.findAll('[role="gridcell"]')
    await wrapper.get('.lot-row button').trigger('click')
    await cells[3]!.trigger('click')
    expect(cells[0]!.classes()).toContain('cell-Empty')
    expect(cells[3]!.text()).toContain('1')
    await cells[4]!.trigger('pointerdown')
    await cells[5]!.trigger('pointerenter')
    window.dispatchEvent(new Event('pointerup'))
    expect(cells[4]!.text()).toMatch(/^Uusi \d+$/)
    expect(cells[5]!.text()).toMatch(/^Uusi \d+$/)
    await cells[3]!.trigger('keydown', { key: ' ' })
    expect(cells[3]!.text()).toContain('1')
    const number = wrapper.get('.lot-row input')
    await number.setValue('9')
    await number.trigger('change')
    expect(cells[3]!.text()).toContain('9')
    expect(wrapper.text()).toContain('Tallentamattomia muutoksia')
    await wrapper.get('.editor-actions .secondary-button').trigger('click')
    expect(wrapper.findAll('[role="gridcell"]')[0]!.text()).toContain('1')
    expect(wrapper.text()).not.toContain('Tallentamattomia muutoksia')
    wrapper.unmount()
  })

  it('shrinks the grid, computes exact removed IDs, and sends an optimistic version', async () => {
    const api = apiStub()
    const wrapper = mount(LayoutPage, provide(api))
    await flushPromises()
    const width = wrapper.get('input[aria-label="Ruudukon leveys"]')
    await width.setValue('2')
    expect(wrapper.findAll('[role="gridcell"]')).toHaveLength(6)
    expect(wrapper.get('[role="alert"]').text()).toContain('nimenomaisesti')
    await wrapper.get('input[value="Empty"]').setValue()
    await wrapper.findAll('[role="gridcell"]')[2]!.trigger('click')
    await width.setValue('2')
    expect(wrapper.findAll('[role="gridcell"]')).toHaveLength(4)
    await wrapper.get('.editor-actions .primary-button').trigger('click')
    await flushPromises()
    expect(api.saveLayout).toHaveBeenCalledWith(41, expect.objectContaining({
      expectedVersion: 7,
      width: 2,
      height: 2,
      removedLotIds: [102]
    }))
    expect(wrapper.text()).toContain('Pohja tallennettiin')
    expect(wrapper.text()).not.toContain('Tallentamattomia muutoksia')
    wrapper.unmount()
  })

  it.each([
    [{ status: 409, problem: { code: 'LAYOUT_VERSION_CONFLICT' } }, 'muutettu muualla'],
    [{ status: 409, problem: { code: 'LOT_REMOVAL_CONFLICT' } }, 'ei voi poistaa'],
    [{ status: 404, problem: { code: 'GARAGE_NOT_FOUND' } }, 'ei löytynyt'],
    [new Error('Verkkovirhe'), 'Verkkovirhe']
  ])('säilyttää luonnoksen tallennusvirheen jälkeen (%s)', async (failure, expectedMessage) => {
    const api = apiStub()
    api.saveLayout = vi.fn().mockRejectedValue(failure)
    const wrapper = mount(LayoutPage, provide(api))
    await flushPromises()
    const cells = wrapper.findAll('[role="gridcell"]')
    await cells[3]!.trigger('click')
    expect(wrapper.text()).toContain('Tallentamattomia muutoksia')
    await wrapper.get('.editor-actions .primary-button').trigger('click')
    await flushPromises()
    expect(wrapper.get('[role="alert"]').text()).toContain(expectedMessage)
    expect(wrapper.text()).toContain('Tallentamattomia muutoksia')
    expect(wrapper.findAll('[role="gridcell"]')[3]!.text()).toMatch(/^Uusi \d+$/)
    wrapper.unmount()
  })

  it('varmistaa poistumisen dirty-reitiltä ja estää beforeunloadin', async () => {
    const api = apiStub()
    const confirm = vi.fn(() => false)
    vi.stubGlobal('confirm', confirm)
    const wrapper = mount(LayoutPage, provide(api))
    await flushPromises()
    await wrapper.findAll('[role="gridcell"]')[3]!.trigger('click')
    expect(routeMocks.leave?.()).toBe(false)
    expect(confirm).toHaveBeenCalled()
    const event = new Event('beforeunload', { cancelable: true })
    window.dispatchEvent(event)
    expect(event.defaultPrevented).toBe(true)
    await wrapper.get('.editor-actions .secondary-button').trigger('click')
    const cleanEvent = new Event('beforeunload', { cancelable: true })
    window.dispatchEvent(cleanEvent)
    expect(cleanEvent.defaultPrevented).toBe(false)
    wrapper.unmount()
  })
})

describe('MapLibre/OpenFreeMap komponentti', () => {
  it('mounttaa kartan lazy-loadilla, näyttää map errorin ja retryttää sekä poistaa mapin unmountissa', async () => {
    const mapInstance = {
      remove: vi.fn(),
      on: vi.fn((_event: string, _handler: () => void) => undefined),
      once: vi.fn((_event: string, callback: () => void) => callback())
    }
    const markerInstance = { setLngLat: vi.fn().mockReturnThis(), addTo: vi.fn().mockReturnThis(), remove: vi.fn() }
    mapMocks.mapInstance = mapInstance
    mapMocks.markerInstance = markerInstance
    const wrapper = mount(GarageMap, { props: { latitude: 60.17, longitude: 24.94, label: 'Testihalli' }, global: { provide: { [mapLibreLoaderKey]: mapLoader() } } })
    await flushPromises()
    expect(mapMocks.mapConstructor).toHaveBeenCalledWith(expect.objectContaining({
      style: 'https://tiles.openfreemap.org/styles/liberty',
      center: [24.94, 60.17],
      attributionControl: {}
    }))
    expect(mapMocks.markerConstructor).toHaveBeenCalledOnce()
    expect(wrapper.text()).toContain('OpenStreetMap')
    const errorHandler = mapInstance.on.mock.calls[0]?.[1]
    errorHandler?.()
    await wrapper.vm.$nextTick()
    expect(wrapper.get('[role="alert"]').text()).toContain('Karttapalveluun')
    await wrapper.get('button').trigger('click')
    await flushPromises()
    expect(mapInstance.remove).toHaveBeenCalled()
    wrapper.unmount()
    expect(mapInstance.remove).toHaveBeenCalled()
    expect(markerInstance.remove).toHaveBeenCalled()
  })

  it('näyttää koordinaattien puuttuessa tekstivarauman alustamatta ulkoista karttaa', async () => {
    const wrapper = mount(GarageMap, { props: { latitude: null, longitude: null, label: 'Ei sijaintia' }, global: { provide: { [mapLibreLoaderKey]: mapLoader() } } })
    await flushPromises()
    expect(wrapper.get('[role="status"]').text()).toContain('ei ole määritetty')
    wrapper.unmount()
  })
})

describe('HTTP halliylläpidon DTO-reitit', () => {
  it('käyttää suhteellisia reittejä, oikeita verbejä ja lähettää sopimusbodyt', async () => {
    const fetch = vi.fn(async (_path: string, _init?: RequestInit) => ({ ok: true, status: 200, json: async () => garage }))
    vi.stubGlobal('fetch', fetch)
    const api = new HttpGarageApi()
    await api.createGarage({ name: garage.name, address: garage.address, latitude: garage.latitude, longitude: garage.longitude })
    await api.updateGarage(41, { name: garage.name, address: null, latitude: null, longitude: null })
    await api.getLayout(41)
    await api.saveLayout(41, { expectedVersion: 7, width: 1, height: 1, cells: [], removedLotIds: [] })
    expect(fetch.mock.calls.map(call => [call[0], call[1]?.method ?? 'GET'])).toEqual([
      ['/api/garages', 'POST'], ['/api/garages/41', 'PUT'], ['/api/garages/41/layout', 'GET'], ['/api/garages/41/layout', 'PUT']
    ])
    expect(JSON.parse(String(fetch.mock.calls[0]?.[1]?.body))).toEqual({ name: garage.name, address: garage.address, latitude: garage.latitude, longitude: garage.longitude })
    expect(JSON.parse(String(fetch.mock.calls[3]?.[1]?.body))).toEqual({ expectedVersion: 7, width: 1, height: 1, cells: [], removedLotIds: [] })
  })

  it('säilyttää ProblemDetails code/status-virheet UI:lle', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => ({ ok: false, status: 409, json: async () => ({ title: 'Ristiriita', code: 'LAYOUT_VERSION_CONFLICT' }) })))
    await expect(new HttpGarageApi().getLayout(41)).rejects.toMatchObject({ status: 409, problem: { code: 'LAYOUT_VERSION_CONFLICT' } })
  })
})


describe('editorin suojaavat reunapolut', () => {
  it('vahvistaa ajoradan pienennyksen ja sallii 50 ? 50 mitat', async () => {
    const api = apiStub()
    const current = layout()
    current.cells[2] = { x: 2, y: 0, type: 'Road', lotId: null }
    current.lots.pop()
    api.getLayout = vi.fn(async () => current)
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false)
    const wrapper = mount(LayoutPage, provide(api))
    await flushPromises()
    await wrapper.get('input[aria-label="Ruudukon leveys"]').setValue('2')
    expect(confirm).toHaveBeenCalled()
    expect(wrapper.findAll('[role="gridcell"]')).toHaveLength(6)
    confirm.mockReturnValue(true)
    await wrapper.get('input[aria-label="Ruudukon leveys"]').setValue('2')
    expect(wrapper.findAll('[role="gridcell"]')).toHaveLength(4)
    await wrapper.get('input[aria-label="Ruudukon leveys"]').setValue('50')
    await wrapper.get('input[aria-label="Ruudukon korkeus"]').setValue('50')
    expect(wrapper.findAll('[role="gridcell"]')).toHaveLength(2500)
    expect(api.saveLayout).not.toHaveBeenCalled()
    wrapper.unmount()
  })

  it('käsittelee ruudun kerran vedossa ja lopettaa peruutetun pointerin', async () => {
    const api = apiStub()
    const wrapper = mount(LayoutPage, provide(api))
    await flushPromises()
    const cells = wrapper.findAll('[role="gridcell"]')
    await cells[3]!.trigger('pointerdown')
    await wrapper.get('input[value="Road"]').setValue()
    await cells[3]!.trigger('pointerenter')
    expect(cells[3]!.text()).toMatch(/^Uusi/)
    window.dispatchEvent(new Event('pointercancel'))
    await cells[4]!.trigger('pointerenter')
    expect(cells[4]!.text()).not.toContain('Ajorata')
    expect(api.saveLayout).not.toHaveBeenCalled()
    wrapper.unmount()
  })

  it('säilyttää luonnoksen reload-peruutuksessa ja käyttää tallennetut ID:t seuraavassa tallennuksessa', async () => {
    const api = apiStub()
    const wrapper = mount(LayoutPage, provide(api))
    await flushPromises()
    await wrapper.findAll('[role="gridcell"]')[3]!.trigger('click')
    vi.spyOn(window, 'confirm').mockReturnValue(false)
    await wrapper.findAll('button').find(button => button.text() === 'Lataa pohja uudelleen')!.trigger('click')
    expect(api.getLayout).toHaveBeenCalledTimes(2)
    await wrapper.get('.editor-actions .primary-button').trigger('click')
    await flushPromises()
    expect(wrapper.findAll('[role="gridcell"]')[3]!.text()).toContain('1')
    expect(wrapper.text()).not.toContain('Tallentamattomia muutoksia')
    await wrapper.get('input[value="Road"]').setValue()
    await wrapper.findAll('[role="gridcell"]')[4]!.trigger('click')
    await wrapper.get('.editor-actions .primary-button').trigger('click')
    await flushPromises()
    const calls = vi.mocked(api.saveLayout).mock.calls
    expect(calls[1]![1].expectedVersion).toBe(8)
    expect(calls[1]![1].cells[3]!.lotId).toBe(500)
    wrapper.unmount()
  })

  it('valitsee uuden hallin pisteen kartalta ja keskipisteen näppäimistöpainikkeella', async () => {
    const handlers = new Map<string, (event?: unknown) => void>()
    mapMocks.mapInstance = {
      remove: vi.fn(),
      on: vi.fn((name: string, handler: (event?: unknown) => void) => handlers.set(name, handler)),
      once: vi.fn((name: string, handler: (event?: unknown) => void) => handlers.set(name, handler)),
      getCenter: () => ({ lat: 61, lng: 25 })
    }
    const wrapper = mount(GarageMap, { props: { latitude: null, longitude: null, label: 'Uusi', selectable: true }, global: { provide: { [mapLibreLoaderKey]: mapLoader() } } })
    await flushPromises()
    expect(mapMocks.markerConstructor).not.toHaveBeenCalled()
    handlers.get('load')?.()
    await wrapper.vm.$nextTick()
    handlers.get('click')?.({ lngLat: { lat: 62, lng: 26 } })
    expect(wrapper.emitted('select')?.[0]).toEqual([{ latitude: 62, longitude: 26 }])
    await wrapper.findAll('button').find(button => button.text() === 'Valitse kartan keskipiste')!.trigger('click')
    expect(wrapper.emitted('select')?.[1]).toEqual([{ latitude: 61, longitude: 25 }])
    wrapper.unmount()
  })

  it('ei alusta karttaa jos komponentti poistuu kesken lazy-importin', async () => {
    let resolve!: (value: Awaited<ReturnType<ReturnType<typeof mapLoader>>>) => void
    const delayed = () => new Promise<Awaited<ReturnType<ReturnType<typeof mapLoader>>>>(done => { resolve = done })
    const wrapper = mount(GarageMap, { props: { latitude: 60, longitude: 24, label: 'Halli' }, global: { provide: { [mapLibreLoaderKey]: delayed } } })
    wrapper.unmount()
    resolve(await mapLoader()())
    await flushPromises()
    expect(mapMocks.mapConstructor).not.toHaveBeenCalled()
  })
  it('maalaa myös pointer-kaappauksen aikana osumatestatusta ruudusta', async () => {
    const api = apiStub()
    const wrapper = mount(LayoutPage, provide(api))
    await flushPromises()
    const cells = wrapper.findAll('.grid-cell')
    await wrapper.get('input[value="Road"]').setValue()
    await cells[0]!.trigger('pointerdown')
    const hitTest = vi.fn().mockReturnValue(cells[4]!.element)
    Object.defineProperty(document, 'elementFromPoint', { configurable: true, value: hitTest })
    await wrapper.get('.layout-grid').trigger('pointermove')
    expect(cells[4]!.text()).toContain('Ajoväylä')
    window.dispatchEvent(new Event('pointerup'))
    delete (document as unknown as Record<string, unknown>).elementFromPoint
    wrapper.unmount()
  })

})
