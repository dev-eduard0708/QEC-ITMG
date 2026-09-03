import { useTranslation } from 'react-i18next'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

export function FoundationHomePage() {
  const { t } = useTranslation()

  return (
    <div>
      <PageHeader title={t('foundation.title')} description={t('foundation.description')} />
      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between gap-3">
              <CardTitle>{t('foundation.card.title')}</CardTitle>
              <Badge variant="success">{t('status.foundation')}</Badge>
            </div>
            <CardDescription>{t('placeholder.note')}</CardDescription>
          </CardHeader>
          <CardContent className="text-sm leading-relaxed text-muted-foreground">
            {t('foundation.card.body')}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>{t('foundation.next.title')}</CardTitle>
            <CardDescription>{t('brand.product')}</CardDescription>
          </CardHeader>
          <CardContent className="text-sm leading-relaxed text-muted-foreground">
            {t('foundation.next.body')}
          </CardContent>
        </Card>
      </div>
    </div>
  )
}
