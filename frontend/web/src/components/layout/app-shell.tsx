import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import {
  BookOpen,
  Building2,
  HardDrive,
  Languages,
  LayoutDashboard,
  LogOut,
  Menu,
  Monitor,
  Moon,
  Network,
  Radio,
  RefreshCw,
  Settings2,
  Shield,
  Sun,
  Ticket,
  Users,
  Wrench,
  KeyRound,
  FileText,
} from 'lucide-react'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useAuth } from '@/auth/auth-provider'
import { isAppLanguage, type AppLanguage } from '@/i18n'
import { useTheme } from '@/app/theme-provider'
import type { ThemeOption } from '@/app/theme'
import { NotificationBell } from '@/components/layout/notification-bell'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Separator } from '@/components/ui/separator'
import { Sheet, SheetContent, SheetTrigger } from '@/components/ui/sheet'
import { cn } from '@/lib/utils'

type WorkspaceItem = {
  to: string
  labelKey: string
  icon: typeof LayoutDashboard
  end: boolean
  visible: boolean
}

function WorkspaceNav({ onNavigate }: { onNavigate?: () => void }) {
  const { t } = useTranslation()
  const { can } = useAuth()

  const workspaces = useMemo<WorkspaceItem[]>(
    () => [
      { to: '/', labelKey: 'nav.foundation', icon: LayoutDashboard, end: true, visible: true },
      { to: '/employee', labelKey: 'nav.employee', icon: Users, end: true, visible: true },
      {
        to: '/employee/equipment',
        labelKey: 'nav.equipment',
        icon: HardDrive,
        end: false,
        visible: true,
      },
      {
        to: '/employee/requests',
        labelKey: 'nav.requests',
        icon: Ticket,
        end: false,
        visible: true,
      },
      {
        to: '/employee/knowledge',
        labelKey: 'nav.knowledge',
        icon: BookOpen,
        end: false,
        visible: true,
      },
      {
        to: '/employee/policies',
        labelKey: 'nav.myPolicies',
        icon: FileText,
        end: false,
        visible: true,
      },
      { to: '/it', labelKey: 'nav.it', icon: Building2, end: true, visible: true },
      {
        to: '/it/tickets',
        labelKey: 'nav.tickets',
        icon: Ticket,
        end: false,
        visible: can('tickets.read'),
      },
      {
        to: '/it/problems',
        labelKey: 'nav.problems',
        icon: Shield,
        end: false,
        visible: can('problems.read'),
      },
      {
        to: '/it/changes',
        labelKey: 'nav.changes',
        icon: RefreshCw,
        end: false,
        visible: can('change.read'),
      },
      {
        to: '/it/events',
        labelKey: 'nav.events',
        icon: Radio,
        end: false,
        visible: can('event.read'),
      },
      {
        to: '/it/operations',
        labelKey: 'nav.operations',
        icon: Wrench,
        end: false,
        visible: can('ops.read'),
      },
      {
        to: '/it/access',
        labelKey: 'nav.access',
        icon: KeyRound,
        end: false,
        visible: can('access.request'),
      },
      {
        to: '/it/documents',
        labelKey: 'nav.documents',
        icon: FileText,
        end: false,
        visible: can('doc.read') || can('policy.read'),
      },
      {
        to: '/it/knowledge',
        labelKey: 'nav.knowledgeAdmin',
        icon: BookOpen,
        end: false,
        visible: can('kb.read'),
      },
      {
        to: '/it/assets',
        labelKey: 'nav.assets',
        icon: Monitor,
        end: false,
        visible: can('assets.read'),
      },
      {
        to: '/it/cmdb',
        labelKey: 'nav.cmdb',
        icon: Network,
        end: false,
        visible: can('cmdb.read'),
      },
      {
        to: '/it/admin',
        labelKey: 'nav.admin',
        icon: Settings2,
        end: false,
        visible: can('admin.users') || can('admin.roles') || can('admin.lookups'),
      },
      { to: '/governance', labelKey: 'nav.governance', icon: Shield, end: false, visible: true },
    ],
    [can],
  )

  return (
    <nav className="flex flex-1 flex-col gap-1 p-3" aria-label={t('nav.workspaces')}>
      {workspaces
        .filter((item) => item.visible)
        .map((item) => {
          const Icon = item.icon
          return (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.end}
              onClick={onNavigate}
              className={({ isActive }) =>
                cn(
                  'flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors',
                  isActive
                    ? 'bg-sidebar-accent text-white'
                    : 'text-sidebar-muted hover:bg-white/10 hover:text-sidebar-foreground',
                )
              }
            >
              <Icon className="h-4 w-4 shrink-0" />
              <span>{t(item.labelKey)}</span>
            </NavLink>
          )
        })}
    </nav>
  )
}

function BrandBlock() {
  const { t } = useTranslation()

  return (
    <div className="px-4 py-5">
      <div className="text-xs font-semibold uppercase tracking-[0.18em] text-sidebar-muted">
        {t('brand.organization')}
      </div>
      <div className="mt-1 text-lg font-semibold text-sidebar-foreground">{t('brand.name')}</div>
      <p className="mt-1 text-xs text-sidebar-muted">{t('brand.product')}</p>
    </div>
  )
}

function SidebarContent({ onNavigate }: { onNavigate?: () => void }) {
  const { t } = useTranslation()

  return (
    <div className="flex h-full flex-col bg-sidebar text-sidebar-foreground">
      <BrandBlock />
      <Separator className="bg-white/10" />
      <WorkspaceNav onNavigate={onNavigate} />
      <div className="mt-auto space-y-3 p-4">
        <Badge variant="warning">{t('status.development')}</Badge>
        <p className="text-xs leading-relaxed text-sidebar-muted">{t('shell.sidebarNote')}</p>
      </div>
    </div>
  )
}

function PreferenceControls() {
  const { t, i18n } = useTranslation()
  const { theme, setTheme } = useTheme()
  const language = (isAppLanguage(i18n.language) ? i18n.language : 'en') as AppLanguage

  return (
    <div className="flex items-center gap-2">
      <label className="sr-only" htmlFor="theme-select">
        {t('shell.theme')}
      </label>
      <div className="relative">
        <select
          id="theme-select"
          className="h-9 appearance-none rounded-md border border-input bg-background pe-8 ps-9 text-sm"
          value={theme}
          onChange={(event) => setTheme(event.target.value as ThemeOption)}
          aria-label={t('shell.theme')}
        >
          <option value="light">{t('shell.theme.light')}</option>
          <option value="dark">{t('shell.theme.dark')}</option>
          <option value="system">{t('shell.theme.system')}</option>
        </select>
        <span className="pointer-events-none absolute inset-y-0 start-2.5 flex items-center text-muted-foreground">
          {theme === 'dark' ? <Moon className="h-4 w-4" /> : null}
          {theme === 'light' ? <Sun className="h-4 w-4" /> : null}
          {theme === 'system' ? <Monitor className="h-4 w-4" /> : null}
        </span>
      </div>

      <label className="sr-only" htmlFor="language-select">
        {t('shell.language')}
      </label>
      <div className="relative">
        <select
          id="language-select"
          className="h-9 appearance-none rounded-md border border-input bg-background pe-8 ps-9 text-sm"
          value={language}
          onChange={(event) => {
            void i18n.changeLanguage(event.target.value)
          }}
          aria-label={t('shell.language')}
        >
          <option value="en">{t('shell.language.en')}</option>
          <option value="ar">{t('shell.language.ar')}</option>
        </select>
        <span className="pointer-events-none absolute inset-y-0 start-2.5 flex items-center text-muted-foreground">
          <Languages className="h-4 w-4" />
        </span>
      </div>
    </div>
  )
}

function UserSessionControls() {
  const { t } = useTranslation()
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const [busy, setBusy] = useState(false)

  if (!user) {
    return null
  }

  return (
    <div className="flex min-w-0 items-center gap-2">
      <div className="hidden min-w-0 text-end md:block">
        <div className="truncate text-sm font-medium">{user.displayName}</div>
        <div className="truncate text-xs text-muted-foreground">{user.upn}</div>
      </div>
      <Button
        variant="outline"
        size="sm"
        disabled={busy}
        onClick={() => {
          setBusy(true)
          void logout()
            .then(() => navigate('/login', { replace: true }))
            .finally(() => setBusy(false))
        }}
      >
        <LogOut className="h-4 w-4" />
        <span className="ms-1 hidden sm:inline">{t('shell.logout')}</span>
      </Button>
    </div>
  )
}

function workspaceTitle(pathname: string, t: (key: string) => string) {
  if (pathname.startsWith('/it/admin')) return t('nav.admin')
  if (pathname.startsWith('/it/assets')) return t('nav.assets')
  if (pathname.startsWith('/it/tickets')) return t('nav.tickets')
  if (pathname.startsWith('/it/problems')) return t('nav.problems')
  if (pathname.startsWith('/it/changes')) return t('nav.changes')
  if (pathname.startsWith('/it/events')) return t('nav.events')
  if (pathname.startsWith('/it/operations')) return t('nav.operations')
  if (pathname.startsWith('/it/access')) return t('nav.access')
  if (pathname.startsWith('/it/documents') || pathname.startsWith('/it/policies')) return t('nav.documents')
  if (pathname.startsWith('/it/knowledge')) return t('nav.knowledgeAdmin')
  if (pathname.startsWith('/it/cmdb')) return t('nav.cmdb')
  if (pathname.startsWith('/employee/policies')) return t('docs.myPoliciesTitle')
  if (pathname.startsWith('/employee/knowledge')) return t('nav.knowledge')
  if (pathname.startsWith('/employee/requests')) return t('nav.requests')
  if (pathname.startsWith('/employee/equipment')) return t('nav.equipment')
  if (pathname.startsWith('/employee')) return t('nav.employee')
  if (pathname.startsWith('/it')) return t('nav.it')
  if (pathname.startsWith('/governance')) return t('nav.governance')
  return t('nav.foundation')
}

export function AppShell() {
  const { t } = useTranslation()
  const location = useLocation()
  const [mobileOpen, setMobileOpen] = useState(false)

  return (
    <div className="flex min-h-svh bg-background">
      <aside className="hidden w-64 shrink-0 border-e border-border lg:block">
        <SidebarContent />
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-40 border-b border-border bg-card/95 backdrop-blur">
          <div className="flex h-14 items-center gap-3 px-4 sm:px-6">
            <Sheet open={mobileOpen} onOpenChange={setMobileOpen}>
              <SheetTrigger asChild>
                <Button variant="outline" size="icon" className="lg:hidden" aria-label={t('shell.openMenu')}>
                  <Menu className="h-4 w-4" />
                </Button>
              </SheetTrigger>
              <SheetContent>
                <SidebarContent onNavigate={() => setMobileOpen(false)} />
              </SheetContent>
            </Sheet>

            <div className="min-w-0 flex-1">
              <div className="truncate text-sm font-semibold text-foreground">{t('brand.name')}</div>
              <div className="truncate text-xs text-muted-foreground">
                {workspaceTitle(location.pathname, t)}
              </div>
            </div>

            <div className="hidden sm:block">
              <PreferenceControls />
            </div>
            <NotificationBell />
            <UserSessionControls />
            <Badge variant="secondary">{t('status.foundation')}</Badge>
          </div>
          <div className="border-t border-border px-4 py-2 sm:hidden">
            <PreferenceControls />
          </div>
        </header>

        <main className="flex-1 px-4 py-6 sm:px-6 lg:px-8">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
