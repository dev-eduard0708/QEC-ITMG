import { apiFetch } from '@/api/client'
import type { CurrentUser } from '@/auth/types'

export const meKeys = {
  all: ['me'] as const,
  session: () => [...meKeys.all, 'session'] as const,
}

export function fetchCurrentUser() {
  return apiFetch<CurrentUser>('/api/v1/me')
}

export function logoutSession() {
  return apiFetch<{ signedOut?: boolean }>('/auth/logout', { method: 'POST' })
}
