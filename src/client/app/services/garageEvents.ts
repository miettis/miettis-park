import { ref, onMounted, onBeforeUnmount } from 'vue'

export interface GarageChange { garageId: number, kind: 'occupancy' | 'layout' | 'garage' }
type Listener = (change: GarageChange | null) => void | Promise<void>
export function createGarageEvents(factory: (url: string) => EventSource = url => new EventSource(url)) {
  const connected = ref(false)
  const listeners = new Set<Listener>()
  let source: EventSource | null = null
  function dispatch(change: GarageChange | null) {
    for (const listener of listeners) void listener(change)
  }
  function start() {
    if (source || typeof EventSource === 'undefined') return
    source = factory('/api/garages/events')
    source.onopen = () => { connected.value = true; dispatch(null) }
    source.onerror = () => { connected.value = false }
    source.addEventListener('garage-changed', event => {
      try {
        const change = JSON.parse((event as MessageEvent).data) as GarageChange
        if (Number.isSafeInteger(change.garageId) && change.garageId > 0 && ['occupancy', 'layout', 'garage'].includes(change.kind)) dispatch(change)
      } catch { /* Malformed frames do not alter application state. */ }
    })
  }
  return { connected, start, subscribe(listener: Listener) { listeners.add(listener); return () => listeners.delete(listener) },
    dispose() { source?.close(); source = null; connected.value = false; listeners.clear() } }
}
export const garageEvents = createGarageEvents()
if (import.meta.hot) import.meta.hot.dispose(() => garageEvents.dispose())

// Every invalidation changes the generation, including invalidations during a read.
export function createResync(read: (signal: AbortSignal) => Promise<() => void>, failed: (cause: unknown) => void) {
  const controller = new AbortController()
  let generation = 0
  let pending = false
  let running: Promise<void> | null = null
  let disposed = false
  async function drain() {
    while (pending && !disposed) {
      pending = false
      const current = generation
      try {
        const publish = await read(controller.signal)
        if (!disposed && generation === current) publish()
      } catch (cause) { if (!disposed && generation === current) failed(cause) }
    }
  }
  const scheduler = {
    refresh(): Promise<void> {
      if (disposed) return Promise.resolve()
      generation++; pending = true
      running ??= drain().finally(() => { running = null; if (pending && !disposed) return scheduler.refresh() })
      return running
    },
    dispose() { controller.abort(); disposed = true; generation++; pending = false }
  }
  return scheduler
}
export function useGarageEvents(listener: Listener) {
  const unsubscribe = garageEvents.subscribe(listener)
  onMounted(() => garageEvents.start())
  onBeforeUnmount(unsubscribe)
}
