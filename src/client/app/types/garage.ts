export type LotStatus = 'Free' | 'Occupied' | 'Unknown'
export type LayoutCellType = 'Empty' | 'Road' | 'ParkingLot'
export type EntryOutcome = 'Entry' | 'Duplicate' | 'RecoveredMissingExit'

export interface GarageDto {
  id: number
  name: string
  address: string | null
  latitude: number | null
  longitude: number | null
  capacity: number
  freeLots: number
  occupiedLots: number
  unknownLots: number
  isFull: boolean
}

export interface GarageInputDto {
  name: string
  address: string | null
  latitude: number | null
  longitude: number | null
}

export interface LayoutCellDto {
  x: number
  y: number
  type: LayoutCellType
  lotId: number | null
}

export interface LayoutLotDto {
  id: number
  number: number
  currentStatus: LotStatus
  canRemove: boolean
}

export interface GarageLayoutDto {
  garageId: number
  width: number
  height: number
  version: number
  cells: LayoutCellDto[]
  lots: LayoutLotDto[]
}

export interface LayoutCellInputDto extends LayoutCellDto {
  number: number | null
}

export interface GarageLayoutInputDto {
  expectedVersion: number
  width: number
  height: number
  cells: LayoutCellInputDto[]
  removedLotIds: number[]
}

export interface LotDto {
  id: number
  number: number
  currentStatus: LotStatus
}

export interface AvailabilityDto {
  capacity: number
  activeVisits: number
  freeLots: number
  occupiedLots: number
  unknownLots: number
  isFull: boolean
  lots: LotDto[]
}

export interface ActiveVisitDto {
  visitId: number
  licenseNumber: string
  startAt: string
  lotId: number | null
  lotNumber: number | null
}

export interface ActiveParkingDto {
  parkingId: number
  lotId: number
  lotNumber: number
  visitId: number | null
  licenseNumber: string | null
  startAt: string
}

export interface EntryDto {
  visitId: number
  licenseNumber: string
  startAt: string
  outcome: EntryOutcome
  gateOpened: boolean
  effectiveOccurredAt: string
}

export interface LotEntryDto {
  parkingId: number
  lotId: number
  visitId: number | null
  startAt: string
  effectiveOccurredAt: string
}

export interface LotExitDto {
  lotId: number
  parkingId: number | null
  missingParkingObservation: boolean
  effectiveOccurredAt: string
}

export interface ReceiptDto {
  visitId: number
  licenseNumber: string
  startAt: string
  endAt: string
  closedAt: string
  duration: string
  cost: number
  currency: string
  gateOpened: boolean
  effectiveOccurredAt: string
}

export interface ProblemDetailsDto {
  title?: string
  detail?: string
  code?: string
  gateOpened?: boolean
  requiresCustomerService?: boolean
  minimumOccurredAt?: string
}

export interface ApiError extends Error {
  status?: number
  problem?: ProblemDetailsDto
}

export interface ImageEntryDto {
  visitId: number
  licenseNumber: string
  startAt: string
  outcome: EntryOutcome
  effectiveOccurredAt: string
  gateOpened: boolean
  recognizedLicenseNumber: string
}

export interface VisitHistoryDto {
  visitId: number
  licenseNumber: string
  startAt: string
  endAt: string | null
  closedAt: string | null
  status: 'Active' | 'Completed' | 'Interrupted'
  closureReason: string | null
  cost: number | null
}
export interface FeeTierDto {
  amount: number
  perUnit: string
  startDuration: string
  endDuration: string | null
}
export interface PricingDto { garageId: number, tiers: FeeTierDto[] }
