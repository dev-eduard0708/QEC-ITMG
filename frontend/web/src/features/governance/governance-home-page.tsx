import { useTranslation } from 'react-i18next'
import { PageHeader } from '@/components/page-header'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

export function GovernanceHomePage() {
  const { t } = useTranslation()

  return (
    <div>
      <PageHeader title={t('governance.title')} description={t('governance.description')} />
      <Card>
        <CardHeader>
          <CardTitle>{t('nav.governance')}</CardTitle>
          <CardDescription>{t('placeholder.note')}</CardDescription>
        </CardHeader>
        <CardContent className="text-sm text-muted-foreground">{t('governance.planned')}</CardContent>
      </Card>
    </div>
  )
}
