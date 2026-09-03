import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { PageHeader } from '@/components/page-header'

export function UnauthorizedPage() {
  const { t } = useTranslation()

  return (
    <div className="mx-auto max-w-lg space-y-6">
      <PageHeader title={t('unauthorized.title')} description={t('unauthorized.description')} />
      <Button asChild variant="outline">
        <Link to="/it">{t('unauthorized.back')}</Link>
      </Button>
    </div>
  )
}
