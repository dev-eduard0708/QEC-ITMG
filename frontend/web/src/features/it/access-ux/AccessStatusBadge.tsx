import { useTranslation } from 'react-i18next'
import type { AccessCase } from '@/api/client'
import { Badge } from '@/components/ui/badge'
import { getAccessFriendlyStatus } from '@/features/it/access-ux/access-status'
import { cn } from '@/lib/utils'

export function AccessStatusBadge({
  accessCase,
  className,
}: {
  accessCase: Pick<AccessCase, 'status' | 'isReadyToClose'>
  className?: string
}) {
  const { t } = useTranslation()
  const friendly = getAccessFriendlyStatus(accessCase)

  return (
    <Badge variant={friendly.tone} className={cn('font-medium', className)}>
      {t(friendly.labelKey)}
    </Badge>
  )
}
