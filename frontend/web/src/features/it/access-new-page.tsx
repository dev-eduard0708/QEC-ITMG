import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useMutation } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ApiError, accessApi } from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'

const types = ['Joiner', 'Mover', 'Leaver', 'AccessRequest'] as const

export function AccessNewPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [type, setType] = useState<string>('Joiner')
  const [reason, setReason] = useState('')
  const [subjectName, setSubjectName] = useState('')
  const [subjectEmail, setSubjectEmail] = useState('')
  const [approverId, setApproverId] = useState('')
  const [error, setError] = useState<string | null>(null)

  const createMutation = useMutation({
    mutationFn: () =>
      accessApi.createCase({
        type,
        reason,
        subjectName: subjectName || null,
        subjectEmail: subjectEmail || null,
        designatedApproverUserId: approverId || null,
      }),
    onSuccess: (created) => navigate(`/it/access/${created.id}`),
    onError: (err) => setError(err instanceof ApiError ? err.message : t('access.error.generic')),
  })

  return (
    <div className="mx-auto max-w-xl space-y-6">
      <PageHeader
        title={t('access.newTitle')}
        description={t('access.newDescription')}
        actions={
          <Button asChild variant="secondary">
            <Link to="/it/access">{t('access.back')}</Link>
          </Button>
        }
      />
      <div className="space-y-4">
        <div className="space-y-1">
          <Label>{t('access.columns.type')}</Label>
          <Select value={type} onValueChange={setType}>
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {types.map((item) => (
                <SelectItem key={item} value={item}>
                  {item}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-1">
          <Label htmlFor="reason">{t('access.columns.reason')}</Label>
          <Input id="reason" value={reason} onChange={(e) => setReason(e.target.value)} />
        </div>
        <div className="space-y-1">
          <Label htmlFor="subjectName">{t('access.fields.subjectName')}</Label>
          <Input id="subjectName" value={subjectName} onChange={(e) => setSubjectName(e.target.value)} />
        </div>
        <div className="space-y-1">
          <Label htmlFor="subjectEmail">{t('access.fields.subjectEmail')}</Label>
          <Input id="subjectEmail" value={subjectEmail} onChange={(e) => setSubjectEmail(e.target.value)} />
        </div>
        <div className="space-y-1">
          <Label htmlFor="approver">{t('access.fields.approver')}</Label>
          <Input
            id="approver"
            value={approverId}
            placeholder={t('access.fields.approverPlaceholder')}
            onChange={(e) => setApproverId(e.target.value)}
          />
        </div>
        {error ? <p className="text-sm text-destructive">{error}</p> : null}
        <Button
          type="button"
          disabled={!reason.trim() || createMutation.isPending}
          onClick={() => createMutation.mutate()}
        >
          {t('access.create')}
        </Button>
      </div>
    </div>
  )
}
