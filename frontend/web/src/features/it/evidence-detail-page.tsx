import { useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { evidenceApi } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'

export function EvidenceDetailPage() {
  const { id = '' } = useParams()
  const { t } = useTranslation()
  const { can } = useAuth()
  const qc = useQueryClient()
  const fileRef = useRef<HTMLInputElement>(null)
  const [linkType, setLinkType] = useState('InternalControl')
  const [linkTargetId, setLinkTargetId] = useState('')
  const [withdrawReason, setWithdrawReason] = useState('')

  const detailQuery = useQuery({
    queryKey: ['evidence', id],
    queryFn: () => evidenceApi.get(id),
    enabled: !!id,
  })
  const versionsQuery = useQuery({
    queryKey: ['evidence', id, 'versions'],
    queryFn: () => evidenceApi.listVersions(id),
    enabled: !!id,
  })
  const linksQuery = useQuery({
    queryKey: ['evidence', id, 'links'],
    queryFn: () => evidenceApi.listLinks(id),
    enabled: !!id,
  })

  const invalidate = async () => {
    await qc.invalidateQueries({ queryKey: ['evidence', id] })
  }

  const submitMutation = useMutation({
    mutationFn: () => evidenceApi.submit(id),
    onSuccess: invalidate,
  })
  const acceptMutation = useMutation({
    mutationFn: () => evidenceApi.accept(id),
    onSuccess: invalidate,
  })
  const returnMutation = useMutation({
    mutationFn: () => evidenceApi.returnToDraft(id),
    onSuccess: invalidate,
  })
  const withdrawMutation = useMutation({
    mutationFn: () => evidenceApi.withdraw(id, withdrawReason),
    onSuccess: invalidate,
  })
  const linkMutation = useMutation({
    mutationFn: () => evidenceApi.link(id, linkType, linkTargetId),
    onSuccess: async () => {
      setLinkTargetId('')
      await qc.invalidateQueries({ queryKey: ['evidence', id, 'links'] })
    },
  })
  const uploadMutation = useMutation({
    mutationFn: (file: File) => evidenceApi.upload(id, file, detailQuery.data?.status === 'Accepted'),
    onSuccess: invalidate,
  })
  const exportMutation = useMutation({
    mutationFn: async () => {
      const blob = await evidenceApi.exportZip([id])
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `evidence-${detailQuery.data?.evidenceNumber ?? id}.zip`
      a.click()
      URL.revokeObjectURL(url)
    },
  })

  const item = detailQuery.data
  if (detailQuery.isLoading) return <p className="text-sm text-muted-foreground">{t('evidence.loading')}</p>
  if (!item) return <p className="text-sm text-muted-foreground">{t('evidence.notFound')}</p>

  return (
    <div className="space-y-6">
      <PageHeader
        title={`${item.evidenceNumber} · ${item.title}`}
        description={item.description ?? t('evidence.description')}
        actions={
          <div className="flex flex-wrap gap-2">
            <Button asChild variant="outline">
              <Link to="/it/evidence">{t('evidence.back')}</Link>
            </Button>
            {can('evidence.upload') && item.status === 'Draft' ? (
              <Button type="button" onClick={() => submitMutation.mutate()}>
                {t('evidence.submit')}
              </Button>
            ) : null}
            {can('evidence.accept') && item.status === 'Submitted' ? (
              <>
                <Button type="button" onClick={() => acceptMutation.mutate()}>
                  {t('evidence.accept')}
                </Button>
                <Button type="button" variant="secondary" onClick={() => returnMutation.mutate()}>
                  {t('evidence.return')}
                </Button>
              </>
            ) : null}
            {can('evidence.export') ? (
              <Button type="button" variant="secondary" onClick={() => exportMutation.mutate()}>
                {t('evidence.export')}
              </Button>
            ) : null}
          </div>
        }
      />

      <div className="flex flex-wrap gap-2 text-sm">
        <Badge variant="secondary">{item.status}</Badge>
        <Badge variant="outline">{item.evidenceType}</Badge>
        <Badge variant="outline">{item.sourceType}</Badge>
        <Badge variant="outline">{item.classification}</Badge>
        {item.isExpired ? <Badge variant="outline">{t('evidence.expired')}</Badge> : null}
        {item.isExpiringSoon ? <Badge variant="outline">{t('evidence.expiringSoon')}</Badge> : null}
      </div>

      <Card>
        <CardHeader>
          <CardTitle>{t('evidence.sections.metadata')}</CardTitle>
        </CardHeader>
        <CardContent className="text-sm space-y-1">
          <p>
            {t('evidence.columns.valid')}: {item.validFrom ? new Date(item.validFrom).toLocaleDateString() : '—'} →{' '}
            {item.validTo ? new Date(item.validTo).toLocaleDateString() : '—'}
            {item.daysToExpiry != null ? ` (${item.daysToExpiry}d)` : ''}
          </p>
          <p>
            {t('evidence.acceptedBy')}: {item.acceptedByUserId ?? '—'}{' '}
            {item.acceptedAtUtc ? new Date(item.acceptedAtUtc).toLocaleString() : ''}
          </p>
          {item.currentAttachmentId ? (
            <a
              className="text-primary underline"
              href={`/api/v1/evidence/${id}/attachments/${item.currentAttachmentId}/content`}
            >
              {t('evidence.downloadCurrent')}
            </a>
          ) : (
            <p className="text-muted-foreground">{t('evidence.noFile')}</p>
          )}
          {can('evidence.upload') ? (
            <div className="pt-2">
              <input
                ref={fileRef}
                type="file"
                className="text-sm"
                onChange={(e) => {
                  const file = e.target.files?.[0]
                  if (file) uploadMutation.mutate(file)
                }}
              />
            </div>
          ) : null}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t('evidence.sections.versions')}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2 text-sm">
          {(versionsQuery.data ?? []).map((v) => (
            <div key={v.id} className="rounded border p-2">
              v{v.versionNumber} · {new Date(v.createdAtUtc).toLocaleString()} · {v.changeSummary ?? '—'}
            </div>
          ))}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t('evidence.sections.links')}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3 text-sm">
          {(linksQuery.data ?? []).map((l) => (
            <div key={l.id} className="rounded border p-2">
              {l.targetType}: {l.targetId}
            </div>
          ))}
          {can('evidence.upload') ? (
            <div className="flex flex-wrap gap-2">
              <Input
                className="max-w-[10rem]"
                value={linkType}
                onChange={(e) => setLinkType(e.target.value)}
                placeholder="InternalControl"
              />
              <Input
                className="max-w-xs"
                value={linkTargetId}
                onChange={(e) => setLinkTargetId(e.target.value)}
                placeholder="target id"
              />
              <Button type="button" variant="secondary" disabled={!linkTargetId} onClick={() => linkMutation.mutate()}>
                {t('evidence.link')}
              </Button>
            </div>
          ) : null}
        </CardContent>
      </Card>

      {can('evidence.accept') && item.status !== 'Withdrawn' ? (
        <Card>
          <CardHeader>
            <CardTitle>{t('evidence.withdraw')}</CardTitle>
          </CardHeader>
          <CardContent className="flex gap-2">
            <Input value={withdrawReason} onChange={(e) => setWithdrawReason(e.target.value)} placeholder={t('evidence.reason')} />
            <Button type="button" variant="outline" disabled={!withdrawReason.trim()} onClick={() => withdrawMutation.mutate()}>
              {t('evidence.withdraw')}
            </Button>
          </CardContent>
        </Card>
      ) : null}
    </div>
  )
}
