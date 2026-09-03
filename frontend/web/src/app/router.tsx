import { Navigate, Route, Routes } from 'react-router-dom'
import { AppShell } from '@/components/layout/app-shell'
import { AdminLayout } from '@/features/admin/admin-layout'
import { AdminRolesPage } from '@/features/admin/roles-page'
import { AdminUsersPage } from '@/features/admin/users-page'
import { EmployeeHomePage } from '@/features/employee/employee-home-page'
import { GovernanceHomePage } from '@/features/governance/governance-home-page'
import { ItHomePage } from '@/features/it/it-home-page'
import { FoundationHomePage } from '@/features/foundation/foundation-home-page'

export function AppRouter() {
  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route index element={<FoundationHomePage />} />
        <Route path="employee" element={<EmployeeHomePage />} />
        <Route path="it" element={<ItHomePage />} />
        <Route path="it/admin" element={<AdminLayout />}>
          <Route index element={<Navigate to="users" replace />} />
          <Route path="users" element={<AdminUsersPage />} />
          <Route path="roles" element={<AdminRolesPage />} />
        </Route>
        <Route path="governance" element={<GovernanceHomePage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  )
}
