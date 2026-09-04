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
import { KnowledgeArticlePage } from '@/features/employee/knowledge-article-page'
import { KnowledgePage } from '@/features/employee/knowledge-page'
import { MyEquipmentPage } from '@/features/employee/my-equipment-page'
import { MyRequestsPage } from '@/features/employee/my-requests-page'
import { NewRequestPage } from '@/features/employee/new-request-page'
import { RequestDetailPage } from '@/features/employee/request-detail-page'
import { GovernanceHomePage } from '@/features/governance/governance-home-page'
import { AssetDetailPage } from '@/features/it/asset-detail-page'
import { AssetsPage } from '@/features/it/assets-page'
import { CmdbPage } from '@/features/it/cmdb-page'
import { ItHomePage } from '@/features/it/it-home-page'
import { ItKnowledgePage } from '@/features/it/knowledge-page'
import { ProblemDetailPage } from '@/features/it/problem-detail-page'
import { ProblemsPage } from '@/features/it/problems-page'
import { ChangesPage } from '@/features/it/changes-page'
import { EventsPage } from '@/features/it/events-page'
import { EventDetailPage } from '@/features/it/event-detail-page'
import { OperationsPage } from '@/features/it/operations-page'
import { ChangeNewPage } from '@/features/it/change-new-page'
import { ChangeCatalogPage } from '@/features/it/change-catalog-page'
import { ChangeDetailPage } from '@/features/it/change-detail-page'
import { TicketDetailPage } from '@/features/it/ticket-detail-page'
import { TicketsPage } from '@/features/it/tickets-page'
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
          <Route path="employee/equipment" element={<MyEquipmentPage />} />
          <Route path="employee/requests" element={<MyRequestsPage />} />
          <Route path="employee/requests/new" element={<NewRequestPage />} />
          <Route path="employee/requests/:id" element={<RequestDetailPage />} />
          <Route path="employee/knowledge" element={<KnowledgePage />} />
          <Route path="employee/knowledge/:slug" element={<KnowledgeArticlePage />} />
          <Route path="it" element={<ItHomePage />} />
          <Route element={<RequirePermission permission="assets.read" />}>
            <Route path="it/assets" element={<AssetsPage />} />
            <Route path="it/assets/:id" element={<AssetDetailPage />} />
          </Route>
          <Route element={<RequirePermission permission="cmdb.read" />}>
            <Route path="it/cmdb" element={<CmdbPage />} />
          </Route>
          <Route element={<RequirePermission permission="tickets.read" />}>
            <Route path="it/tickets" element={<TicketsPage />} />
            <Route path="it/tickets/:id" element={<TicketDetailPage />} />
          </Route>
          <Route element={<RequirePermission permission="problems.read" />}>
            <Route path="it/problems" element={<ProblemsPage />} />
            <Route path="it/problems/:id" element={<ProblemDetailPage />} />
          </Route>
          <Route element={<RequirePermission permission="change.read" />}>
            <Route path="it/changes" element={<ChangesPage />} />
            <Route path="it/changes/catalog" element={<ChangeCatalogPage />} />
            <Route path="it/changes/new" element={<ChangeNewPage />} />
            <Route path="it/changes/:id" element={<ChangeDetailPage />} />
          </Route>
          <Route element={<RequirePermission permission="event.read" />}>
            <Route path="it/events" element={<EventsPage />} />
            <Route path="it/events/:id" element={<EventDetailPage />} />
          </Route>
          <Route element={<RequirePermission permission="ops.read" />}>
            <Route path="it/operations" element={<OperationsPage />} />
          </Route>
          <Route element={<RequirePermission permission="kb.read" />}>
            <Route path="it/knowledge" element={<ItKnowledgePage />} />
          </Route>
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
