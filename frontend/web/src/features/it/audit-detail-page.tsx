import { useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { auditsApi } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'

type Tab = 'overview' | 'scope' | 'questions' | 'evidence' | 'findings' | 'capa' | 'export'

export function AuditDetailPage() {
  const { id = '' } = useParams()
  const { t } = useTranslation()
  const { can, user } = useAuth()
  const qc = useQueryClient()
  const [tab, setTab] = useState<Tab>('overview')
  const [questionResponse, setQuestionResponse] = useState<Record<string, string>>({})
  const [findingTitle, setFindingTitle] = useState('')
  const [findingDesc, setFindingDesc] = useState('')
  const [capaTitle, setCapaTitle] = useState('')
  const [capaDesc, setCapaDesc] = useState('')
  const [selectedFindingId, setSelectedFindingId] = useState('')
  const [requestTitle, setRequestTitle] = useState('')
  const [fulfillEvidenceId, setFulfillEvidenceId] = useState('')
  const [mgmtResponse, setMgmtResponse] = useState('')
  const [scopeType, setScopeType] = useState('InternalControl')
  const [scopeTargetId, setScopeTargetId] = useState('')
  const [acceptedRiskReason, setAcceptedRiskReason] = useState('')

  const detailQuery = useQuery({
    queryKey: ['audits', id],
    queryFn: () => auditsApi.get(id),
    enabled: Boolean(id),
  })
  const scopeQuery = useQuery({
    queryKey: ['audits', id, 'scope'],
    queryFn: () => auditsApi.listScope(id),
    enabled: Boolean(id),
  })
  const questionsQuery = useQuery({
    queryKey: ['audits', id, 'questions'],
    queryFn: () => auditsApi.listQuestions(id),
    enabled: Boolean(id),
  })
  const findingsQuery = useQuery({
    queryKey: ['audits', id, 'findings'],
    queryFn: () => auditsApi.listFindings({ engagementId: id }),
    enabled: Boolean(id),
  })
  const capaQuery = useQuery({
    queryKey: ['audits', id, 'capa'],
    queryFn: () => auditsApi.listCapa({ engagementId: id }),
    enabled: Boolean(id),
  })
  const capaSummaryQuery = useQuery({
    queryKey: ['audits', id, 'capa-summary'],
    queryFn: () => auditsApi.capaSummary(id),
    enabled: Boolean(id),
  })
  const requestsQuery = useQuery({
    queryKey: ['audits', id, 'evidence-requests'],
    queryFn: () => auditsApi.listEvidenceRequests({ engagementId: id }),
    enabled: Boolean(id),
  })

  const refresh = async () => {
    await qc.invalidateQueries({ queryKey: ['audits', id] })
  }

  const transitionMutation = useMutation({
    mutationFn: (status: string) => auditsApi.transition(id, status),
    onSuccess: refresh,
  })
  const answerMutation = useMutation({
    mutationFn: ({ questionId, response }: { questionId: string; response: string }) =>
      auditsApi.answerQuestion(id, questionId, response),
    onSuccess: refresh,
  })
  const createFindingMutation = useMutation({
    mutationFn: () =>
      auditsApi.createFinding(id, {
        title: findingTitle,
        description: findingDesc,
        severity: 'Medium',
      }),
    onSuccess: async () => {
      setFindingTitle('')
      setFindingDesc('')
      await refresh()
    },
  })
  const createCapaMutation = useMutation({
    mutationFn: () =>
      auditsApi.createCapa(selectedFindingId, {
        title: capaTitle,
        description: capaDesc,
        ownerUserId: user!.id,
      }),
    onSuccess: async () => {
      setCapaTitle('')
      setCapaDesc('')
      await refresh()
    },
  })
  const createRequestMutation = useMutation({
    mutationFn: () => auditsApi.createEvidenceRequest(id, { title: requestTitle }),
    onSuccess: async () => {
      setRequestTitle('')
      await refresh()
    },
  })
  const fulfillMutation = useMutation({
    mutationFn: (requestId: string) => auditsApi.fulfillEvidenceRequest(requestId, fulfillEvidenceId),
    onSuccess: refresh,
  })
  const addScopeMutation = useMutation({
    mutationFn: () => auditsApi.addScope(id, scopeType, scopeTargetId),
    onSuccess: async () => {
      setScopeTargetId('')
      await refresh()
    },
  })
  const responseMutation = useMutation({
    mutationFn: (findingId: string) =>
      auditsApi.addManagementResponse(findingId, { responseText: mgmtResponse }),
    onSuccess: async () => {
      setMgmtResponse('')
      await refresh()
    },
  })
  const findingTransitionMutation = useMutation({
    mutationFn: ({
      findingId,
      status,
    }: {
      findingId: string
      status: string
    }) =>
      auditsApi.transitionFinding(findingId, {
        status,
        acceptedRiskReason: status === 'AcceptedRisk' ? acceptedRiskReason : undefined,
      }),
    onSuccess: refresh,
  })
  const capaTransitionMutation = useMutation({
    mutationFn: ({ capaId, status }: { capaId: string; status: string }) =>
      auditsApi.transitionCapa(capaId, status),
    onSuccess: refresh,
  })

  const tabs = useMemo(
    () =>
      (
        [
          ['overview', 'audits.tabs.overview'],
          ['scope', 'audits.tabs.scope'],
          ['questions', 'audits.tabs.questions'],
          ['evidence', 'audits.tabs.evidence'],
          ['findings', 'audits.tabs.findings'],
          ['capa', 'audits.tabs.capa'],
          ['export', 'audits.tabs.export'],
        ] as const
      ).map(([key, label]) => ({ key, label: t(label) })),
    [t],
  )

  const item = detailQuery.data
  if (detailQuery.isLoading) return <p className="text-sm text-muted-foreground">{t('audits.loading')}</p>
  if (!item) return <p className="text-sm text-muted-foreground">{t('audits.notFound')}</p>

  const nextStatuses: Record<string, string[]> = {
    Draft: ['Planned', 'Cancelled'],
    Planned: ['InProgress', 'Cancelled'],
    InProgress: ['Fieldwork', 'Reporting', 'Cancelled'],
    Fieldwork: ['Reporting', 'Cancelled'],
    Reporting: ['Closed', 'Cancelled'],
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={item.auditNumber}
        description={`${item.title} · ${item.auditType}`}
        actions={
          <div className="flex flex-wrap gap-2">
            <Button asChild variant="outline">
              <Link to="/it/audits">{t('audits.back')}</Link>
            </Button>
            {can('audit.manage')
              ? (nextStatuses[item.status] ?? []).map((s) => (
                  <Button key={s} type="button" size="sm" onClick={() => transitionMutation.mutate(s)}>
                    → {s}
                  </Button>
                ))
              : null}
          </div>
        }
      />

      <div className="flex flex-wrap gap-2">
        <Badge>{item.status}</Badge>
        {item.auditType === 'ISA315Profile' ? (
          <Badge variant="outline">{t('audits.isa315.badge')}</Badge>
        ) : null}
      </div>

      <div className="flex flex-wrap gap-2">
        {tabs.map((tabItem) => (
          <Button
            key={tabItem.key}
            type="button"
            size="sm"
            variant={tab === tabItem.key ? 'default' : 'outline'}
            onClick={() => setTab(tabItem.key)}
          >
            {tabItem.label}
          </Button>
        ))}
      </div>

      {tab === 'overview' ? (
        <Card>
          <CardHeader>
            <CardTitle>{t('audits.tabs.overview')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2 text-sm">
            <p>{item.objective ?? '—'}</p>
            <p className="text-muted-foreground">{item.scopeSummary ?? '—'}</p>
            {item.auditType === 'ISA315Profile' ? (
              <p className="text-muted-foreground">{t('audits.isa315.note')}</p>
            ) : null}
          </CardContent>
        </Card>
      ) : null}

      {tab === 'scope' ? (
        <div className="space-y-4">
          {(scopeQuery.data ?? []).map((s) => (
            <p key={s.id} className="text-sm">
              {s.targetType}: {s.targetId}
            </p>
          ))}
          {can('audit.manage') ? (
            <div className="flex flex-wrap gap-2">
              <Select value={scopeType} onValueChange={setScopeType}>
                <SelectTrigger className="w-48">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {['ConfigurationItem', 'BusinessService', 'InternalControl', 'FrameworkVersion'].map(
                    (x) => (
                      <SelectItem key={x} value={x}>
                        {x}
                      </SelectItem>
                    ),
                  )}
                </SelectContent>
              </Select>
              <Input
                className="max-w-sm"
                value={scopeTargetId}
                onChange={(e) => setScopeTargetId(e.target.value)}
                placeholder={t('audits.fields.targetId')}
              />
              <Button
                type="button"
                disabled={!scopeTargetId}
                onClick={() => addScopeMutation.mutate()}
              >
                {t('audits.addScope')}
              </Button>
            </div>
          ) : null}
        </div>
      ) : null}

      {tab === 'questions' ? (
        <div className="space-y-3">
          {(questionsQuery.data ?? []).map((q) => (
            <Card key={q.id}>
              <CardContent className="space-y-2 py-4">
                <p className="text-xs text-muted-foreground">
                  {q.questionCode ?? ''} · {q.category} · {q.status}
                </p>
                <p className="font-medium">{q.questionText}</p>
                {q.response ? <p className="text-sm">{q.response}</p> : null}
                {can('audit.manage') && q.status === 'Open' ? (
                  <div className="flex flex-wrap gap-2">
                    <Input
                      value={questionResponse[q.id] ?? ''}
                      onChange={(e) =>
                        setQuestionResponse((prev) => ({ ...prev, [q.id]: e.target.value }))
                      }
                      placeholder={t('audits.fields.response')}
                    />
                    <Button
                      type="button"
                      size="sm"
                      onClick={() =>
                        answerMutation.mutate({
                          questionId: q.id,
                          response: questionResponse[q.id] ?? '',
                        })
                      }
                    >
                      {t('audits.answer')}
                    </Button>
                  </div>
                ) : null}
              </CardContent>
            </Card>
          ))}
        </div>
      ) : null}

      {tab === 'evidence' ? (
        <div className="space-y-4">
          {(requestsQuery.data ?? []).map((r) => (
            <Card key={r.id}>
              <CardContent className="space-y-2 py-4">
                <p className="font-medium">{r.title}</p>
                <p className="text-sm text-muted-foreground">
                  {r.status}
                  {r.evidenceId ? ` · Evidence ${r.evidenceId.slice(0, 8)}` : ''}
                </p>
                {can('audit.manage') && r.status !== 'Fulfilled' && r.status !== 'Cancelled' ? (
                  <div className="flex flex-wrap gap-2">
                    <Input
                      value={fulfillEvidenceId}
                      onChange={(e) => setFulfillEvidenceId(e.target.value)}
                      placeholder={t('audits.fields.evidenceId')}
                    />
                    <Button
                      type="button"
                      size="sm"
                      disabled={!fulfillEvidenceId}
                      onClick={() => fulfillMutation.mutate(r.id)}
                    >
                      {t('audits.fulfill')}
                    </Button>
                    <Button asChild size="sm" variant="outline">
                      <Link to="/it/evidence/new">{t('audits.createEvidence')}</Link>
                    </Button>
                  </div>
                ) : null}
              </CardContent>
            </Card>
          ))}
          {can('audit.manage') ? (
            <div className="flex flex-wrap gap-2">
              <Input
                value={requestTitle}
                onChange={(e) => setRequestTitle(e.target.value)}
                placeholder={t('audits.fields.requestTitle')}
              />
              <Button
                type="button"
                disabled={!requestTitle.trim()}
                onClick={() => createRequestMutation.mutate()}
              >
                {t('audits.addRequest')}
              </Button>
            </div>
          ) : null}
        </div>
      ) : null}

      {tab === 'findings' ? (
        <div className="space-y-4">
          {(findingsQuery.data ?? []).map((f) => (
            <Card key={f.id}>
              <CardContent className="space-y-2 py-4">
                <p className="font-medium">
                  {f.findingNumber} · {f.title}
                </p>
                <p className="text-sm text-muted-foreground">
                  {f.severity} · {f.status}
                </p>
                <p className="text-sm">{f.description}</p>
                {can('finding.manage') ? (
                  <div className="flex flex-wrap gap-2">
                    {f.status === 'Open' ? (
                      <Button
                        type="button"
                        size="sm"
                        onClick={() =>
                          findingTransitionMutation.mutate({ findingId: f.id, status: 'InRemediation' })
                        }
                      >
                        InRemediation
                      </Button>
                    ) : null}
                    {f.status !== 'Closed' && f.status !== 'AcceptedRisk' ? (
                      <>
                        <Input
                          className="max-w-xs"
                          value={acceptedRiskReason}
                          onChange={(e) => setAcceptedRiskReason(e.target.value)}
                          placeholder={t('audits.fields.acceptedRiskReason')}
                        />
                        <Button
                          type="button"
                          size="sm"
                          variant="secondary"
                          onClick={() =>
                            findingTransitionMutation.mutate({
                              findingId: f.id,
                              status: 'AcceptedRisk',
                            })
                          }
                        >
                          AcceptedRisk
                        </Button>
                        <Button
                          type="button"
                          size="sm"
                          onClick={() =>
                            findingTransitionMutation.mutate({ findingId: f.id, status: 'Closed' })
                          }
                        >
                          Close
                        </Button>
                      </>
                    ) : null}
                    <Input
                      value={mgmtResponse}
                      onChange={(e) => setMgmtResponse(e.target.value)}
                      placeholder={t('audits.fields.managementResponse')}
                    />
                    <Button
                      type="button"
                      size="sm"
                      disabled={!mgmtResponse.trim()}
                      onClick={() => responseMutation.mutate(f.id)}
                    >
                      {t('audits.addResponse')}
                    </Button>
                  </div>
                ) : null}
              </CardContent>
            </Card>
          ))}
          {can('finding.manage') ? (
            <div className="space-y-2">
              <Input
                value={findingTitle}
                onChange={(e) => setFindingTitle(e.target.value)}
                placeholder={t('audits.fields.findingTitle')}
              />
              <Input
                value={findingDesc}
                onChange={(e) => setFindingDesc(e.target.value)}
                placeholder={t('audits.fields.findingDescription')}
              />
              <Button
                type="button"
                disabled={!findingTitle.trim() || !findingDesc.trim()}
                onClick={() => createFindingMutation.mutate()}
              >
                {t('audits.addFinding')}
              </Button>
            </div>
          ) : null}
        </div>
      ) : null}

      {tab === 'capa' ? (
        <div className="space-y-4">
          {capaSummaryQuery.data ? (
            <p className="text-sm text-muted-foreground">
              {t('audits.capa.open')}: {capaSummaryQuery.data.open} · {t('audits.capa.overdue')}:{' '}
              {capaSummaryQuery.data.overdue} · {t('audits.capa.awaiting')}:{' '}
              {capaSummaryQuery.data.completedAwaitingVerification} · {t('audits.capa.verified')}:{' '}
              {capaSummaryQuery.data.verified}
            </p>
          ) : null}
          {(capaQuery.data ?? []).map((c) => (
            <Card key={c.id}>
              <CardContent className="space-y-2 py-4">
                <p className="font-medium">
                  {c.actionNumber} · {c.title}
                </p>
                <p className="text-sm text-muted-foreground">
                  {c.status}
                  {c.isOverdue ? ` · ${t('audits.capa.overdue')}` : ''}
                </p>
                {can('finding.manage') ? (
                  <div className="flex flex-wrap gap-2">
                    {c.status === 'Open' ? (
                      <Button
                        type="button"
                        size="sm"
                        onClick={() =>
                          capaTransitionMutation.mutate({ capaId: c.id, status: 'InProgress' })
                        }
                      >
                        InProgress
                      </Button>
                    ) : null}
                    {c.status === 'InProgress' ? (
                      <Button
                        type="button"
                        size="sm"
                        onClick={() =>
                          capaTransitionMutation.mutate({ capaId: c.id, status: 'Completed' })
                        }
                      >
                        Completed
                      </Button>
                    ) : null}
                    {c.status === 'Completed' ? (
                      <Button
                        type="button"
                        size="sm"
                        onClick={() =>
                          capaTransitionMutation.mutate({ capaId: c.id, status: 'Verified' })
                        }
                      >
                        Verified
                      </Button>
                    ) : null}
                  </div>
                ) : null}
              </CardContent>
            </Card>
          ))}
          {can('finding.manage') ? (
            <div className="space-y-2">
              <Select value={selectedFindingId} onValueChange={setSelectedFindingId}>
                <SelectTrigger>
                  <SelectValue placeholder={t('audits.fields.finding')} />
                </SelectTrigger>
                <SelectContent>
                  {(findingsQuery.data ?? []).map((f) => (
                    <SelectItem key={f.id} value={f.id}>
                      {f.findingNumber}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <Input value={capaTitle} onChange={(e) => setCapaTitle(e.target.value)} placeholder={t('audits.fields.capaTitle')} />
              <Input value={capaDesc} onChange={(e) => setCapaDesc(e.target.value)} placeholder={t('audits.fields.capaDescription')} />
              <Button
                type="button"
                disabled={!selectedFindingId || !capaTitle.trim() || !capaDesc.trim() || !user}
                onClick={() => createCapaMutation.mutate()}
              >
                {t('audits.addCapa')}
              </Button>
            </div>
          ) : null}
        </div>
      ) : null}

      {tab === 'export' ? (
        <Card>
          <CardHeader>
            <CardTitle>{t('audits.tabs.export')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <p className="text-sm text-muted-foreground">{t('audits.export.note')}</p>
            {can('evidence.export') ? (
              <Button
                type="button"
                onClick={async () => {
                  const blob = await auditsApi.exportPack(id)
                  const url = URL.createObjectURL(blob)
                  const a = document.createElement('a')
                  a.href = url
                  a.download = `${item.auditNumber}-pack.zip`
                  a.click()
                  URL.revokeObjectURL(url)
                }}
              >
                {t('audits.export.download')}
              </Button>
            ) : (
              <p className="text-sm text-muted-foreground">{t('audits.export.denied')}</p>
            )}
          </CardContent>
        </Card>
      ) : null}
    </div>
  )
}
