import { computed, inject, onMounted, ref } from 'vue'
import { garageApiKey, HttpGarageApi, type GarageApi } from '../services/garageApi'
import type { ActiveParkingDto, ActiveVisitDto, AvailabilityDto, EntryDto, GarageDto, ReceiptDto } from '../types/garage'

export type NoticeKind = 'success' | 'error' | 'info' | 'warning'
export interface DashboardNotice {
  kind: NoticeKind
  message: string
  code?: string
  gateOpened?: boolean
  requiresCustomerService?: boolean
}

export function useGarageDashboard(api: GarageApi = inject(garageApiKey, new HttpGarageApi()) as GarageApi) {
  const garages = ref<GarageDto[]>([])
  const garageId = ref<number | null>(null)
  const availability = ref<AvailabilityDto | null>(null)
  const visits = ref<ActiveVisitDto[]>([])
  const parkings = ref<ActiveParkingDto[]>([])
  const receipt = ref<ReceiptDto | null>(null)
  const notice = ref<DashboardNotice | null>(null)
  const loading = ref(false)
  const submitting = ref(false)
  const initialized = ref(false)
  const error = ref('')
  const selectedGarage = computed(() => garages.value.find(garage => garage.id === garageId.value) ?? null)

  function explainError(value: unknown): DashboardNotice {
    const apiError = value as Error & { status?: number, problem?: { code?: string, gateOpened?: boolean, requiresCustomerService?: boolean } }
    return {
      kind: 'error',
      message: apiError.message || 'Toiminto epäonnistui. Päivitä tilanne ja yritä uudelleen.',
      code: apiError.problem?.code,
      gateOpened: apiError.problem?.gateOpened,
      requiresCustomerService: apiError.problem?.requiresCustomerService
        || apiError.problem?.code === 'ACTIVE_VISIT_NOT_FOUND',
    }
  }

  async function refresh(): Promise<void> {
    if (loading.value) {
      return
    }
    loading.value = true
    error.value = ''
    try {
      if (garages.value.length === 0) {
        garages.value = await api.getGarages()
      }
      if (garages.value.length === 0) {
        garageId.value = null
        availability.value = null
        visits.value = []
        parkings.value = []
        initialized.value = true
        return
      }
      garageId.value ??= garages.value[0]?.id ?? null
      if (garageId.value === null) {
        return
      }
      const [nextAvailability, nextVisits, nextParkings] = await Promise.all([
        api.getAvailability(garageId.value),
        api.getActiveVisits(garageId.value),
        api.getActiveParkings(garageId.value)
      ])
      availability.value = nextAvailability
      visits.value = nextVisits
      parkings.value = nextParkings
      initialized.value = true
    } catch (cause) {
      error.value = cause instanceof Error ? cause.message : 'Tietojen haku epäonnistui.'
    } finally {
      loading.value = false
    }
  }

  async function afterMutation(message: string, code?: string, kind: NoticeKind = 'success'): Promise<void> {
    notice.value = { kind, message, code }
    await refresh()
  }

  function entryMessage(result: EntryDto): string {
    if (result.outcome === 'Duplicate') {
      return `Aktiivinen käynti oli jo olemassa. Alkuperäinen alkamisaika: ${new Intl.DateTimeFormat('fi-FI', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(result.startAt))}. ${result.licenseNumber}`
    }
    if (result.outcome === 'RecoveredMissingExit') {
      const startedAt = new Intl.DateTimeFormat('fi-FI', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(result.startAt))
      return `Huomio: Aiempi käynti keskeytettiin. Uusi laskutus alkoi ${startedAt}. Paikka ei vapautunut. ${result.licenseNumber}`
    }
    return `Sisäänajo kirjattu. ${result.licenseNumber}`
  }

  async function enterGarage(licenseNumber: string): Promise<void> {
    if (loading.value || submitting.value || garageId.value === null || !licenseNumber.trim()) {
      return
    }
    submitting.value = true
    try {
      const result = await api.enterGarage(garageId.value, licenseNumber.trim())
      await afterMutation(entryMessage(result), result.outcome, result.outcome === 'RecoveredMissingExit' ? 'warning' : result.outcome === 'Duplicate' ? 'info' : 'success')
    } catch (cause) {
      notice.value = explainError(cause)
      if ((cause as { status?: number }).status === 409) {
        await refresh()
      }
    } finally {
      submitting.value = false
    }
  }

  async function exitGarage(licenseNumber: string): Promise<void> {
    if (loading.value || submitting.value || garageId.value === null || !licenseNumber.trim()) {
      return
    }
    receipt.value = null
    submitting.value = true
    try {
      receipt.value = await api.exitGarage(garageId.value, licenseNumber.trim())
      notice.value = { kind: 'success', message: 'Ulosajo kirjattu. Kuitti perustuu palvelimen tietoihin.' }
      await refresh()
    } catch (cause) {
      notice.value = explainError(cause)
      if ((cause as { status?: number }).status === 409) {
        await refresh()
      }
    } finally {
      submitting.value = false
    }
  }

  async function enterLot(lotId: number, visitId: number | null): Promise<void> {
    if (loading.value || submitting.value || garageId.value === null) {
      return
    }
    submitting.value = true
    try {
      await api.enterLot(garageId.value, lotId, visitId)
      await afterMutation('Paikan käyttö kirjattu.', visitId === null ? 'Tuntematon auto tai käynti' : undefined)
    } catch (cause) {
      notice.value = explainError(cause)
      if ((cause as { status?: number }).status === 409) {
        await refresh()
      }
    } finally {
      submitting.value = false
    }
  }

  async function exitLot(lotId: number): Promise<void> {
    if (loading.value || submitting.value || garageId.value === null) {
      return
    }
    submitting.value = true
    try {
      const result = await api.exitLot(garageId.value, lotId)
      await afterMutation(result.missingParkingObservation
        ? 'Paikka vapautettiin; aktiivista pysäköintihavaintoa ei ollut.'
        : 'Paikan vapautuminen kirjattu.')
    } catch (cause) {
      notice.value = explainError(cause)
      if ((cause as { status?: number }).status === 409) {
        await refresh()
      }
    } finally {
      submitting.value = false
    }
  }

  onMounted(() => {
    void refresh()
  })

  return {
    garages,
    garageId,
    selectedGarage,
    availability,
    visits,
    parkings,
    receipt,
    notice,
    loading,
    submitting,
    initialized,
    error,
    refresh,
    enterGarage,
    exitGarage,
    enterLot,
    exitLot
  }
}
