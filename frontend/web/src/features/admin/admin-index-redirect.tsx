import { Navigate } from 'react-router-dom'
import { useAuth } from '@/auth/auth-provider'

export function AdminIndexRedirect() {
  const { can } = useAuth()
  if (can('admin.users')) {
    return <Navigate to="users" replace />
  }
  if (can('admin.roles')) {
    return <Navigate to="roles" replace />
  }
  if (can('admin.lookups')) {
    return <Navigate to="lookups" replace />
  }
  if (can('admin.integrations')) {
    return <Navigate to="integrations" replace />
  }
  return <Navigate to="/unauthorized" replace />
}
