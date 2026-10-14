import type { GarageDto } from '../types/garage'

export function formatGarageOccupancy(garage: Pick<GarageDto, 'occupiedLots' | 'capacity' | 'unknownLots'>): string {
  return `${garage.occupiedLots}/${garage.capacity} varattu${garage.unknownLots ? ` · ${garage.unknownLots} tuntematon` : ''}`
}
