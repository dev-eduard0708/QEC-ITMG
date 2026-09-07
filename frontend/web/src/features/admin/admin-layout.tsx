import { NavLink, Outlet } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useAuth } from '@/auth/auth-provider'
import { cn } from '@/lib/utils'

const adminLinks = [
  { to: '/it/admin/users', labelKey: 'admin.nav.users', permissions: ['admin.users'] },
  { to: '/it/admin/roles', labelKey: 'admin.nav.roles', permissions: ['admin.roles'] },
  { to: '/it/admin/lookups', labelKey: 'admin.nav.lookups', permissions: ['admin.lookups'] },
  {
    to: '/administration/hierarchy',
    labelKey: 'admin.nav.hierarchy',
    permissions: ['organization.hierarchy.read', 'organization.hierarchy.manage'],
  },
  {
    to: '/it/admin/integrations',
    labelKey: 'admin.nav.integrations',
    permissions: ['admin.integrations'],
  },
] as const

export function AdminLayout() {
  const { t } = useTranslation()
  const { can } = useAuth()

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-3 border-b border-border pb-4 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <p className="text-xs font-semibold uppercase tracking-[0.14em] text-muted-foreground">
            {t('admin.section')}
          </p>
          <h2 className="mt-1 text-xl font-semibold text-foreground">{t('admin.title')}</h2>
          <p className="mt-1 max-w-2xl text-sm text-muted-foreground">{t('admin.description')}</p>
        </div>
        <nav className="flex flex-wrap gap-1 rounded-md border border-border bg-card p-1" aria-label={t('admin.nav')}>
          {adminLinks
            .filter((link) => link.permissions.some((permission) => can(permission)))
            .map((link) => (
              <NavLink
                key={link.to}
                to={link.to}
                className={({ isActive }) =>
                  cn(
                    'rounded-sm px-3 py-1.5 text-sm font-medium transition-colors',
                    isActive
                      ? 'bg-primary text-primary-foreground'
                      : 'text-muted-foreground hover:bg-accent hover:text-accent-foreground',
                  )
                }
              >
                {t(link.labelKey)}
              </NavLink>
            ))}
        </nav>
      </div>
      <Outlet />
    </div>
  )
}
