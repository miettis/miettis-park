import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import GarageLayoutView from './GarageLayoutView.vue'
import { formatGarageOccupancy } from '../utils/occupancy'
import type { GarageDto, GarageLayoutDto } from '../types/garage'

const layout: GarageLayoutDto = {
  garageId: 4,
  width: 3,
  height: 2,
  version: 1,
  cells: [
    { x: 0, y: 0, type: 'ParkingLot', lotId: 10 },
    { x: 1, y: 0, type: 'Road', lotId: null },
    { x: 2, y: 0, type: 'ParkingLot', lotId: 12 },
    { x: 0, y: 1, type: 'Empty', lotId: null },
    { x: 1, y: 1, type: 'ParkingLot', lotId: 11 },
    { x: 2, y: 1, type: 'ParkingLot', lotId: 13 }
  ],
  lots: [
    { id: 10, number: 1, currentStatus: 'Free', canRemove: true },
    { id: 11, number: 2, currentStatus: 'Occupied', canRemove: true },
    { id: 12, number: 3, currentStatus: 'Unknown', canRemove: true },
    { id: 13, number: 4, currentStatus: 'Unknown', canRemove: true }
  ]
}

describe('GarageLayoutView', () => {
  it('renders numbered free, occupied and unknown lots with distinct road and empty cells', async () => {
    const wrapper = mount(GarageLayoutView, { props: { layout, selectedLotId: 11 } })
    const cells = wrapper.findAll('[role="gridcell"]')
    expect(cells).toHaveLength(6)
    expect(cells[0]?.text()).toContain('1')
    expect(cells[0]?.text()).toContain('Vapaa')
    expect(cells[0]?.classes()).toContain('status-Free')
    expect(cells[1]?.text()).toBe('')
    expect(cells[1]?.classes()).toContain('road-cell')
    expect(cells[2]?.text()).toContain('3')
    expect(cells[2]?.text()).toContain('Ei tiedossa')
    expect(cells[2]?.classes()).toContain('status-Unknown')
    expect(cells[3]?.text()).toBe('')
    expect(cells[3]?.classes()).toContain('empty-cell')
    expect(cells[4]?.classes()).toContain('status-Occupied')
    expect(cells[4]?.attributes('aria-pressed')).toBe('true')
    expect(cells[5]?.text()).toContain('4')

    await cells[0]?.trigger('click')
    expect(wrapper.emitted('select')).toEqual([[10]])
  })

  it('uses the shared garage occupancy formatter', () => {
    const garage: GarageDto = {
      id: 3,
      name: 'Laitoshalli',
      address: null,
      latitude: null,
      longitude: null,
      capacity: 10,
      freeLots: 4,
      occupiedLots: 3,
      unknownLots: 3,
      isFull: false
    }
    expect(formatGarageOccupancy(garage)).toBe('3/10 varattu · 3 tuntematon')
  })
})
