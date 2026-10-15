import { computed, inject, onMounted, onBeforeUnmount, ref, watch, type Ref } from 'vue'
import { createResync, garageEvents, useGarageEvents } from '../services/garageEvents'
import { garageApiKey, HttpGarageApi, type GarageApi } from '../services/garageApi'
import type { ActiveParkingDto, ActiveVisitDto, AvailabilityDto, EntryDto, GarageDto, GarageLayoutDto, ReceiptDto } from '../types/garage'

export type NoticeKind = 'success' | 'error' | 'info' | 'warning'
export interface DashboardNotice {
  kind: NoticeKind
  message: string
  code?: string
  gateOpened?: boolean
  requiresCustomerService?: boolean
  minimumOccurredAt?: string
}

export function useGarageDashboard(api: GarageApi = inject(garageApiKey, new HttpGarageApi()) as GarageApi, routeId?: Ref<number | null>) {
  const garages = ref<GarageDto[]>([])
  const garageId = routeId ?? ref<number | null>(null)
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

  function explainError(value: unknown, operation: 'gate' | 'lot' = 'gate'): DashboardNotice {
    const apiError = value as Error & { status?: number, problem?: { code?: string, gateOpened?: boolean, requiresCustomerService?: boolean, minimumOccurredAt?: string } }
    return {
      kind: 'error',
      message: apiError.problem?.code === 'VISIT_ALREADY_PARKED'
        ? 'Auto on jo pysäköitynä. Vapauta ensin sen nykyinen paikka ennen uuteen paikkaan ajamista. Paikkaan ajoa ei kirjattu.'
        : apiError.message || 'Toiminto epäonnistui. Päivitä tilanne ja yritä uudelleen.',
      code: apiError.problem?.code,
      gateOpened: operation === 'gate' ? apiError.problem?.gateOpened : undefined,
      requiresCustomerService: apiError.problem?.requiresCustomerService
        || apiError.problem?.code === 'ACTIVE_VISIT_NOT_FOUND',
      minimumOccurredAt: apiError.problem?.minimumOccurredAt
    }
  }

  const layout = ref<GarageLayoutDto | null>(null)
  const fresh = ref(false)
  let routeGeneration = 0
  const sync = createResync(async signal => {
    const id = garageId.value
    loading.value = true
    if (id === null || !Number.isSafeInteger(id) || id <= 0) throw new Error('Virheellinen hallitunniste.')
    const [nextGarages, nextLayout, nextVisits, nextParkings] = await Promise.all([
      api.getGarages(signal), api.getLayout(id, signal), api.getActiveVisits(id, signal), api.getActiveParkings(id, signal)
    ])
    if (!nextGarages.some(garage => garage.id === id)) throw new Error('Hallia ei löytynyt (404).')
    return () => {
      garages.value = nextGarages
      layout.value = nextLayout
      visits.value = nextVisits
      parkings.value = nextParkings
      const lots = nextLayout.lots
      const free = lots.filter(lot => lot.currentStatus === 'Free').length
      availability.value = { capacity: lots.length, activeVisits: nextVisits.length, freeLots: free,
        occupiedLots: lots.filter(lot => lot.currentStatus === 'Occupied').length,
        unknownLots: lots.filter(lot => lot.currentStatus === 'Unknown').length, isFull: free === 0, lots }
      initialized.value = true
      loading.value = false
      error.value = ''
      fresh.value = true
    }
  }, cause => { error.value = cause instanceof Error ? cause.message : 'Tietojen haku epäonnistui.'; loading.value = false; fresh.value = false })
  function refresh(): Promise<void> { fresh.value = false; return sync.refresh() }
  useGarageEvents(change => { if (!change || change.garageId === garageId.value || change.kind === 'garage') return refresh() })
  watch(garageId, () => {
    routeGeneration++
    garages.value = []; availability.value = null; layout.value = null; visits.value = []; parkings.value = []
    receipt.value = null; notice.value = null; initialized.value = false; error.value = ''
    void refresh()
  })
  onBeforeUnmount(() => { routeGeneration++; sync.dispose() })
  async function afterMutation(message: string, code?: string, kind: NoticeKind = 'success'): Promise<void> {
    notice.value = { kind, message, code }
    await refresh()
  }

  function formatEffectiveTime(value: string): string {
    return new Intl.DateTimeFormat('fi-FI', { dateStyle: 'short', timeStyle: 'medium' }).format(new Date(value))
  }

  function entryMessage(result: EntryDto): string {
    const effectiveTime = ` Tapahtuma-aika: ${formatEffectiveTime(result.effectiveOccurredAt)}.`
    if (result.outcome === 'Duplicate') {
      return `Aktiivinen käynti oli jo olemassa. Alkuperäinen alkamisaika: ${new Intl.DateTimeFormat('fi-FI', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(result.startAt))}. ${result.licenseNumber}${effectiveTime}`
    }
    if (result.outcome === 'RecoveredMissingExit') {
      const startedAt = new Intl.DateTimeFormat('fi-FI', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(result.startAt))
      return `Huomio: Aiempi käynti keskeytettiin. Uusi laskutus alkoi ${startedAt}. Paikka ei vapautunut. ${result.licenseNumber}${effectiveTime}`
    }
    return `Sisäänajo kirjattu. ${result.licenseNumber}.${effectiveTime}`
  }

  async function enterGarage(licenseNumber: string, occurredAt: string | null): Promise<void> {
    if (loading.value || submitting.value || garageId.value === null || !licenseNumber.trim()) {
      return
    }
    const commandRoute = routeGeneration
    const commandGarage = garageId.value
    submitting.value = true
    try {
      const result = await api.enterGarage(commandGarage, licenseNumber.trim(), occurredAt)
      if (commandRoute !== routeGeneration) return
      await afterMutation(entryMessage(result), result.outcome, result.outcome === 'RecoveredMissingExit' ? 'warning' : result.outcome === 'Duplicate' ? 'info' : 'success')
    } catch (cause) {
      if (commandRoute !== routeGeneration) return
      notice.value = explainError(cause)
      if ((cause as { status?: number }).status === 409) {
        await refresh()
      }
    } finally {
      submitting.value = false
    }
  }

  async function enterGarageFromImage(image: File, occurredAt: string | null): Promise<boolean> {
    if (loading.value || submitting.value || garageId.value === null || !image) return false
    const commandRoute = routeGeneration
    const commandGarage = garageId.value
    submitting.value = true
    try {
      const result = await api.enterGarageFromImage(commandGarage, image, occurredAt)
      if (commandRoute !== routeGeneration) return false
      await afterMutation(`${entryMessage(result)} Tunnistettu rekisteri: ${result.recognizedLicenseNumber}. VisitId: ${result.visitId}.`, result.outcome,
        result.outcome === 'RecoveredMissingExit' ? 'warning' : result.outcome === 'Duplicate' ? 'info' : 'success')
      return true
    } catch (cause) {
      if (commandRoute !== routeGeneration) return false
      notice.value = explainError(cause)
      if ((cause as { status?: number }).status === 409) await refresh()
      return false
    } finally {
      submitting.value = false
    }
  }

  async function exitGarage(licenseNumber: string, occurredAt: string | null): Promise<void> {
    if (loading.value || submitting.value || garageId.value === null || !licenseNumber.trim()) {
      return
    }
    receipt.value = null
    const commandRoute = routeGeneration
    const commandGarage = garageId.value
    submitting.value = true
    try {
      const nextReceipt = await api.exitGarage(commandGarage, licenseNumber.trim(), occurredAt)
      if (commandRoute !== routeGeneration) return
      receipt.value = nextReceipt
      notice.value = { kind: 'success', message: 'Ulosajo kirjattu. Kuitti perustuu palvelimen tietoihin.' }
      await refresh()
    } catch (cause) {
      if (commandRoute !== routeGeneration) return
      notice.value = explainError(cause)
      if ((cause as { status?: number }).status === 409) {
        await refresh()
      }
    } finally {
      submitting.value = false
    }
  }

  async function enterLot(lotId: number, visitId: number | null, occurredAt: string | null): Promise<void> {
    if (loading.value || submitting.value || garageId.value === null) {
      return
    }
    const commandRoute = routeGeneration
    const commandGarage = garageId.value
    submitting.value = true
    try {
      const result = await api.enterLot(commandGarage, lotId, visitId, occurredAt)
      if (commandRoute !== routeGeneration) return
      const effectiveTime = formatEffectiveTime(result.effectiveOccurredAt)
      await afterMutation(`Paikan käyttö kirjattu. Tapahtuma-aika: ${effectiveTime}.`, visitId === null ? 'Tuntematon auto tai käynti' : undefined)
    } catch (cause) {
      if (commandRoute !== routeGeneration) return
      notice.value = explainError(cause, 'lot')
      if ((cause as { status?: number }).status === 409) {
        await refresh()
      }
    } finally {
      submitting.value = false
    }
  }

  async function exitLot(lotId: number, occurredAt: string | null): Promise<void> {
    if (loading.value || submitting.value || garageId.value === null) {
      return
    }
    const commandRoute = routeGeneration
    const commandGarage = garageId.value
    submitting.value = true
    try {
      const result = await api.exitLot(commandGarage, lotId, occurredAt)
      if (commandRoute !== routeGeneration) return
      const message = result.missingParkingObservation
        ? 'Paikka vapautettiin; aktiivista pysäköintihavaintoa ei ollut.'
        : 'Paikan vapautuminen kirjattu.'
      await afterMutation(`${message} Tapahtuma-aika: ${formatEffectiveTime(result.effectiveOccurredAt)}.`)
    } catch (cause) {
      if (commandRoute !== routeGeneration) return
      notice.value = explainError(cause, 'lot')
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
    layout, fresh, connected: garageEvents.connected, garages,
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
    enterGarageFromImage,
    exitGarage,
    enterLot,
    exitLot
  }
}
