import { inject, onMounted, ref } from 'vue'
import { garageApiKey, HttpGarageApi, type GarageApi } from '../services/garageApi'
import type { ActiveParkingDto, ActiveVisitDto, AvailabilityDto, GarageDto } from '../types/garage'

type AdminApi = Pick<GarageApi, 'getGarages' | 'getAvailability' | 'getActiveVisits' | 'getActiveParkings'>

export function useAdminDashboard() {
  const api = inject<AdminApi>(garageApiKey, new HttpGarageApi())
  const garages = ref<GarageDto[]>([])
  const selectedGarage = ref<GarageDto | null>(null)
  const availability = ref<AvailabilityDto | null>(null)
  const parkings = ref<ActiveParkingDto[]>([])
  const visits = ref<ActiveVisitDto[]>([])
  const updatedAt = ref<Date | null>(null)
  const loading = ref(false)
  const initialized = ref(false)
  const error = ref('')

  async function refresh(): Promise<void> {
    if (loading.value) {
      return
    }
    loading.value = true
    error.value = ''
    try {
      if (!selectedGarage.value) garages.value = await api.getGarages()
      const garage = selectedGarage.value ?? garages.value[0] ?? null
      if (!garage) {
        selectedGarage.value = null
        availability.value = null
        return
      }
      const [nextAvailability, nextVisits, nextParkings] = await Promise.allSettled([
        api.getAvailability(garage.id),
        api.getActiveVisits(garage.id),
        api.getActiveParkings(garage.id)
      ])
      if (nextAvailability.status === 'rejected') {
        throw nextAvailability.reason
      }
      if (nextVisits.status === 'rejected') {
        throw nextVisits.reason
      }
      if (nextParkings.status === 'rejected') {
        throw nextParkings.reason
      }
      selectedGarage.value = garage
      availability.value = nextAvailability.value
      visits.value = nextVisits.value
      parkings.value = nextParkings.value
      updatedAt.value = new Date()
    }
    catch (failure) {
      error.value = failure instanceof Error ? failure.message : 'Tietojen haku epäonnistui.'
    }
    finally {
      initialized.value = true
      loading.value = false
    }
  }

  onMounted(refresh)
  return { garages, selectedGarage, availability, parkings, visits, updatedAt, loading, initialized, error, refresh }
}

export function timeLabel(value: string): string {
  const date = new Date(value)
  return Number.isNaN(date.getTime())
    ? 'Ei tiedossa'
    : new Intl.DateTimeFormat('fi-FI', { dateStyle: 'short', timeStyle: 'short' }).format(date)
}

export function elapsedLabel(startAt: string, updatedAt: Date | null): string {
  const start = new Date(startAt).getTime()
  if (!updatedAt || Number.isNaN(start)) {
    return 'Ei tiedossa'
  }
  const minutes = Math.floor(Math.max(0, updatedAt.getTime() - start) / 60000)
  if (minutes === 0) {
    return 'Alle minuutti'
  }
  const hours = Math.floor(minutes / 60)
  return hours > 0 ? `${hours} h ${minutes % 60} min` : `${minutes} min`
}
