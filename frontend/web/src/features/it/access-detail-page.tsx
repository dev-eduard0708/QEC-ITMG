import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ApiError, accessApi, type AccessEvidenceProjection } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
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

export function AccessDetailPage() {
  const { id = '' } = useParams()
  const { t } = useTranslation()
  const { can } = useAuth()
  const qc = useQueryClient()
  const [entitlement, setEntitlement] = useState('')
  const [action, setAction] = useState('Grant')
  const [existingKey, setExistingKey] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [evidence, setEvidence] = useState<AccessEvidenceProjection | null>(null)

  const caseQuery = useQuery({
    queryKey: ['access', 'case', id],
    queryFn: () => accessApi.getCase(id),
    enabled: !!id,
  })
  const itemsQuery = useQuery({
    queryKey: ['access', 'case', id, 'items'],
    queryFn: () => accessApi.listItems(id),
    enabled: !!id,
  })
  const existingQuery = useQuery({
    queryKey: ['access', 'case', id, 'existing'],
    queryFn: () => accessApi.listExistingAccess(id),
    enabled: !!id && caseQuery.data?.type === 'Mover',
  })

  const invalidate = async () => {
    await qc.invalidateQueries({ queryKey: ['access', 'case', id] })
    await qc.invalidateQueries({ queryKey: ['access', 'cases'] })
  }

  const run = useMutation({
    mutationFn: async (fn: () => Promise<unknown>) => fn(),
    onSuccess: async () => {
      setError(null)
      await invalidate()
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t('access.error.generic')),
  })

  const accessCase = caseQuery.data
  if (caseQuery.isLoading) return <p className="text-sm text-muted-foreground">{t('access.loading')}</p>
  if (!accessCase) return <p className="text-sm text-destructive">{t('access.notFound')}</p>

  return (
    <div className="space-y-6">
      <PageHeader
        title={accessCase.caseNumber}
        description={`${accessCase.type} · ${accessCase.status}`}
        actions={
          <Button asChild variant="secondary">
            <Link to="/it/access">{t('access.back')}</Link>
          </Button>
        }
      />
      <div className="flex flex-wrap gap-2">
        <Badge variant="outline">{accessCase.type}</Badge>
        <Badge variant="secondary">{accessCase.status}</Badge>
        {accessCase.existingAccessConfirmed ? (
          <Badge variant="success">{t('access.existingConfirmed')}</Badge>
        ) : null}
      </div>
      <p className="text-sm">{accessCase.reason}</p>
      {error ? <p className="text-sm text-destructive">{error}</p> : null}

      <div className="flex flex-wrap gap-2">
        {accessCase.status === 'Draft' && can('access.request') ? (
          <Button type="button" onClick={() => run.mutate(() => accessApi.submit(id))}>
            {t('access.actions.submit')}
          </Button>
        ) : null}
        {accessCase.status === 'Submitted' && can('access.approve') ? (
          <Button type="button" onClick={() => run.mutate(() => accessApi.startApproval(id))}>
            {t('access.actions.startApproval')}
          </Button>
        ) : null}
        {accessCase.status === 'Approval' && can('access.approve') ? (
          <>
            <Button type="button" onClick={() => run.mutate(() => accessApi.approve(id))}>
              {t('access.actions.approve')}
            </Button>
            <Button type="button" variant="secondary" onClick={() => run.mutate(() => accessApi.reject(id))}>
              {t('access.actions.reject')}
            </Button>
          </>
        ) : null}
        {accessCase.status === 'Fulfillment' && can('access.fulfill') ? (
          <Button type="button" onClick={() => run.mutate(() => accessApi.startVerification(id))}>
            {t('access.actions.startVerification')}
          </Button>
        ) : null}
        {accessCase.status === 'Verification' && can('access.fulfill') ? (
          <Button type="button" onClick={() => run.mutate(() => accessApi.close(id))}>
            {t('access.actions.close')}
          </Button>
        ) : null}
        {accessCase.status === 'Closed' ? (
          <Button
            type="button"
            variant="secondary"
            onClick={() =>
              run.mutate(async () => {
                setEvidence(await accessApi.prepareCaseEvidence(id))
              })
            }
          >
            {t('access.actions.prepareEvidence')}
          </Button>
        ) : null}
      </div>

      {accessCase.type === 'Mover' ? (
        <section className="space-y-3">
          <h2 className="text-base font-medium">{t('access.existingTitle')}</h2>
          <ul className="space-y-1 text-sm">
            {(existingQuery.data ?? []).map((item) => (
              <li key={item.id}>
                <Badge variant="outline" className="me-2">
                  Existing
                </Badge>
                {item.entitlementKey}
                {item.accessSummary ? ` — ${item.accessSummary}` : ''}
              </li>
            ))}
          </ul>
          {can('access.request') && accessCase.status !== 'Closed' ? (
            <div className="flex flex-wrap gap-2">
              <Input
                className="max-w-xs"
                value={existingKey}
                placeholder={t('access.fields.entitlement')}
                onChange={(e) => setExistingKey(e.target.value)}
              />
              <Button
                type="button"
                size="sm"
                disabled={!existingKey.trim()}
                onClick={() =>
                  run.mutate(async () => {
                    await accessApi.addExistingAccess(id, { entitlementKey: existingKey })
                    setExistingKey('')
                    await qc.invalidateQueries({ queryKey: ['access', 'case', id, 'existing'] })
                  })
                }
              >
                {t('access.actions.addExisting')}
              </Button>
              <Button
                type="button"
                size="sm"
                variant="secondary"
                onClick={() => run.mutate(() => accessApi.confirmExistingAccess(id))}
              >
                {t('access.actions.confirmExisting')}
              </Button>
            </div>
          ) : null}
        </section>
      ) : null}

      <section className="space-y-3">
        <h2 className="text-base font-medium">{t('access.itemsTitle')}</h2>
        <ul className="space-y-2 text-sm">
          {(itemsQuery.data ?? []).map((item) => (
            <li key={item.id} className="flex flex-wrap items-center gap-2">
              <Badge variant="outline">{item.action}</Badge>
              <span>{item.entitlementKey}</span>
              <Badge variant="secondary">{item.status}</Badge>
              {item.isMandatory ? <Badge variant="warning">{t('access.mandatory')}</Badge> : null}
              {item.isPrivileged ? <Badge variant="outline">{t('access.privileged')}</Badge> : null}
              {can('access.fulfill') && item.status === 'Pending' ? (
                <Button
                  type="button"
                  size="sm"
                  variant="secondary"
                  onClick={() =>
                    run.mutate(async () => {
                      await accessApi.completeItem(id, item.id)
                      await qc.invalidateQueries({ queryKey: ['access', 'case', id, 'items'] })
                    })
                  }
                >
                  {t('access.actions.completeItem')}
                </Button>
              ) : null}
            </li>
          ))}
        </ul>
        {can('access.request') && !['Closed', 'Rejected', 'Cancelled'].includes(accessCase.status) ? (
          <div className="flex flex-wrap items-end gap-2">
            <div className="space-y-1">
              <Label>{t('access.fields.entitlement')}</Label>
              <Input value={entitlement} onChange={(e) => setEntitlement(e.target.value)} />
            </div>
            <div className="space-y-1">
              <Label>{t('access.fields.action')}</Label>
              <Select value={action} onValueChange={setAction}>
                <SelectTrigger className="w-[140px]">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {['Grant', 'Remove', 'Disable', 'Reassign'].map((item) => (
                    <SelectItem key={item} value={item}>
                      {item}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <Button
              type="button"
              disabled={!entitlement.trim()}
              onClick={() =>
                run.mutate(async () => {
                  await accessApi.addItem(id, { entitlementKey: entitlement, action })
                  setEntitlement('')
                  await qc.invalidateQueries({ queryKey: ['access', 'case', id, 'items'] })
                })
              }
            >
              {t('access.actions.addItem')}
            </Button>
          </div>
        ) : null}
      </section>

      {evidence ? (
        <section className="space-y-2 rounded-lg border p-4 text-sm">
          <h2 className="font-medium">{t('access.evidenceTitle')}</h2>
          <p>
            {evidence.sourceType} · {evidence.businessNumber} · {evidence.status}
          </p>
          <ul className="list-disc ps-5">
            {evidence.actorHistorySummary.map((line) => (
              <li key={line}>{line}</li>
            ))}
          </ul>
          <ul className="list-disc ps-5">
            {evidence.fulfillmentOrReviewDecisions.map((line) => (
              <li key={line}>{line}</li>
            ))}
          </ul>
        </section>
      ) : null}
    </div>
  )
}
