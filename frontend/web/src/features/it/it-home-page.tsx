import { useTranslation } from 'react-i18next'
import { PageHeader } from '@/components/page-header'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

export function ItHomePage() {
  const { t } = useTranslation()

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
