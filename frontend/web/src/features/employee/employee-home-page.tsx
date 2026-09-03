import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { PageHeader } from '@/components/page-header'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

export function EmployeeHomePage() {
  const { t } = useTranslation()

  return (
    <div>
      <PageHeader title={t('employee.title')} description={t('employee.description')} />
      <Card>
        <CardHeader>
          <CardTitle>{t('nav.employee')}</CardTitle>
          <CardDescription>{t('employee.homeHint')}</CardDescription>
        </CardHeader>
        <CardContent>
          <Button asChild>
            <Link to="/employee/equipment">{t('nav.equipment')}</Link>
          </Button>
        </CardContent>
      </Card>
    </div>
  )
}
