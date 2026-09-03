import { Navigate, Route, Routes } from 'react-router-dom'
import { AppShell } from '@/components/layout/app-shell'
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
        <Route path="governance" element={<GovernanceHomePage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  )
}
