import { useCallback, useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import { adminApi } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import type { PickableUser } from '@/components/shared/user-picker'
import { shortUserId } from '@/features/it/policy-users'

/** Active-user directory for access pickers and friendly name resolution. */
export function useAccessUsers() {
  const { can, user } = useAuth()
  const canLoadDirectory =
    can('admin.users') || can('access.configure') || can('policy.manage') || can('policy.approve')

  const query = useQuery({
    queryKey: ['admin', 'users', 'access-workspace'],
    queryFn: () => adminApi.listUsers(),
    enabled: canLoadDirectory,
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

  const byId = useMemo(() => {
    const map = new Map<string, PickableUser>()
    for (const item of activeUsers) map.set(item.id, item)
    for (const item of query.data ?? []) {
      if (!map.has(item.id)) {
        map.set(item.id, { id: item.id, displayName: item.displayName, upn: item.upn })
      }
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
    byId,
    nameFor,
    isDirectoryAvailable: !query.isError && activeUsers.length > 0,
    isLoading: query.isLoading,
  }
}
