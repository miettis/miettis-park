import { mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import EventTimeInput from './EventTimeInput.vue'
import { toEventTime, toLocalInputValue } from '../utils/eventTime'

afterEach(() => vi.useRealTimers())

describe('EventTimeInput', () => {
  it('shows seconds precision, local zone and the selected offset', () => {
    const wrapper = mount(EventTimeInput, { props: { modelValue: '2026-10-14T13:30:45' } })
    const input = wrapper.get('input')

    expect(input.attributes('type')).toBe('datetime-local')
    expect(input.attributes('step')).toBe('1')
    expect(wrapper.text()).toContain(Intl.DateTimeFormat().resolvedOptions().timeZone)
    expect(wrapper.text()).toMatch(/UTC[+-]\d{2}:\d{2}/)
    expect(toEventTime('2026-10-14T13:30:45').occurredAt).toMatch(/^2026-10-14T13:30:45[+-]\d{2}:\d{2}$/)
  })

  it('quick actions update only the field and never submit a command', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date(2026, 9, 14, 10, 20, 30))
    const wrapper = mount(EventTimeInput, { props: { modelValue: '' } })
    const commands = vi.fn()

    await wrapper.get('button:nth-of-type(2)').trigger('click')
    expect(wrapper.emitted('update:modelValue')?.at(-1)?.[0]).toBe(toLocalInputValue(new Date(Date.now() + 10 * 60_000)))
    await wrapper.get('button:nth-of-type(3)').trigger('click')
    expect(wrapper.emitted('update:modelValue')?.at(-1)?.[0]).toBe(toLocalInputValue(new Date(Date.now() + 60 * 60_000)))
    await wrapper.get('button:nth-of-type(4)').trigger('click')
    expect(wrapper.emitted('update:modelValue')?.at(-1)?.[0]).toBe(toLocalInputValue(new Date(Date.now() + 180 * 60_000)))
    await wrapper.get('button:first-of-type').trigger('click')
    expect(wrapper.emitted('update:modelValue')?.at(-1)?.[0]).toBe('')
    expect(commands).not.toHaveBeenCalled()
  })

  it('increments the selected time rather than the browser clock', async () => {
    const wrapper = mount(EventTimeInput, { props: { modelValue: '2027-01-01T10:20:30' } })
    await wrapper.get('button:nth-of-type(2)').trigger('click')
    expect(wrapper.emitted('update:modelValue')?.at(-1)?.[0]).toBe('2027-01-01T10:30:30')
    await wrapper.get('button:nth-of-type(3)').trigger('click')
    expect(wrapper.emitted('update:modelValue')?.at(-1)?.[0]).toBe('2027-01-01T11:20:30')
    await wrapper.get('button:nth-of-type(4)').trigger('click')
    expect(wrapper.emitted('update:modelValue')?.at(-1)?.[0]).toBe('2027-01-01T13:20:30')
    expect(wrapper.text()).toContain('2027-01-01T10:20:30')
  })

  it('reports impossible calendar times and freezes its field and quick actions while submitting', async () => {
    expect(toEventTime('2026-10-14T10:20').occurredAt).toMatch(/^2026-10-14T10:20:00[+-]\d{2}:\d{2}$/)
    expect(toEventTime('2026-02-30T10:20:30').error).toBeTruthy()
    const invalidWrapper = mount(EventTimeInput, { props: { modelValue: '2026-02-30T10:20:30' } })
    expect(invalidWrapper.get('input').attributes('aria-invalid')).toBe('true')
    expect(invalidWrapper.get('[role="alert"]').text()).toContain('mahdollinen')
    const wrapper = mount(EventTimeInput, { props: { modelValue: '2026-10-14T10:20:30', disabled: true } })

    expect(wrapper.get('input').attributes('disabled')).toBeDefined()
    for (const button of wrapper.findAll('button')) {
      expect(button.attributes('disabled')).toBeDefined()
    }
  })
})
