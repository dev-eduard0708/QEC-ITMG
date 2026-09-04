import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { controlsApi } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'

export function ControlDetailPage() {
  const { id = '' } = useParams()
  const { t } = useTranslation()
  const { can } = useAuth()
  const qc = useQueryClient()
  const manage = can('control.manage')

  const [procedureTitle, setProcedureTitle] = useState('')
  const [procedureSteps, setProcedureSteps] = useState('')
  const [expectedResult, setExpectedResult] = useState('')
  const [evidenceDesc, setEvidenceDesc] = useState('')
  const [linkCiId, setLinkCiId] = useState('')
  const [linkServiceId, setLinkServiceId] = useState('')
  const [linkDocId, setLinkDocId] = useState('')

  const detailQuery = useQuery({
    queryKey: ['controls', id],
    queryFn: () => controlsApi.get(id),
    enabled: !!id,
  })

  const invalidate = async () => {
    await qc.invalidateQueries({ queryKey: ['controls', id] })
  }

  const activateMutation = useMutation({
    mutationFn: () => controlsApi.activate(id),
    onSuccess: invalidate,
  })
  const retireMutation = useMutation({
    mutationFn: () => controlsApi.retire(id),
    onSuccess: invalidate,
  })
  const addProcedureMutation = useMutation({
    mutationFn: () =>
      controlsApi.addTestProcedure(id, {
        title: procedureTitle,
        procedureSteps,
        expectedResult,
      }),
    onSuccess: async () => {
      setProcedureTitle('')
      setProcedureSteps('')
      setExpectedResult('')
      await invalidate()
    },
  })
  const addEvidenceMutation = useMutation({
    mutationFn: () => controlsApi.addEvidenceRequirement(id, { description: evidenceDesc }),
    onSuccess: async () => {
      setEvidenceDesc('')
      await invalidate()
    },
  })
  const linkCiMutation = useMutation({
    mutationFn: () => controlsApi.linkConfigurationItem(id, linkCiId),
    onSuccess: async () => {
      setLinkCiId('')
      await invalidate()
    },
  })
  const linkServiceMutation = useMutation({
    mutationFn: () => controlsApi.linkBusinessService(id, linkServiceId),
    onSuccess: async () => {
      setLinkServiceId('')
      await invalidate()
    },
  })
  const linkDocMutation = useMutation({
    mutationFn: () => controlsApi.linkDocument(id, linkDocId),
    onSuccess: async () => {
      setLinkDocId('')
      await invalidate()
    },
  })

  const control = detailQuery.data
  if (detailQuery.isLoading) return <p className="text-sm text-muted-foreground">{t('controls.loading')}</p>
  if (!control) return <p className="text-sm text-muted-foreground">{t('controls.notFound')}</p>

  return (
    <div className="space-y-6">
      <PageHeader
        title={`${control.controlNumber} · ${control.title}`}
        description={control.objective}
        actions={
          <div className="flex flex-wrap gap-2">
            <Button asChild variant="outline">
              <Link to="/it/controls">{t('controls.back')}</Link>
            </Button>
            {manage && control.status === 'Draft' ? (
              <Button type="button" onClick={() => activateMutation.mutate()}>
                {t('controls.activate')}
              </Button>
            ) : null}
            {manage && control.status !== 'Retired' ? (
              <Button type="button" variant="outline" onClick={() => retireMutation.mutate()}>
                {t('controls.retire')}
              </Button>
            ) : null}
          </div>
        }
      />

      <div className="flex flex-wrap gap-2 text-sm">
        <Badge variant="outline">{control.domainLabel}</Badge>
        <Badge variant="secondary">{control.status}</Badge>
        <Badge variant="outline">{control.frequency}</Badge>
        <Badge variant="outline">{control.automationType}</Badge>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>{t('controls.sections.overview')}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2 text-sm whitespace-pre-wrap">{control.description}</CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t('controls.sections.owners')}</CardTitle>
        </CardHeader>
        <CardContent className="text-sm space-y-1">
          <p>
            {t('controls.columns.owner')}: {control.primaryOwnerUserId ?? '—'}
          </p>
          <p>
            {t('controls.secondaryOwners')}:{' '}
            {control.secondaryOwnerUserIds.length ? control.secondaryOwnerUserIds.join(', ') : '—'}
          </p>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t('controls.sections.links')}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3 text-sm">
          <p>
            {t('controls.linkedPolicies')}: {control.linkedManagedDocumentIds.length || '—'}
          </p>
          <p>
            {t('controls.linkedCis')}: {control.linkedConfigurationItemIds.length || '—'}
          </p>
          <p>
            {t('controls.linkedServices')}: {control.linkedBusinessServiceIds.length || '—'}
          </p>
          {manage ? (
            <div className="grid gap-2 md:grid-cols-3">
              <div className="flex gap-2">
                <Input value={linkDocId} onChange={(e) => setLinkDocId(e.target.value)} placeholder="document id" />
                <Button type="button" variant="secondary" disabled={!linkDocId} onClick={() => linkDocMutation.mutate()}>
                  {t('controls.link')}
                </Button>
              </div>
              <div className="flex gap-2">
                <Input value={linkCiId} onChange={(e) => setLinkCiId(e.target.value)} placeholder="CI id" />
                <Button type="button" variant="secondary" disabled={!linkCiId} onClick={() => linkCiMutation.mutate()}>
                  {t('controls.link')}
                </Button>
              </div>
              <div className="flex gap-2">
                <Input
                  value={linkServiceId}
                  onChange={(e) => setLinkServiceId(e.target.value)}
                  placeholder="service id"
                />
                <Button
                  type="button"
                  variant="secondary"
                  disabled={!linkServiceId}
                  onClick={() => linkServiceMutation.mutate()}
                >
                  {t('controls.link')}
                </Button>
              </div>
            </div>
          ) : null}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t('controls.sections.procedures')}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {control.testProcedures.map((p) => (
            <div key={p.id} className="rounded border p-3 text-sm space-y-1">
              <div className="font-medium">{p.title}</div>
              <div className="text-muted-foreground whitespace-pre-wrap">{p.procedureSteps}</div>
              <div>
                {t('controls.fields.expectedResult')}: {p.expectedResult}
              </div>
            </div>
          ))}
          {manage ? (
            <div className="space-y-2">
              <Input
                value={procedureTitle}
                onChange={(e) => setProcedureTitle(e.target.value)}
                placeholder={t('controls.fields.procedureTitle')}
              />
              <textarea
                className="min-h-24 w-full rounded-md border bg-background px-3 py-2 text-sm"
                value={procedureSteps}
                onChange={(e) => setProcedureSteps(e.target.value)}
                placeholder={t('controls.fields.procedureSteps')}
              />
              <Input
                value={expectedResult}
                onChange={(e) => setExpectedResult(e.target.value)}
                placeholder={t('controls.fields.expectedResult')}
              />
              <Button
                type="button"
                disabled={!procedureTitle.trim() || !procedureSteps.trim() || !expectedResult.trim()}
                onClick={() => addProcedureMutation.mutate()}
              >
                {t('controls.addProcedure')}
              </Button>
            </div>
          ) : null}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t('controls.sections.evidence')}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {control.evidenceRequirements.map((e) => (
            <div key={e.id} className="text-sm rounded border p-3">
              {e.description}
              {e.isRequired ? <Badge className="ml-2" variant="outline">{t('controls.required')}</Badge> : null}
            </div>
          ))}
          {manage ? (
            <div className="flex gap-2">
              <Input
                value={evidenceDesc}
                onChange={(e) => setEvidenceDesc(e.target.value)}
                placeholder={t('controls.fields.evidenceDescription')}
              />
              <Button
                type="button"
                disabled={!evidenceDesc.trim()}
                onClick={() => addEvidenceMutation.mutate()}
              >
                {t('controls.addEvidence')}
              </Button>
            </div>
          ) : null}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t('controls.sections.history')}</CardTitle>
        </CardHeader>
        <CardContent className="text-sm text-muted-foreground">
          {t('controls.createdAt')}: {new Date(control.createdAtUtc).toLocaleString()}
          <br />
          {t('controls.updatedAt')}: {new Date(control.updatedAtUtc).toLocaleString()}
          {control.retiredAtUtc ? (
            <>
              <br />
              {t('controls.retiredAt')}: {new Date(control.retiredAtUtc).toLocaleString()}
            </>
          ) : null}
        </CardContent>
      </Card>
    </div>
  )
}
