export type ToastVariant = 'success' | 'error' | 'info'

export type ToastItem = {
  id: number
  message: string
  description?: string
  variant: ToastVariant
}

type ToastListener = () => void

const listeners = new Set<ToastListener>()
let toasts: ToastItem[] = []
let nextId = 1

function emit() {
  for (const listener of listeners) listener()
}

function push(message: string, variant: ToastVariant, description?: string) {
  const id = nextId++
  toasts = [...toasts, { id, message, description, variant }].slice(-4)
  emit()
  window.setTimeout(() => dismissToast(id), description ? 6500 : 4500)
  return id
}

export function dismissToast(id: number) {
  const next = toasts.filter((item) => item.id !== id)
  if (next.length === toasts.length) return
  toasts = next
  emit()
}

export function subscribeToasts(listener: ToastListener) {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}

export function getToasts() {
  return toasts
}

export const toast = {
  success: (message: string, description?: string) => push(message, 'success', description),
  error: (message: string, description?: string) => push(message, 'error', description),
  info: (message: string, description?: string) => push(message, 'info', description),
  dismiss: dismissToast,
}
