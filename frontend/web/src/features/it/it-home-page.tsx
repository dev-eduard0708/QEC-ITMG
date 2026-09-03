import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

export function ItHomePage() {
  const { t } = useTranslation()
  const { can } = useAuth()

  return (
    <div>
      <PageHeader title={t('it.title')} description={t('it.description')} />
      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>{t('nav.it')}</CardTitle>
            <CardDescription>{t('it.homeHint')}</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-2">
            {can('assets.read') ? (
              <Button asChild>
                <Link to="/it/assets">{t('nav.assets')}</Link>
              </Button>
            ) : null}
            {can('cmdb.read') ? (
              <Button asChild variant="secondary">
                <Link to="/it/cmdb">{t('nav.cmdb')}</Link>
              </Button>
            ) : null}
          </CardContent>
        </Card>
        {(can('admin.users') || can('admin.roles') || can('admin.lookups')) ? (
          <Card>
            <CardHeader>
              <CardTitle>{t('admin.title')}</CardTitle>
              <CardDescription>{t('admin.description')}</CardDescription>
            </CardHeader>
            <CardContent>
              <Button asChild>
                <Link to="/it/admin/users">{t('admin.nav.users')}</Link>
              </Button>
            </CardContent>
          </Card>
        ) : null}
      </div>
    </div>
  )
}
