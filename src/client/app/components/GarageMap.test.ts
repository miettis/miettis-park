import { flushPromises, mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import GarageMap from './GarageMap.vue'
import { mapLibreLoaderKey } from '../services/mapLibre'
import type { GarageDto } from '../types/garage'

function garage(overrides: Partial<GarageDto> = {}): GarageDto {
  return {
    id: 1,
    name: 'Keskustan halli',
    address: 'Torikatu 3',
    latitude: 60.17,
    longitude: 24.94,
    capacity: 8,
    freeLots: 2,
    occupiedLots: 5,
    unknownLots: 1,
    isFull: false,
    ...overrides
  }
}

function mapStub() {
  const markers: Array<{ element: HTMLElement, remove: ReturnType<typeof vi.fn>, setLngLat: ReturnType<typeof vi.fn> }> = []
  let container: HTMLElement | null = null
  const mapInstance = {
    on: vi.fn(),
    once: vi.fn((_event: string, callback: () => void) => callback()),
    fitBounds: vi.fn(),
    remove: vi.fn(),
    getCenter: vi.fn(() => ({ lat: 60, lng: 24 }))
  }
  const loader = async () => ({
    Map: function Map(options: { container: HTMLElement }) {
      container = options.container
      return mapInstance
    },
    Marker: function Marker(options?: { element?: HTMLElement }) {
      const entry = {
        element: options?.element ?? document.createElement('div'),
        remove: vi.fn(function remove(this: { element: HTMLElement }) {
          this.element.remove()
        }),
        setLngLat: vi.fn().mockReturnThis(),
        addTo: vi.fn(function addTo(this: { element: HTMLElement }) {
          container?.append(this.element)
          return this
        })
      }
      markers.push(entry)
      return entry
    }
  })

  return { loader, mapInstance, markers }
}

describe('GarageMap hall markers', () => {
  it('renders direct garage anchors with occupancy and updates them without recreating map or refitting unchanged points', async () => {
    const stub = mapStub()
    const wrapper = mount(GarageMap, {
      props: { latitude: null, longitude: null, label: 'Hallit', garages: [garage()] },
      global: { provide: { [mapLibreLoaderKey]: stub.loader } }
    })
    await flushPromises()

    const anchor = wrapper.get('.garage-map-marker')
    expect(anchor.element.tagName).toBe('A')
    expect(anchor.attributes('href')).toBe('/garages/1')
    expect(anchor.text()).toContain('Keskustan halli')
    expect(anchor.text()).toContain('Torikatu 3')
    expect(anchor.text()).toContain('5/8 varattu')
    expect(anchor.text()).toContain('1 tuntematon')
    expect(stub.mapInstance.fitBounds).toHaveBeenCalledTimes(1)

    await wrapper.setProps({ garages: [garage({ occupiedLots: 6, freeLots: 1 })] })
    expect(wrapper.get('.garage-map-marker').text()).toContain('6/8 varattu')
    expect(stub.markers).toHaveLength(1)
    expect(stub.mapInstance.fitBounds).toHaveBeenCalledTimes(1)

    wrapper.unmount()
    expect(stub.markers[0]?.remove).toHaveBeenCalledOnce()
    expect(stub.mapInstance.remove).toHaveBeenCalledOnce()
  })

  it('fits only when the garage point set changes, omits missing locations, and accepts zero coordinates', async () => {
    const stub = mapStub()
    const wrapper = mount(GarageMap, {
      props: {
        latitude: null,
        longitude: null,
        label: 'Hallit',
        garages: [garage({ id: 1, latitude: 0, longitude: 0 }), garage({ id: 2, latitude: null, longitude: 25 })]
      },
      global: { provide: { [mapLibreLoaderKey]: stub.loader } }
    })
    await flushPromises()
    expect(wrapper.findAll('.garage-map-marker')).toHaveLength(1)
    expect(stub.markers[0]?.setLngLat).toHaveBeenCalledWith([0, 0])
    expect(stub.mapInstance.fitBounds).toHaveBeenCalledTimes(1)

    await wrapper.setProps({ garages: [garage({ id: 1, latitude: 0, longitude: 0 }), garage({ id: 2, latitude: 61, longitude: 26 })] })
    expect(stub.mapInstance.fitBounds).toHaveBeenCalledTimes(2)
    expect(wrapper.findAll('.garage-map-marker')).toHaveLength(2)

    await wrapper.setProps({ garages: [garage({ id: 1, latitude: 0, longitude: 0 }), garage({ id: 2, latitude: 61, longitude: 26, occupiedLots: 8 })] })
    expect(stub.mapInstance.fitBounds).toHaveBeenCalledTimes(2)
    wrapper.unmount()
  })

  it('retains legacy selectable-map behavior with no garage coordinates', async () => {
    const stub = mapStub()
    const clickHandlers = new Map<string, (event?: unknown) => void>()
    stub.mapInstance.on.mockImplementation((name: string, handler: (event?: unknown) => void) => clickHandlers.set(name, handler))
    const wrapper = mount(GarageMap, {
      props: { latitude: null, longitude: null, label: 'Uusi halli', selectable: true },
      global: { provide: { [mapLibreLoaderKey]: stub.loader } }
    })
    await flushPromises()

    expect(wrapper.find('.garage-map-marker').exists()).toBe(false)
    clickHandlers.get('click')?.({ lngLat: { lat: 62, lng: 26 } })
    expect(wrapper.emitted('select')?.[0]).toEqual([{ latitude: 62, longitude: 26 }])
    await wrapper.get('button').trigger('click')
    expect(wrapper.emitted('select')?.[1]).toEqual([{ latitude: 60, longitude: 24 }])
    wrapper.unmount()
  })
})
