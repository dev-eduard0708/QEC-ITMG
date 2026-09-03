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
  return <Navigate to="/unauthorized" replace />
}
