export type MapLibreLoader = () => Promise<typeof import('maplibre-gl')>

export const mapLibreLoaderKey = Symbol('mapLibreLoader')

export async function loadMapLibre(): Promise<typeof import('maplibre-gl')> {
  const [library, worker] = await Promise.all([
    import('maplibre-gl'),
    import('maplibre-gl/dist/maplibre-gl-worker.mjs?worker&url')
  ])
  library.setWorkerUrl(worker.default)
  return library
}
