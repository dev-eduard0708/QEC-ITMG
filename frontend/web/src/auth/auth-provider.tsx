import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  type ReactNode,
} from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ApiError } from '@/api/client'
import { fetchCurrentUser, logoutSession, meKeys } from '@/auth/api'
import type { CurrentUser } from '@/auth/types'

type AuthContextValue = {
  user: CurrentUser | null
  isAuthenticated: boolean
  isLoading: boolean
  can: (permissionKey: string) => boolean
  refresh: () => Promise<void>
  logout: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

type AuthProviderProps = {
  children: ReactNode
}

export function AuthProvider({ children }: AuthProviderProps) {
  const queryClient = useQueryClient()
  const query = useQuery({
    queryKey: meKeys.session(),
    queryFn: async () => {
      try {
        return await fetchCurrentUser()
      } catch (error) {
        if (error instanceof ApiError && (error.status === 401 || error.status === 403)) {
          return null
        }
        throw error
      }
    },
    staleTime: 30_000,
    retry: false,
  })

  const user = query.data ?? null

  const can = useCallback(
    (permissionKey: string) => {
      if (!user) return false
      return user.permissions.includes(permissionKey)
    },
    [user],
  )

  const refresh = useCallback(async () => {
    await queryClient.invalidateQueries({ queryKey: meKeys.session() })
  }, [queryClient])

  const logout = useCallback(async () => {
    try {
      await logoutSession()
    } finally {
      queryClient.setQueryData(meKeys.session(), null)
      await queryClient.invalidateQueries({ queryKey: meKeys.session() })
    }
  }, [queryClient])

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isAuthenticated: user !== null,
      isLoading: query.isLoading || query.isFetching,
      can,
      refresh,
      logout,
    }),
    [user, query.isLoading, query.isFetching, can, refresh, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used within AuthProvider')
  }
  return context
}
