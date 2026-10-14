import { inject, onBeforeUnmount, onMounted, ref } from 'vue'
import { garageApiKey, HttpGarageApi } from '../services/garageApi'
import { createResync, garageEvents, useGarageEvents } from '../services/garageEvents'
import type { GarageDto } from '../types/garage'

export function useGarageList() {
  const api = inject(garageApiKey, new HttpGarageApi())
  const garages = ref<GarageDto[]>([])
  const loading = ref(false)
  const initialized = ref(false)
  const error = ref('')
  const fresh = ref(false)
  const sync = createResync(async signal => {
    loading.value = true
    const next = await api.getGarages(signal)
    return () => { garages.value = next; initialized.value = true; loading.value = false; error.value = ''; fresh.value = true }
  }, cause => { loading.value = false; error.value = cause instanceof Error ? cause.message : 'Tietojen haku epäonnistui.'; fresh.value = false })
  const refresh = () => { fresh.value = false; return sync.refresh() }
  useGarageEvents(() => refresh())
  onMounted(() => void refresh())
  onBeforeUnmount(() => sync.dispose())
  return { garages, loading, initialized, error, fresh, connected: garageEvents.connected, refresh }
}
