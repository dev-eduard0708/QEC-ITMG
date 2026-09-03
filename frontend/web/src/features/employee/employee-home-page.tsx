import { PageHeader } from '@/components/page-header'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { t } from '@/i18n'

export function EmployeeHomePage() {
  return (
    <div>
      <PageHeader title={t('employee.title')} description={t('employee.description')} />
      <Card>
        <CardHeader>
          <CardTitle>{t('nav.employee')}</CardTitle>
          <CardDescription>{t('placeholder.note')}</CardDescription>
        </CardHeader>
        <CardContent className="text-sm text-muted-foreground">{t('employee.planned')}</CardContent>
      </Card>
    </div>
  )
}
