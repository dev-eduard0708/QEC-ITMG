import { PageHeader } from '@/components/page-header'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { t } from '@/i18n'

export function ItHomePage() {
  return (
    <div>
      <PageHeader title={t('it.title')} description={t('it.description')} />
      <Card>
        <CardHeader>
          <CardTitle>{t('nav.it')}</CardTitle>
          <CardDescription>{t('placeholder.note')}</CardDescription>
        </CardHeader>
        <CardContent className="text-sm text-muted-foreground">{t('it.planned')}</CardContent>
      </Card>
    </div>
  )
}
