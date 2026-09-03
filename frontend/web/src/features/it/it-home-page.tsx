import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { PageHeader } from '@/components/page-header'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

export function ItHomePage() {
  const { t } = useTranslation()

  return (
    <div>
      <PageHeader title={t('it.title')} description={t('it.description')} />
      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>{t('nav.it')}</CardTitle>
            <CardDescription>{t('placeholder.note')}</CardDescription>
          </CardHeader>
          <CardContent className="text-sm text-muted-foreground">{t('it.planned')}</CardContent>
        </Card>
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
      </div>
    </div>
  )
}
