import { Link, useLocation } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

type ComplianceNavTab = {
  to: string
  labelKey: string
  match: (pathname: string) => boolean
}

const tabs: ComplianceNavTab[] = [
  {
    to: '/it/compliance',
    labelKey: 'compliance.nav.overview',
    match: (path) => path === '/it/compliance',
  },
  {
    to: '/it/compliance/readiness',
    labelKey: 'compliance.nav.readiness',
    match: (path) => path.startsWith('/it/compliance/readiness'),
  },
  {
    to: '/it/compliance/frameworks',
    labelKey: 'compliance.nav.frameworks',
    match: (path) => path.startsWith('/it/compliance/frameworks'),
  },
  {
    to: '/it/compliance/mappings',
    labelKey: 'compliance.nav.mappings',
    match: (path) => path.startsWith('/it/compliance/mappings'),
  },
  {
    to: '/it/compliance/assessments',
    labelKey: 'compliance.nav.assessments',
    match: (path) => path.startsWith('/it/compliance/assessments'),
  },
  {
    to: '/it/compliance/calendar',
    labelKey: 'compliance.nav.calendar',
    match: (path) => path.startsWith('/it/compliance/calendar'),
  },
]

export function ComplianceReadinessNav({ className }: { className?: string }) {
  const { t } = useTranslation()
  const { pathname } = useLocation()

  return (
    <div className={cn('flex flex-wrap gap-2', className)}>
      {tabs.map((tab) => {
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
