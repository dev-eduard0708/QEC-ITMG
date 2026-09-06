import { Link, useLocation } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useAuth } from '@/auth/auth-provider'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

type AccessNavTab = {
  to: string
  labelKey: string
  match: (pathname: string) => boolean
  visible: boolean
}

export function AccessNavTabs({ className }: { className?: string }) {
  const { t } = useTranslation()
  const { can } = useAuth()
  const { pathname } = useLocation()

  const tabs: AccessNavTab[] = [
    {
      to: '/it/access',
      labelKey: 'access.nav.cases',
      match: (path) =>
        path === '/it/access' ||
        path === '/it/access/new' ||
        (/^\/it\/access\/[^/]+$/.test(path) &&
          !['reviews', 'accounts', 'sod', 'configuration', 'new'].includes(path.split('/')[3] ?? '')),
      visible: can('access.request'),
    },
    {
      to: '/it/access/configuration',
      labelKey: 'access.nav.categories',
      match: (path) => path.startsWith('/it/access/configuration'),
      visible: can('access.configure'),
    },
    {
      to: '/it/access/reviews',
      labelKey: 'access.nav.reviews',
      match: (path) => path.startsWith('/it/access/reviews'),
      visible: can('access.review'),
    },
    {
      to: '/it/access/accounts',
      labelKey: 'access.nav.accounts',
      match: (path) => path.startsWith('/it/access/accounts'),
      visible: can('access.privileged.manage'),
    },
    {
      to: '/it/access/sod',
      labelKey: 'access.nav.sod',
      match: (path) => path.startsWith('/it/access/sod'),
      visible: can('sod.manage'),
    },
  ]

  const visible = tabs.filter((tab) => tab.visible)
  if (visible.length <= 1) return null

  return (
    <div className={cn('flex flex-wrap gap-2', className)}>
      {visible.map((tab) => {
        const active = tab.match(pathname)
        return (
          <Button key={tab.to} asChild size="sm" variant={active ? 'default' : 'secondary'}>
            <Link to={tab.to} aria-current={active ? 'page' : undefined}>
              {t(tab.labelKey)}
            </Link>
          </Button>
        )
      })}
    </div>
  )
}
