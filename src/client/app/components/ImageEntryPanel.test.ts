import { mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import ImageEntryPanel from './ImageEntryPanel.vue'

const validImage = () => new File(['image'], 'plate.jpg', { type: 'image/jpeg' })
beforeEach(() => vi.stubGlobal('URL', { createObjectURL: vi.fn(() => 'blob:image'), revokeObjectURL: vi.fn() }))
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })

function panel() { return mount(ImageEntryPanel, { props: { submitting: false, eventTimeValid: true, occurredAt: '2026-10-14T10:00:00Z' } }) }

describe('ImageEntryPanel', () => {
  it('native keyboard file input previews without sending, explicit submit and cleanup', async () => {
    const wrapper = panel()
    const input = wrapper.get('input[type=file]')
    expect(input.attributes('id')).toBe(wrapper.get('label').attributes('for'))
    await input.trigger('keydown', { key: 'Enter' })
    expect(wrapper.emitted('submit')).toBeUndefined()
    Object.defineProperty(input.element, 'files', { value: [validImage()], configurable: true })
    await input.trigger('change')
    expect(wrapper.emitted('submit')).toBeUndefined()
    expect(wrapper.get('img').attributes('src')).toBe('blob:image')
    expect(wrapper.text()).toContain('2026-10-14T10:00:00Z')
    expect(wrapper.text()).toContain('OpenRouter')
    await wrapper.get('button').trigger('click')
    expect(wrapper.emitted('submit')?.[0]?.[0]).toBeInstanceOf(File)
    await wrapper.get('.drop-area').trigger('drop', { dataTransfer: { files: [validImage()] } })
    expect(URL.revokeObjectURL).toHaveBeenCalledTimes(1)
    wrapper.unmount()
    expect(URL.revokeObjectURL).toHaveBeenCalledTimes(2)
  })

  it('drag feedback and drop share validation and never automatically send', async () => {
    const wrapper = panel()
    await wrapper.get('.drop-area').trigger('dragenter')
    expect(wrapper.get('.drop-area').classes()).toContain('drag-active')
    await wrapper.get('.drop-area').trigger('dragleave')
    expect(wrapper.get('.drop-area').classes()).not.toContain('drag-active')
    await wrapper.get('.drop-area').trigger('drop', { dataTransfer: { files: [validImage()] } })
    expect(wrapper.find('img').exists()).toBe(true)
    expect(wrapper.emitted('submit')).toBeUndefined()
    await wrapper.setProps({ submitting: true })
    expect((wrapper.get('input').element as HTMLInputElement).disabled).toBe(true)
    await wrapper.get('button').trigger('click')
    expect(wrapper.emitted('submit')).toBeUndefined()
    await wrapper.setProps({ submitting: false, eventTimeValid: false })
    expect((wrapper.get('button').element as HTMLButtonElement).disabled).toBe(true)
    wrapper.unmount()
  })

  it('only resets a selected image after an explicit successful-operation reset', async () => {
    const wrapper = panel()
    await wrapper.get('.drop-area').trigger('drop', { dataTransfer: { files: [validImage()] } })
    await wrapper.get('button').trigger('click')
    await wrapper.setProps({ submitting: true })
    await wrapper.setProps({ submitting: false })
    expect(wrapper.find('img').exists()).toBe(true)
    await wrapper.setProps({ resetKey: 1 })
    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.get('button').attributes('disabled')).toBeDefined()
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:image')
    wrapper.unmount()
  })

  it.each([
    [new File(['x'], 'bad.gif', { type: 'image/gif' })],
    [new File([], 'empty.png', { type: 'image/png' })],
    [new File([new Uint8Array(8 * 1024 * 1024 + 1)], 'big.png', { type: 'image/png' })],
    [validImage(), validImage()]
  ])('rejects invalid dropped files without sending', async (...files) => {
    const wrapper = panel()
    await wrapper.get('.drop-area').trigger('drop', { dataTransfer: { files } })
    expect(wrapper.get('[role=alert]').text()).toContain('yksi JPEG- tai PNG-kuva')
    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.emitted('submit')).toBeUndefined()
    wrapper.unmount()
  })
})