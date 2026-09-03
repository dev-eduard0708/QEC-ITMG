import { NavLink, Outlet, useLocation } from 'react-router-dom'
import {
  Building2,
  LayoutDashboard,
  Menu,
  Shield,
  Users,
} from 'lucide-react'
import { useState } from 'react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Separator } from '@/components/ui/separator'
import { Sheet, SheetContent, SheetTrigger } from '@/components/ui/sheet'
import { t } from '@/i18n'
import { cn } from '@/lib/utils'

const workspaces = [
  { to: '/', labelKey: 'nav.foundation', icon: LayoutDashboard, end: true },
  { to: '/employee', labelKey: 'nav.employee', icon: Users, end: false },
  { to: '/it', labelKey: 'nav.it', icon: Building2, end: false },
  { to: '/governance', labelKey: 'nav.governance', icon: Shield, end: false },
] as const

function WorkspaceNav({ onNavigate }: { onNavigate?: () => void }) {
  return (
    <nav className="flex flex-1 flex-col gap-1 p-3" aria-label={t('nav.workspaces')}>
      {workspaces.map((item) => {
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

function workspaceTitle(pathname: string) {
  if (pathname.startsWith('/employee')) return t('nav.employee')
  if (pathname.startsWith('/it')) return t('nav.it')
  if (pathname.startsWith('/governance')) return t('nav.governance')
  return t('nav.foundation')
}

export function AppShell() {
  const location = useLocation()
  const [mobileOpen, setMobileOpen] = useState(false)

  return (
    <div className="flex min-h-svh bg-background">
      <aside className="hidden w-64 shrink-0 border-r border-border lg:block">
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
                {workspaceTitle(location.pathname)}
              </div>
            </div>

            <Badge variant="secondary">{t('status.foundation')}</Badge>
          </div>
        </header>

        <main className="flex-1 px-4 py-6 sm:px-6 lg:px-8">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
