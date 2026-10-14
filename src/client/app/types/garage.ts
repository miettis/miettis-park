export type LotStatus = 'Free' | 'Occupied' | 'Unknown'
export type EntryOutcome = 'Entry' | 'Duplicate' | 'RecoveredMissingExit'

export interface GarageDto {
  id: number
  name: string
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
}

export interface LotEntryDto {
  parkingId: number
  lotId: number
  visitId: number | null
  startAt: string
}

export interface LotExitDto {
  lotId: number
  parkingId: number | null
  missingParkingObservation: boolean
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
}

export interface ProblemDetailsDto {
  title?: string
  detail?: string
  code?: string
  gateOpened?: boolean
  requiresCustomerService?: boolean
}

export interface ApiError extends Error {
  status?: number
  problem?: ProblemDetailsDto
}
