export interface EventTimeResult {
  occurredAt: string | null
  error: string | null
}

const LOCAL_DATE_TIME = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2})(?:\.(\d{1,3}))?)?$/

export function toEventTime(value: string): EventTimeResult {
  if (!value) {
    return { occurredAt: null, error: null }
  }

  const parts = LOCAL_DATE_TIME.exec(value)
  if (!parts) {
    return { occurredAt: null, error: 'Anna tapahtuma-aika sekunnin tarkkuudella.' }
  }

  const [year, month, day, hour, minute, second] = parts.slice(1, 7).map(part => Number(part ?? 0))
  const fractionalSeconds = parts[7]
  if (year === undefined || month === undefined || day === undefined || hour === undefined || minute === undefined || second === undefined || year < 1) {
    return { occurredAt: null, error: 'Tapahtuma-aika ei ole kelvollinen.' }
  }

  if (fractionalSeconds && /[1-9]/.test(fractionalSeconds)) {
    return { occurredAt: null, error: 'Anna tapahtuma-aika sekunnin tarkkuudella.' }
  }

  const date = new Date(0)
  date.setFullYear(year, month - 1, day)
  date.setHours(hour, minute, second, 0)
  if (date.getFullYear() !== year || date.getMonth() !== month - 1 || date.getDate() !== day
    || date.getHours() !== hour || date.getMinutes() !== minute || date.getSeconds() !== second) {
    return { occurredAt: null, error: 'Tämä paikallinen aika ei ole mahdollinen valitussa aikavyöhykkeessä.' }
  }

  const offsetMinutes = -date.getTimezoneOffset()
  const offsetSign = offsetMinutes >= 0 ? '+' : '-'
  const absoluteOffset = Math.abs(offsetMinutes)
  const offset = `${offsetSign}${String(Math.floor(absoluteOffset / 60)).padStart(2, '0')}:${String(absoluteOffset % 60).padStart(2, '0')}`
  const localValue = `${String(year).padStart(4, '0')}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}T${String(hour).padStart(2, '0')}:${String(minute).padStart(2, '0')}:${String(second).padStart(2, '0')}`

  return { occurredAt: `${localValue}${offset}`, error: null }
}

export function toLocalInputValue(date: Date): string {
  return `${String(date.getFullYear()).padStart(4, '0')}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}T${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}:${String(date.getSeconds()).padStart(2, '0')}`
}

export function formatUtcDateTime(value: string): string {
  return new Intl.DateTimeFormat('fi-FI', { dateStyle: 'short', timeStyle: 'medium' }).format(new Date(value))
}

export function formatOffset(date: Date): string {
  const offsetMinutes = -date.getTimezoneOffset()
  const sign = offsetMinutes >= 0 ? '+' : '-'
  const absoluteOffset = Math.abs(offsetMinutes)
  return `UTC${sign}${String(Math.floor(absoluteOffset / 60)).padStart(2, '0')}:${String(absoluteOffset % 60).padStart(2, '0')}`
}
