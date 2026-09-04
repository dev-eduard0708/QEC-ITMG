import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

export function GovernanceHomePage() {
  const { t } = useTranslation()
  const { can } = useAuth()

  return (
    <div className="space-y-6">
      <PageHeader title={t('governance.title')} description={t('governance.description')} />
      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
        {can('gov.read') ? (
          <>
            <Card>
              <CardHeader>
                <CardTitle>{t('governance.nav.organization')}</CardTitle>
                <CardDescription>{t('governance.organization.description')}</CardDescription>
              </CardHeader>
              <CardContent>
                <Button asChild variant="secondary">
                  <Link to="/it/governance/organization">{t('governance.nav.open')}</Link>
                </Button>
              </CardContent>
            </Card>
            <Card>
              <CardHeader>
                <CardTitle>{t('governance.nav.registers')}</CardTitle>
                <CardDescription>{t('governance.registers.description')}</CardDescription>
              </CardHeader>
              <CardContent>
                <Button asChild variant="secondary">
                  <Link to="/it/governance/registers">{t('governance.nav.open')}</Link>
                </Button>
              </CardContent>
            </Card>
          </>
        ) : null}
        {can('control.read') ? (
          <Card>
            <CardHeader>
              <CardTitle>{t('governance.nav.controls')}</CardTitle>
              <CardDescription>{t('controls.description')}</CardDescription>
            </CardHeader>
            <CardContent>
              <Button asChild>
                <Link to="/it/controls">{t('governance.nav.open')}</Link>
              </Button>
            </CardContent>
          </Card>
        ) : null}
      </div>
    </div>
  )
}
