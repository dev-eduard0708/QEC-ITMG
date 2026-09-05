import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '@/auth/auth-provider'
import { Skeleton } from '@/components/ui/skeleton'

export function RequireAuth() {
  const { isAuthenticated, isLoading } = useAuth()
  const location = useLocation()

  if (isLoading) {
    return (
      <div className="flex min-h-svh items-center justify-center p-8">
        <Skeleton className="h-10 w-48" />
      </div>
    )
  }

  if (!isAuthenticated) {
    const authError = new URLSearchParams(location.search).get('authError')
    const loginSearch = authError ? `?authError=${encodeURIComponent(authError)}` : ''
    return (
      <Navigate
        to={`/login${loginSearch}`}
        replace
        state={{ from: `${location.pathname}${location.search}` }}
      />
    )
  }

  return <Outlet />
}

export function RequirePermission({ permission }: { permission: string }) {
  const { can, isLoading, isAuthenticated } = useAuth()
  const location = useLocation()

  if (isLoading) {
    return (
      <div className="p-8">
        <Skeleton className="h-8 w-40" />
      </div>
    )
  }

  if (!isAuthenticated) {
    const authError = new URLSearchParams(location.search).get('authError')
    const loginSearch = authError ? `?authError=${encodeURIComponent(authError)}` : ''
    return (
      <Navigate
        to={`/login${loginSearch}`}
        replace
        state={{ from: `${location.pathname}${location.search}` }}
      />
    )
  }

  if (!can(permission)) {
    return <Navigate to="/unauthorized" replace />
  }

  return <Outlet />
}

export function RequireAnyPermission({ permissions }: { permissions: string[] }) {
  const { can, isLoading, isAuthenticated } = useAuth()
  const location = useLocation()

  if (isLoading) {
    return (
      <div className="p-8">
        <Skeleton className="h-8 w-40" />
      </div>
    )
  }

  if (!isAuthenticated) {
    const authError = new URLSearchParams(location.search).get('authError')
    const loginSearch = authError ? `?authError=${encodeURIComponent(authError)}` : ''
    return (
      <Navigate
        to={`/login${loginSearch}`}
        replace
        state={{ from: `${location.pathname}${location.search}` }}
      />
    )
  }

  if (!permissions.some((permission) => can(permission))) {
    return <Navigate to="/unauthorized" replace />
  }

  return <Outlet />
}
