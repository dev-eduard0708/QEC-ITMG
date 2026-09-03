import { Navigate, Route, Routes } from 'react-router-dom'
import { RequireAnyPermission, RequireAuth, RequirePermission } from '@/auth/route-guards'
import { AppShell } from '@/components/layout/app-shell'
import { AdminIndexRedirect } from '@/features/admin/admin-index-redirect'
import { AdminLayout } from '@/features/admin/admin-layout'
import { AdminLookupsPage } from '@/features/admin/lookups-page'
import { AdminRolesPage } from '@/features/admin/roles-page'
import { AdminUsersPage } from '@/features/admin/users-page'
import { BreakGlassPage } from '@/features/auth/break-glass-page'
import { LoginPage } from '@/features/auth/login-page'
import { UnauthorizedPage } from '@/features/auth/unauthorized-page'
import { EmployeeHomePage } from '@/features/employee/employee-home-page'
import { GovernanceHomePage } from '@/features/governance/governance-home-page'
import { ItHomePage } from '@/features/it/it-home-page'
import { FoundationHomePage } from '@/features/foundation/foundation-home-page'

export function AppRouter() {
  return (
    <Routes>
      <Route path="login" element={<LoginPage />} />
      <Route path="break-glass" element={<BreakGlassPage />} />

      <Route element={<RequireAuth />}>
        <Route element={<AppShell />}>
          <Route index element={<FoundationHomePage />} />
          <Route path="employee" element={<EmployeeHomePage />} />
          <Route path="it" element={<ItHomePage />} />
          <Route path="it/admin" element={<RequireAnyPermission permissions={['admin.users', 'admin.roles', 'admin.lookups']} />}>
            <Route element={<AdminLayout />}>
              <Route index element={<AdminIndexRedirect />} />
              <Route element={<RequirePermission permission="admin.users" />}>
                <Route path="users" element={<AdminUsersPage />} />
              </Route>
              <Route element={<RequirePermission permission="admin.roles" />}>
                <Route path="roles" element={<AdminRolesPage />} />
              </Route>
              <Route element={<RequirePermission permission="admin.lookups" />}>
                <Route path="lookups" element={<AdminLookupsPage />} />
              </Route>
            </Route>
          </Route>
          <Route path="governance" element={<GovernanceHomePage />} />
          <Route path="unauthorized" element={<UnauthorizedPage />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Route>
      </Route>
    </Routes>
  )
}
