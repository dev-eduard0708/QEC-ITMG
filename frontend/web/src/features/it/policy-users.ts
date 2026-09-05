import { useCallback, useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import { adminApi } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import type { PickableUser } from '@/components/shared/user-picker'

/** Directory lookups need `admin.users`; without it the UI degrades to a short identifier. */
export function shortUserId(userId: string | null | undefined): string {
  if (!userId) return '—'
  return userId.slice(0, 8)
}

export function usePolicyUsers() {
  const { can, user } = useAuth()
  const canManage = can('policy.manage') || can('policy.approve')

  const query = useQuery({
    queryKey: ['admin', 'users', 'policy-workspace'],
    queryFn: () => adminApi.listUsers(),
    enabled: canManage,
    retry: false,
    staleTime: 60_000,
  })

  const activeUsers = useMemo<PickableUser[]>(
    () =>
      (query.data ?? [])
        .filter((item) => item.status === 'Active')
        .map((item) => ({ id: item.id, displayName: item.displayName, upn: item.upn })),
    [query.data],
  )

  const employeeUsers = useMemo<PickableUser[]>(
    () =>
      (query.data ?? [])
        .filter((item) => item.status === 'Active' && item.userType === 'Employee')
        .map((item) => ({ id: item.id, displayName: item.displayName, upn: item.upn })),
    [query.data],
  )

  const byId = useMemo(() => {
    const map = new Map<string, PickableUser>()
    for (const item of activeUsers) map.set(item.id, item)
    for (const item of query.data ?? []) {
      if (!map.has(item.id)) map.set(item.id, { id: item.id, displayName: item.displayName, upn: item.upn })
    }
    if (user && !map.has(user.id)) {
      map.set(user.id, { id: user.id, displayName: user.displayName, upn: user.upn })
    }
    return map
  }, [activeUsers, query.data, user])

  const nameFor = useCallback(
    (userId: string | null | undefined) => {
      if (!userId) return '—'
      return byId.get(userId)?.displayName ?? shortUserId(userId)
    },
    [byId],
  )

  return {
    activeUsers,
    employeeUsers,
    byId,
    nameFor,
    isDirectoryAvailable: !query.isError && activeUsers.length > 0,
    isLoading: query.isLoading,
  }
}
