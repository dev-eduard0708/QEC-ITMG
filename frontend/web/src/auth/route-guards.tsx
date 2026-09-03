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
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
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
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
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
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  if (!permissions.some((permission) => can(permission))) {
    return <Navigate to="/unauthorized" replace />
  }

  return <Outlet />
}
