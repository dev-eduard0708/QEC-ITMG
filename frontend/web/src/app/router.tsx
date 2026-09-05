import { Navigate, Route, Routes } from 'react-router-dom'
import { RequireAnyPermission, RequireAuth, RequirePermission } from '@/auth/route-guards'
import { AppShell } from '@/components/layout/app-shell'
import { AdminIndexRedirect } from '@/features/admin/admin-index-redirect'
import { AdminLayout } from '@/features/admin/admin-layout'
import { AdminLookupsPage } from '@/features/admin/lookups-page'
import { AdminRolesPage } from '@/features/admin/roles-page'
import { AdminUsersPage } from '@/features/admin/users-page'
import { IntegrationsAdminPage } from '@/features/admin/integrations-page'
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
import { OrganizationPage } from '@/features/governance/organization-page'
import { RegistersPage } from '@/features/governance/registers-page'
import { ControlsPage, ControlNewPage } from '@/features/governance/controls-page'
import { ControlDetailPage } from '@/features/governance/control-detail-page'
import { ComplianceHomePage } from '@/features/compliance/compliance-home-page'
import { FrameworksPage, FrameworkDetailPage } from '@/features/compliance/frameworks-page'
import { MappingsPage } from '@/features/compliance/mappings-page'
import { AssessmentsPage } from '@/features/compliance/assessments-page'
import { CalendarPage } from '@/features/compliance/calendar-page'
import { EvidencePage, EvidenceNewPage } from '@/features/it/evidence-page'
import { EvidenceDetailPage } from '@/features/it/evidence-detail-page'
import { AuditsPage, AuditNewPage } from '@/features/it/audits-page'
import { AuditDetailPage } from '@/features/it/audit-detail-page'
import { SecurityHomePage, VulnerabilityDetailPage } from '@/features/it/security-page'
import {
  ContinuityHomePage,
  BiaDetailPage,
  ContinuityPlanDetailPage,
  DrTestDetailPage,
} from '@/features/it/continuity-page'
import { VendorsPage, VendorNewPage, VendorDetailPage } from '@/features/it/vendors-page'
import { ReportsPage, ReportsExecutivePage } from '@/features/it/reports-page'
import { AiAssistantPage } from '@/features/it/ai-page'
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
import { AccessPage } from '@/features/it/access-page'
import { AccessNewPage } from '@/features/it/access-new-page'
import { AccessDetailPage } from '@/features/it/access-detail-page'
import { AccessReviewsPage } from '@/features/it/access-reviews-page'
import { AccessAccountsPage } from '@/features/it/access-accounts-page'
import { AccessSodPage } from '@/features/it/access-sod-page'
import { DocumentsPage } from '@/features/it/documents-page'
import { DocumentDetailPage } from '@/features/it/document-detail-page'
import { PoliciesPage } from '@/features/it/policies-page'
import { PolicyDetailPage } from '@/features/it/policy-detail-page'
import { MyPoliciesPage } from '@/features/employee/my-policies-page'
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
          <Route path="employee/policies" element={<MyPoliciesPage />} />
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
          <Route element={<RequirePermission permission="access.review" />}>
            <Route path="it/access/reviews" element={<AccessReviewsPage />} />
          </Route>
          <Route element={<RequirePermission permission="access.privileged.manage" />}>
            <Route path="it/access/accounts" element={<AccessAccountsPage />} />
          </Route>
          <Route element={<RequirePermission permission="sod.manage" />}>
            <Route path="it/access/sod" element={<AccessSodPage />} />
          </Route>
          <Route element={<RequirePermission permission="access.request" />}>
            <Route path="it/access" element={<AccessPage />} />
            <Route path="it/access/new" element={<AccessNewPage />} />
            <Route path="it/access/:id" element={<AccessDetailPage />} />
          </Route>
          <Route element={<RequirePermission permission="doc.read" />}>
            <Route path="it/documents" element={<DocumentsPage />} />
            <Route path="it/documents/:id" element={<DocumentDetailPage />} />
          </Route>
          <Route element={<RequirePermission permission="policy.read" />}>
            <Route path="it/policies" element={<PoliciesPage />} />
            <Route path="it/policies/:id" element={<PolicyDetailPage />} />
          </Route>
          <Route element={<RequirePermission permission="kb.read" />}>
            <Route path="it/knowledge" element={<ItKnowledgePage />} />
          </Route>
          <Route
            path="it/admin"
            element={
              <RequireAnyPermission
                permissions={['admin.users', 'admin.roles', 'admin.lookups', 'admin.integrations']}
              />
            }
          >
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
              <Route element={<RequirePermission permission="admin.integrations" />}>
                <Route path="integrations" element={<IntegrationsAdminPage />} />
              </Route>
            </Route>
          </Route>
          <Route element={<RequirePermission permission="gov.read" />}>
            <Route path="it/governance" element={<GovernanceHomePage />} />
            <Route path="it/governance/organization" element={<OrganizationPage />} />
            <Route path="it/governance/registers" element={<RegistersPage />} />
            <Route path="governance" element={<GovernanceHomePage />} />
          </Route>
          <Route element={<RequirePermission permission="control.read" />}>
            <Route path="it/controls" element={<ControlsPage />} />
            <Route path="it/controls/:id" element={<ControlDetailPage />} />
          </Route>
          <Route element={<RequirePermission permission="control.manage" />}>
            <Route path="it/controls/new" element={<ControlNewPage />} />
          </Route>
          <Route element={<RequirePermission permission="compliance.read" />}>
            <Route path="it/compliance" element={<ComplianceHomePage />} />
            <Route path="it/compliance/frameworks" element={<FrameworksPage />} />
            <Route path="it/compliance/frameworks/:id" element={<FrameworkDetailPage />} />
            <Route path="it/compliance/mappings" element={<MappingsPage />} />
            <Route path="it/compliance/assessments" element={<AssessmentsPage />} />
            <Route path="it/compliance/calendar" element={<CalendarPage />} />
          </Route>
          <Route element={<RequirePermission permission="evidence.read" />}>
            <Route path="it/evidence" element={<EvidencePage />} />
            <Route path="it/evidence/:id" element={<EvidenceDetailPage />} />
          </Route>
          <Route element={<RequirePermission permission="evidence.upload" />}>
            <Route path="it/evidence/new" element={<EvidenceNewPage />} />
          </Route>
          <Route element={<RequirePermission permission="audit.read" />}>
            <Route path="it/audits" element={<AuditsPage />} />
            <Route path="it/audits/:id" element={<AuditDetailPage />} />
          </Route>
          <Route element={<RequirePermission permission="audit.manage" />}>
            <Route path="it/audits/new" element={<AuditNewPage />} />
          </Route>
          <Route element={<RequirePermission permission="sec.dashboard" />}>
            <Route path="it/security" element={<SecurityHomePage />} />
          </Route>
          <Route element={<RequirePermission permission="vuln.read" />}>
            <Route path="it/security/vulnerabilities/:id" element={<VulnerabilityDetailPage />} />
          </Route>
          <Route element={<RequirePermission permission="bcm.read" />}>
            <Route path="it/continuity" element={<ContinuityHomePage />} />
            <Route path="it/continuity/bia" element={<ContinuityHomePage />} />
            <Route path="it/continuity/bia/:id" element={<BiaDetailPage />} />
            <Route path="it/continuity/plans" element={<ContinuityHomePage />} />
            <Route path="it/continuity/plans/:id" element={<ContinuityPlanDetailPage />} />
            <Route path="it/continuity/procedures" element={<ContinuityHomePage />} />
            <Route path="it/continuity/tests" element={<ContinuityHomePage />} />
            <Route path="it/continuity/tests/:id" element={<DrTestDetailPage />} />
          </Route>
          <Route element={<RequirePermission permission="vendor.manage" />}>
            <Route path="it/vendors/new" element={<VendorNewPage />} />
          </Route>
          <Route element={<RequirePermission permission="vendor.read" />}>
            <Route path="it/vendors" element={<VendorsPage />} />
            <Route path="it/vendors/:id" element={<VendorDetailPage />} />
          </Route>
          <Route
            element={
              <RequireAnyPermission
                permissions={[
                  'report.executive',
                  'report.servicedesk',
                  'report.incident',
                  'report.change',
                  'report.cmdb',
                  'report.security',
                  'report.compliance',
                  'report.audit',
                  'report.bcm',
                  'report.vendor',
                ]}
              />
            }
          >
            <Route path="it/reports" element={<ReportsPage />} />
            <Route path="it/reports/executive" element={<ReportsExecutivePage />} />
          </Route>
          <Route element={<RequireAnyPermission permissions={['ai.use', 'ai.admin']} />}>
            <Route path="it/ai" element={<AiAssistantPage />} />
          </Route>
          <Route path="unauthorized" element={<UnauthorizedPage />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Route>
      </Route>
    </Routes>
  )
}
