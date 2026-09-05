import { NavLink, Navigate, Outlet, useLocation, useNavigate } from 'react-router-dom'
import {
  BookOpen,
  Bot,
  Briefcase,
  Building2,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  ClipboardList,
  FileText,
  HardDrive,
  KeyRound,
  Languages,
  Laptop,
  LayoutDashboard,
  LifeBuoy,
  LogOut,
  Menu,
  Monitor,
  Moon,
  Network,
  PanelLeftClose,
  PanelLeftOpen,
  Radio,
  RefreshCw,
  Scale,
  Settings2,
  Shield,
  Sparkles,
  Sun,
  Ticket,
  Users,
  Wrench,
} from 'lucide-react'
import { useMemo, useState, type ComponentType, type SVGProps } from 'react'
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

type IconType = ComponentType<SVGProps<SVGSVGElement> & { className?: string }>

type NavItemDef = {
  to: string
  labelKey: string
  icon: IconType
  end?: boolean
  visible: boolean
}

type NavGroupDef = {
  id: string
  labelKey: string
  icon: IconType
  items: NavItemDef[]
}

const SIDEBAR_COLLAPSED_KEY = 'qec-itmg.sidebar.collapsed'
const SIDEBAR_GROUPS_KEY = 'qec-itmg.sidebar.groups'

const IT_PERMISSION_KEYS = [
  'tickets.read',
  'problems.read',
  'change.read',
  'event.read',
  'ops.read',
  'cmdb.read',
  'assets.read',
  'access.request',
  'doc.read',
  'policy.read',
  'kb.read',
  'gov.read',
  'control.read',
  'compliance.read',
  'evidence.read',
  'audit.read',
  'sec.dashboard',
  'bcm.read',
  'vendor.read',
  'report.executive',
  'report.servicedesk',
  'ai.use',
  'ai.admin',
  'remote.request',
  'remote.audit.read',
  'remote.attended',
  'remote.admin',
  'admin.users',
  'admin.roles',
  'admin.lookups',
  'admin.integrations',
] as const

export function hasMeaningfulItAccess(can: (permissionKey: string) => boolean): boolean {
  return IT_PERMISSION_KEYS.some((key) => can(key))
}

export function RootWorkspaceRedirect() {
  const { can, isLoading } = useAuth()
  if (isLoading) return null
  return <Navigate to={hasMeaningfulItAccess(can) ? '/it' : '/employee'} replace />
}

function readCollapsedPreference(): boolean {
  try {
    return localStorage.getItem(SIDEBAR_COLLAPSED_KEY) === '1'
  } catch {
    return false
  }
}

function writeCollapsedPreference(collapsed: boolean) {
  try {
    localStorage.setItem(SIDEBAR_COLLAPSED_KEY, collapsed ? '1' : '0')
  } catch {
    /* ignore */
  }
}

function readGroupState(): Record<string, boolean> {
  try {
    const raw = sessionStorage.getItem(SIDEBAR_GROUPS_KEY)
    if (!raw) return {}
    const parsed = JSON.parse(raw) as Record<string, boolean>
    return parsed && typeof parsed === 'object' ? parsed : {}
  } catch {
    return {}
  }
}

function writeGroupState(state: Record<string, boolean>) {
  try {
    sessionStorage.setItem(SIDEBAR_GROUPS_KEY, JSON.stringify(state))
  } catch {
    /* ignore */
  }
}

function pathMatches(pathname: string, to: string, end?: boolean): boolean {
  if (end) return pathname === to
  return pathname === to || pathname.startsWith(`${to}/`)
}

function buildNavGroups(can: (permissionKey: string) => boolean): NavGroupDef[] {
  return [
    {
      id: 'my-workspace',
      labelKey: 'nav.group.myWorkspace',
      icon: Briefcase,
      items: [
        { to: '/employee', labelKey: 'nav.home', icon: Users, end: true, visible: true },
        { to: '/employee/requests/new', labelKey: 'nav.getHelp', icon: LifeBuoy, end: true, visible: true },
        { to: '/employee/requests', labelKey: 'nav.requests', icon: Ticket, visible: true },
        { to: '/employee/equipment', labelKey: 'nav.equipment', icon: HardDrive, visible: true },
        { to: '/employee/knowledge', labelKey: 'nav.knowledge', icon: BookOpen, visible: true },
        { to: '/employee/policies', labelKey: 'nav.myPolicies', icon: FileText, visible: true },
        { to: '/employee/remote-support', labelKey: 'nav.remoteSupport', icon: Laptop, visible: true },
      ],
    },
    {
      id: 'it-operations',
      labelKey: 'nav.group.itOperations',
      icon: Building2,
      items: [
        { to: '/it', labelKey: 'nav.itDashboard', icon: LayoutDashboard, end: true, visible: true },
        { to: '/it/tickets', labelKey: 'nav.tickets', icon: Ticket, visible: can('tickets.read') },
        { to: '/it/problems', labelKey: 'nav.problems', icon: ClipboardList, visible: can('problems.read') },
        { to: '/it/changes', labelKey: 'nav.changes', icon: RefreshCw, visible: can('change.read') },
        { to: '/it/events', labelKey: 'nav.events', icon: Radio, visible: can('event.read') },
        { to: '/it/operations', labelKey: 'nav.operations', icon: Wrench, visible: can('ops.read') },
        { to: '/it/assets', labelKey: 'nav.assets', icon: Monitor, visible: can('assets.read') },
        { to: '/it/cmdb', labelKey: 'nav.cmdb', icon: Network, visible: can('cmdb.read') },
        { to: '/it/knowledge', labelKey: 'nav.knowledgeAdmin', icon: BookOpen, visible: can('kb.read') },
        {
          to: '/it/remote-support',
          labelKey: 'nav.remoteSupport',
          icon: Laptop,
          visible:
            can('remote.request') ||
            can('remote.audit.read') ||
            can('remote.attended') ||
            can('remote.admin'),
        },
      ],
    },
    {
      id: 'governance-risk',
      labelKey: 'nav.group.governanceRisk',
      icon: Scale,
      items: [
        { to: '/it/access', labelKey: 'nav.access', icon: KeyRound, visible: can('access.request') },
        {
          to: '/it/documents',
          labelKey: 'nav.documents',
          icon: FileText,
          visible: can('doc.read') || can('policy.read'),
        },
        {
          to: '/it/governance',
          labelKey: 'nav.governance',
          icon: Shield,
          visible: can('gov.read') || can('control.read'),
        },
        { to: '/it/controls', labelKey: 'nav.controls', icon: Shield, visible: can('control.read') },
        {
          to: '/it/compliance',
          labelKey: 'nav.compliance',
          icon: ClipboardList,
          visible: can('compliance.read'),
        },
        { to: '/it/evidence', labelKey: 'nav.evidence', icon: FileText, visible: can('evidence.read') },
        { to: '/it/audits', labelKey: 'nav.audits', icon: ClipboardList, visible: can('audit.read') },
        { to: '/it/security', labelKey: 'nav.security', icon: Shield, visible: can('sec.dashboard') },
        { to: '/it/continuity', labelKey: 'nav.continuity', icon: Shield, visible: can('bcm.read') },
        { to: '/it/vendors', labelKey: 'nav.vendors', icon: Building2, visible: can('vendor.read') },
      ],
    },
    {
      id: 'insights',
      labelKey: 'nav.group.insights',
      icon: Sparkles,
      items: [
        {
          to: '/it/reports',
          labelKey: 'nav.reports',
          icon: ClipboardList,
          visible:
            can('report.executive') ||
            can('report.servicedesk') ||
            can('report.incident') ||
            can('report.change') ||
            can('report.cmdb') ||
            can('report.security') ||
            can('report.compliance') ||
            can('report.audit') ||
            can('report.bcm') ||
            can('report.vendor'),
        },
        {
          to: '/it/ai',
          labelKey: 'nav.ai',
          icon: Bot,
          visible: can('ai.use') || can('ai.admin'),
        },
      ],
    },
    {
      id: 'administration',
      labelKey: 'nav.group.administration',
      icon: Settings2,
      items: [
        {
          to: '/it/admin',
          labelKey: 'nav.admin',
          icon: Settings2,
          visible:
            can('admin.users') ||
            can('admin.roles') ||
            can('admin.lookups') ||
            can('admin.integrations'),
        },
        {
          to: '/it/admin/integrations',
          labelKey: 'nav.integrations',
          icon: Network,
          visible: can('admin.integrations'),
        },
      ],
    },
  ]
}

function NavItemLink({
  item,
  collapsed,
  onNavigate,
  label,
}: {
  item: NavItemDef
  collapsed: boolean
  onNavigate?: () => void
  label: string
}) {
  const Icon = item.icon
  const location = useLocation()
  return (
    <NavLink
      to={item.to}
      end={item.end}
      onClick={onNavigate}
      title={collapsed ? label : undefined}
      aria-label={label}
      className={({ isActive }) => {
        const active =
          item.to === '/employee/requests'
            ? isActive && !location.pathname.startsWith('/employee/requests/new')
            : isActive
        return cn(
          'group/nav flex items-center gap-3 rounded-md text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
          collapsed ? 'justify-center px-2 py-2.5' : 'px-3 py-2',
          active
            ? 'bg-sidebar-accent text-white'
            : 'text-sidebar-muted hover:bg-white/10 hover:text-sidebar-foreground',
        )
      }}
    >
      <Icon className="h-4 w-4 shrink-0" aria-hidden />
      {!collapsed ? <span className="truncate">{label}</span> : null}
    </NavLink>
  )
}

function NavGroup({
  group,
  collapsed,
  open,
  onToggle,
  onNavigate,
}: {
  group: NavGroupDef & { items: NavItemDef[] }
  collapsed: boolean
  open: boolean
  onToggle: () => void
  onNavigate?: () => void
}) {
  const { t, i18n } = useTranslation()
  const GroupIcon = group.icon
  const visibleItems = group.items.filter((item) => item.visible)
  if (visibleItems.length === 0) return null

  const isRtl = i18n.dir() === 'rtl'
  const Chevron = open ? ChevronDown : isRtl ? ChevronLeft : ChevronRight

  if (collapsed) {
    return (
      <div className="space-y-1" role="group" aria-label={t(group.labelKey)}>
        {visibleItems.map((item) => (
          <NavItemLink
            key={item.to}
            item={item}
            collapsed
            onNavigate={onNavigate}
            label={t(item.labelKey)}
          />
        ))}
      </div>
    )
  }

  return (
    <div className="space-y-1">
      <button
        type="button"
        className="flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-start text-[0.7rem] font-semibold uppercase tracking-[0.12em] text-sidebar-muted transition-colors hover:bg-white/5 hover:text-sidebar-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        aria-expanded={open}
        onClick={onToggle}
      >
        <GroupIcon className="h-3.5 w-3.5 shrink-0 opacity-80" aria-hidden />
        <span className="min-w-0 flex-1 truncate">{t(group.labelKey)}</span>
        <Chevron
          className="h-3.5 w-3.5 shrink-0 transition-transform motion-reduce:transition-none"
          aria-hidden
        />
      </button>
      <div
        className={cn(
          'grid transition-[grid-template-rows] duration-200 ease-out motion-reduce:transition-none',
          open ? 'grid-rows-[1fr]' : 'grid-rows-[0fr]',
        )}
      >
        <div className="overflow-hidden">
          <div className="space-y-0.5 pb-1 ps-0.5">
            {visibleItems.map((item) => (
              <NavItemLink
                key={item.to}
                item={item}
                collapsed={false}
                onNavigate={onNavigate}
                label={t(item.labelKey)}
              />
            ))}
          </div>
        </div>
      </div>
    </div>
  )
}

function BrandBlock({ collapsed }: { collapsed: boolean }) {
  const { t } = useTranslation()

  if (collapsed) {
    return (
      <div className="flex items-center justify-center px-2 py-4" title={t('brand.name')}>
        <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-sidebar-accent/90 text-xs font-bold text-white">
          QEC
        </div>
      </div>
    )
  }

  return (
    <div className="px-4 py-4">
      <div className="text-[0.65rem] font-semibold uppercase tracking-[0.18em] text-sidebar-muted">
        {t('brand.organization')}
      </div>
      <div className="mt-1 text-lg font-semibold leading-tight text-sidebar-foreground">
        {t('brand.name')}
      </div>
      <p className="mt-1 text-xs leading-snug text-sidebar-muted">{t('brand.product')}</p>
    </div>
  )
}

function SidebarContent({
  collapsed,
  onToggleCollapsed,
  onNavigate,
  showCollapseControl,
}: {
  collapsed: boolean
  onToggleCollapsed?: () => void
  onNavigate?: () => void
  showCollapseControl?: boolean
}) {
  const { t } = useTranslation()
  const { can } = useAuth()
  const location = useLocation()
  const groups = useMemo(() => buildNavGroups(can), [can])

  const [groupOpen, setGroupOpen] = useState<Record<string, boolean>>(() => readGroupState())

  const effectiveOpen = useMemo(() => {
    const next: Record<string, boolean> = { ...groupOpen }
    for (const group of groups) {
      const visible = group.items.filter((item) => item.visible)
      if (visible.length === 0) continue
      const active = visible.some((item) => pathMatches(location.pathname, item.to, item.end))
      if (active) {
        next[group.id] = true
        continue
      }
      if (next[group.id] === undefined) {
        if (group.id === 'my-workspace' && location.pathname.startsWith('/employee')) {
          next[group.id] = true
        } else if (
          group.id === 'it-operations' &&
          (location.pathname === '/it' || location.pathname.startsWith('/it/'))
        ) {
          next[group.id] = true
        } else {
          next[group.id] = false
        }
      }
    }
    return next
  }, [groupOpen, groups, location.pathname])

  function toggleGroup(id: string) {
    setGroupOpen((prev) => {
      const currentlyOpen = effectiveOpen[id] ?? false
      const next = { ...prev, [id]: !currentlyOpen }
      writeGroupState(next)
      return next
    })
  }

  function isGroupOpen(id: string): boolean {
    return effectiveOpen[id] ?? false
  }

  return (
    <div className="flex h-full flex-col bg-sidebar text-sidebar-foreground">
      <BrandBlock collapsed={collapsed} />
      <Separator className="bg-white/10" />
      <nav
        className={cn('flex flex-1 flex-col gap-3 overflow-y-auto p-2', collapsed && 'px-1.5')}
        aria-label={t('nav.workspaces')}
      >
        {groups.map((group) => (
          <NavGroup
            key={group.id}
            group={group}
            collapsed={collapsed}
            open={isGroupOpen(group.id)}
            onToggle={() => toggleGroup(group.id)}
            onNavigate={onNavigate}
          />
        ))}
      </nav>

      <div className={cn('mt-auto space-y-2 border-t border-white/10 p-2', collapsed && 'px-1.5')}>
        {import.meta.env.DEV ? (
          <div className={cn('px-1', collapsed && 'flex justify-center')}>
            {collapsed ? (
              <Badge variant="outline" className="border-white/20 text-[0.65rem] text-sidebar-muted" title={t('shell.devOnly')}>
                Dev
              </Badge>
            ) : (
              <p className="px-1 text-[0.65rem] text-sidebar-muted">{t('shell.devOnly')}</p>
            )}
          </div>
        ) : null}
        {showCollapseControl && onToggleCollapsed ? (
          <Button
            type="button"
            variant="ghost"
            size={collapsed ? 'icon' : 'sm'}
            className={cn(
              'w-full text-sidebar-muted hover:bg-white/10 hover:text-sidebar-foreground',
              collapsed && 'mx-auto',
            )}
            onClick={onToggleCollapsed}
            aria-label={collapsed ? t('shell.expandSidebar') : t('shell.collapseSidebar')}
            title={collapsed ? t('shell.expandSidebar') : t('shell.collapseSidebar')}
          >
            {collapsed ? (
              <PanelLeftOpen className="h-4 w-4" />
            ) : (
              <>
                <PanelLeftClose className="h-4 w-4" />
                <span className="ms-2">{t('shell.collapseSidebar')}</span>
              </>
            )}
          </Button>
        ) : null}
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

  if (!user) return null

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
  // Specific routes before generic /it and /employee
  if (pathname.startsWith('/it/admin/integrations')) return t('nav.integrations')
  if (pathname.startsWith('/it/admin')) return t('nav.admin')
  if (pathname.startsWith('/it/assets')) return t('nav.assets')
  if (pathname.startsWith('/it/tickets')) return t('nav.tickets')
  if (pathname.startsWith('/it/problems')) return t('nav.problems')
  if (pathname.startsWith('/it/changes')) return t('nav.changes')
  if (pathname.startsWith('/it/events')) return t('nav.events')
  if (pathname.startsWith('/it/operations')) return t('nav.operations')
  if (pathname.startsWith('/it/access')) return t('nav.access')
  if (pathname.startsWith('/it/documents') || pathname.startsWith('/it/policies')) {
    return t('nav.documents')
  }
  if (pathname.startsWith('/it/knowledge')) return t('nav.knowledgeAdmin')
  if (pathname.startsWith('/it/cmdb')) return t('nav.cmdb')
  if (pathname.startsWith('/it/controls')) return t('nav.controls')
  if (pathname.startsWith('/it/compliance')) return t('nav.compliance')
  if (pathname.startsWith('/it/evidence')) return t('nav.evidence')
  if (pathname.startsWith('/it/audits')) return t('nav.audits')
  if (pathname.startsWith('/it/security')) return t('nav.security')
  if (pathname.startsWith('/it/continuity')) return t('nav.continuity')
  if (pathname.startsWith('/it/vendors')) return t('nav.vendors')
  if (pathname.startsWith('/it/reports')) return t('nav.reports')
  if (pathname.startsWith('/it/ai')) return t('nav.ai')
  if (pathname.startsWith('/it/remote-support')) return t('nav.remoteSupport')
  if (pathname.startsWith('/it/governance') || pathname.startsWith('/governance')) {
    return t('nav.governance')
  }
  if (pathname === '/it' || pathname === '/it/') return t('nav.itDashboard')

  if (pathname.startsWith('/employee/policies')) return t('nav.myPolicies')
  if (pathname.startsWith('/employee/remote-support')) return t('nav.remoteSupport')
  if (pathname.startsWith('/employee/knowledge')) return t('nav.knowledge')
  if (pathname.startsWith('/employee/requests/new')) return t('nav.getHelp')
  if (pathname.startsWith('/employee/requests')) return t('nav.requests')
  if (pathname.startsWith('/employee/equipment')) return t('nav.equipment')
  if (pathname === '/employee' || pathname === '/employee/') return t('nav.home')

  if (pathname.startsWith('/dev/foundation')) return t('nav.foundation')
  return t('brand.name')
}

export function AppShell() {
  const { t } = useTranslation()
  const location = useLocation()
  const [mobileOpen, setMobileOpen] = useState(false)
  const [collapsed, setCollapsed] = useState(readCollapsedPreference)

  function toggleCollapsed() {
    setCollapsed((prev) => {
      const next = !prev
      writeCollapsedPreference(next)
      return next
    })
  }

  return (
    <div className="flex min-h-svh bg-background">
      <aside
        className={cn(
          'hidden shrink-0 border-e border-border transition-[width] duration-200 ease-out motion-reduce:transition-none lg:block',
          collapsed ? 'w-[4.5rem]' : 'w-64',
        )}
      >
        <SidebarContent
          collapsed={collapsed}
          onToggleCollapsed={toggleCollapsed}
          showCollapseControl
        />
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-40 border-b border-border bg-card/95 backdrop-blur">
          <div className="flex h-14 items-center gap-3 px-4 sm:px-6">
            <Sheet open={mobileOpen} onOpenChange={setMobileOpen}>
              <SheetTrigger asChild>
                <Button
                  variant="outline"
                  size="icon"
                  className="lg:hidden"
                  aria-label={t('shell.openMenu')}
                >
                  <Menu className="h-4 w-4" />
                </Button>
              </SheetTrigger>
              <SheetContent className="w-[min(20rem,100vw)] p-0">
                <SidebarContent
                  collapsed={false}
                  onNavigate={() => setMobileOpen(false)}
                  showCollapseControl={false}
                />
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
