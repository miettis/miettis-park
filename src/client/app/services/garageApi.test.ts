import { afterEach, describe, expect, it, vi } from 'vitest'
import { HttpGarageApi } from './garageApi'

const effectiveOccurredAt = '2026-10-14T10:30:45Z'

function response(body: unknown, status = 200): Response {
  return { ok: status >= 200 && status < 300, status, json: async () => body } as Response
}

afterEach(() => vi.unstubAllGlobals())

describe('HttpGarageApi event time commands', () => {
  it('sends occurredAt for all four command payloads and passes additive response values through', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(response({ effectiveOccurredAt }))
      .mockResolvedValueOnce(response({ effectiveOccurredAt }))
      .mockResolvedValueOnce(response({ effectiveOccurredAt }))
      .mockResolvedValueOnce(response({ effectiveOccurredAt }))
    vi.stubGlobal('fetch', fetchMock)
    const api = new HttpGarageApi()

    const results = await Promise.all([
      api.enterGarage(7, 'ABC-123', '2026-10-14T13:30:45+03:00'),
      api.exitGarage(7, 'ABC-123', '2026-10-14T13:30:45+03:00'),
      api.enterLot(7, 12, 44, '2026-10-14T13:30:45+03:00'),
      api.exitLot(7, 12, '2026-10-14T13:30:45+03:00')
    ])
    expect(results.map(result => result.effectiveOccurredAt)).toEqual(Array(4).fill(effectiveOccurredAt))

    expect(fetchMock.mock.calls.map(([url, init]) => [url, init?.method, init?.body])).toEqual([
      ['/api/garages/7/entries', 'POST', JSON.stringify({ licenseNumber: 'ABC-123', occurredAt: '2026-10-14T13:30:45+03:00' })],
      ['/api/garages/7/exits', 'POST', JSON.stringify({ licenseNumber: 'ABC-123', occurredAt: '2026-10-14T13:30:45+03:00' })],
      ['/api/garages/7/lots/12/entries', 'POST', JSON.stringify({ visitId: 44, occurredAt: '2026-10-14T13:30:45+03:00' })],
      ['/api/garages/7/lots/12/exit', 'POST', JSON.stringify({ occurredAt: '2026-10-14T13:30:45+03:00' })]
    ])
  })

  it('keeps legacy lot exit without a request body when server time is selected', async () => {
    const fetchMock = vi.fn().mockResolvedValue(response({ effectiveOccurredAt }))
    vi.stubGlobal('fetch', fetchMock)

    await new HttpGarageApi().exitLot(7, 12, null)

    expect(fetchMock).toHaveBeenCalledWith('/api/garages/7/lots/12/exit', expect.objectContaining({ method: 'POST' }))
    expect(fetchMock.mock.calls[0]?.[1]).not.toHaveProperty('body')
  })

  it('retains problem details with minimumOccurredAt for time conflicts', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response({
      code: 'EVENT_TIME_CONFLICT',
      minimumOccurredAt: '2026-10-14T10:00:00Z',
      detail: 'Aika on liian aikainen.'
    }, 409)))

    await expect(new HttpGarageApi().enterGarage(7, 'ABC-123', null)).rejects.toMatchObject({
      status: 409,
      problem: {
        code: 'EVENT_TIME_CONFLICT',
        minimumOccurredAt: '2026-10-14T10:00:00Z'
      }
    })
  })

  it('retains a disabled-simulation 400 code from ProblemDetails', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response({ code: 'SIMULATION_TIME_DISABLED' }, 400)))

    await expect(new HttpGarageApi().exitGarage(7, 'ABC-123', null)).rejects.toMatchObject({
      status: 400,
      problem: { code: 'SIMULATION_TIME_DISABLED' }
    })
  })
})
