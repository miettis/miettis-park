import type {
  VisitHistoryDto,
  FeeTierDto,
  PricingDto,
  ActiveParkingDto,
  ActiveVisitDto,
  AvailabilityDto,
  EntryDto,
  ImageEntryDto,
  GarageDto,
  GarageInputDto,
  GarageLayoutDto,
  GarageLayoutInputDto,
  LotEntryDto,
  LotExitDto,
  ProblemDetailsDto,
  ReceiptDto
} from '../types/garage'

export interface GarageApi {
  getLayout(id: number, signal?: AbortSignal): Promise<GarageLayoutDto>
  getGarages(signal?: AbortSignal): Promise<GarageDto[]>
  getAvailability(garageId: number, signal?: AbortSignal): Promise<AvailabilityDto>
  getActiveVisits(garageId: number, signal?: AbortSignal): Promise<ActiveVisitDto[]>
  getActiveParkings(garageId: number, signal?: AbortSignal): Promise<ActiveParkingDto[]>
  enterGarage(garageId: number, licenseNumber: string, occurredAt: string | null): Promise<EntryDto>
  enterGarageFromImage(garageId: number, image: File, occurredAt: string | null): Promise<ImageEntryDto>
  exitGarage(garageId: number, licenseNumber: string, occurredAt: string | null): Promise<ReceiptDto>
  enterLot(garageId: number, lotId: number, visitId: number | null, occurredAt: string | null): Promise<LotEntryDto>
  exitLot(garageId: number, lotId: number, occurredAt: string | null): Promise<LotExitDto>
}

export interface GarageManagementApi extends Pick<GarageApi, 'getGarages'> {
  createGarage(input: GarageInputDto): Promise<GarageDto>
  updateGarage(id: number, input: GarageInputDto): Promise<GarageDto>
  getLayout(id: number, signal?: AbortSignal): Promise<GarageLayoutDto>
  saveLayout(id: number, input: GarageLayoutInputDto): Promise<GarageLayoutDto>
}

export const garageApiKey = Symbol('garageApi')

export class HttpGarageApi implements GarageApi, GarageManagementApi {
  private async request<T>(path: string, init?: RequestInit): Promise<T> {
    const response = await fetch(path, {
      ...init,
      headers: init?.body instanceof FormData ? init.headers : { 'Content-Type': 'application/json', ...init?.headers }
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

  getVisitHistory(id: number, skip = 0, signal?: AbortSignal): Promise<VisitHistoryDto[]> {
    return this.request(`/api/garages/${id}/visits?skip=${skip}&take=50`, { signal })
  }

  getPricing(id: number): Promise<PricingDto> {
    return this.request(`/api/garages/${id}/pricing`)
  }

  savePricing(id: number, tiers: FeeTierDto[], expectedTiers: FeeTierDto[]): Promise<PricingDto> {
    return this.request(`/api/garages/${id}/pricing`, { method: 'PUT', body: JSON.stringify({ tiers, expectedTiers }) })
  }

  getGarages(signal?: AbortSignal): Promise<GarageDto[]> {
    return this.request('/api/garages', { signal })
  }

  createGarage(input: GarageInputDto): Promise<GarageDto> {
    return this.request('/api/garages', { method: 'POST', body: JSON.stringify(input) })
  }

  updateGarage(id: number, input: GarageInputDto): Promise<GarageDto> {
    return this.request(`/api/garages/${id}`, { method: 'PUT', body: JSON.stringify(input) })
  }

  getLayout(id: number, signal?: AbortSignal): Promise<GarageLayoutDto> {
    return this.request(`/api/garages/${id}/layout`, { signal })
  }

  saveLayout(id: number, input: GarageLayoutInputDto): Promise<GarageLayoutDto> {
    return this.request(`/api/garages/${id}/layout`, { method: 'PUT', body: JSON.stringify(input) })
  }

  getAvailability(garageId: number, signal?: AbortSignal): Promise<AvailabilityDto> {
    return this.request(`/api/garages/${garageId}/availability`, { signal })
  }

  getActiveVisits(garageId: number, signal?: AbortSignal): Promise<ActiveVisitDto[]> {
    return this.request(`/api/garages/${garageId}/visits/active`, { signal })
  }

  getActiveParkings(garageId: number, signal?: AbortSignal): Promise<ActiveParkingDto[]> {
    return this.request(`/api/garages/${garageId}/parkings/active`, { signal })
  }

  enterGarage(garageId: number, licenseNumber: string, occurredAt: string | null): Promise<EntryDto> {
    return this.request(`/api/garages/${garageId}/entries`, {
      method: 'POST',
      body: JSON.stringify({ licenseNumber, occurredAt })
    })
  }

  enterGarageFromImage(garageId: number, image: File, occurredAt: string | null): Promise<ImageEntryDto> {
    const body = new FormData()
    body.append('image', image)
    if (occurredAt !== null) body.append('occurredAt', occurredAt)
    return this.request(`/api/garages/${garageId}/entries/from-image`, { method: 'POST', body })
  }

  exitGarage(garageId: number, licenseNumber: string, occurredAt: string | null): Promise<ReceiptDto> {
    return this.request(`/api/garages/${garageId}/exits`, {
      method: 'POST',
      body: JSON.stringify({ licenseNumber, occurredAt })
    })
  }

  enterLot(garageId: number, lotId: number, visitId: number | null, occurredAt: string | null): Promise<LotEntryDto> {
    return this.request(`/api/garages/${garageId}/lots/${lotId}/entries`, {
      method: 'POST',
      body: JSON.stringify({ visitId, occurredAt })
    })
  }

  exitLot(garageId: number, lotId: number, occurredAt: string | null): Promise<LotExitDto> {
    return this.request(`/api/garages/${garageId}/lots/${lotId}/exit`, {
      method: 'POST',
      ...(occurredAt === null ? {} : { body: JSON.stringify({ occurredAt }) })
    })
  }
}
