import type {
  ActiveParkingDto,
  ActiveVisitDto,
  AvailabilityDto,
  EntryDto,
  GarageDto,
  LotEntryDto,
  LotExitDto,
  ProblemDetailsDto,
  ReceiptDto
} from '../types/garage'

export interface GarageApi {
  getGarages(): Promise<GarageDto[]>
  getAvailability(garageId: number): Promise<AvailabilityDto>
  getActiveVisits(garageId: number): Promise<ActiveVisitDto[]>
  getActiveParkings(garageId: number): Promise<ActiveParkingDto[]>
  enterGarage(garageId: number, licenseNumber: string): Promise<EntryDto>
  exitGarage(garageId: number, licenseNumber: string): Promise<ReceiptDto>
  enterLot(garageId: number, lotId: number, visitId: number | null): Promise<LotEntryDto>
  exitLot(garageId: number, lotId: number): Promise<LotExitDto>
}

export const garageApiKey = Symbol('garageApi')

export class HttpGarageApi implements GarageApi {
  private async request<T>(path: string, init?: RequestInit): Promise<T> {
    const response = await fetch(path, {
      ...init,
      headers: { 'Content-Type': 'application/json', ...init?.headers }
    })

    if (!response.ok) {
      const problem = await response.json().catch(() => ({})) as ProblemDetailsDto
      const error = new Error(problem.detail || problem.title || 'Pyyntö epäonnistui.') as Error & {
        status: number
        problem: ProblemDetailsDto
      }
      error.status = response.status
      error.problem = problem
      throw error
    }

    if (response.status === 204) {
      return undefined as T
    }

    return await response.json() as T
  }

  getGarages(): Promise<GarageDto[]> {
    return this.request('/api/garages')
  }

  getAvailability(garageId: number): Promise<AvailabilityDto> {
    return this.request(`/api/garages/${garageId}/availability`)
  }

  getActiveVisits(garageId: number): Promise<ActiveVisitDto[]> {
    return this.request(`/api/garages/${garageId}/visits/active`)
  }

  getActiveParkings(garageId: number): Promise<ActiveParkingDto[]> {
    return this.request(`/api/garages/${garageId}/parkings/active`)
  }

  enterGarage(garageId: number, licenseNumber: string): Promise<EntryDto> {
    return this.request(`/api/garages/${garageId}/entries`, {
      method: 'POST',
      body: JSON.stringify({ licenseNumber })
    })
  }

  exitGarage(garageId: number, licenseNumber: string): Promise<ReceiptDto> {
    return this.request(`/api/garages/${garageId}/exits`, {
      method: 'POST',
      body: JSON.stringify({ licenseNumber })
    })
  }

  enterLot(garageId: number, lotId: number, visitId: number | null): Promise<LotEntryDto> {
    return this.request(`/api/garages/${garageId}/lots/${lotId}/entries`, {
      method: 'POST',
      body: JSON.stringify({ visitId })
    })
  }

  exitLot(garageId: number, lotId: number): Promise<LotExitDto> {
    return this.request(`/api/garages/${garageId}/lots/${lotId}/exit`, {
      method: 'POST',
      body: JSON.stringify({})
    })
  }
}
