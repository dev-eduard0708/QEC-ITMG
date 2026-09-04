import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { ApiError, problemsApi, ticketsApi } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { problemKeys, ticketKeys } from '@/features/it/query-keys'

const statuses = ['New', 'Investigating', 'Resolved', 'Closed'] as const
const priorities = ['Low', 'Medium', 'High', 'Critical'] as const

export function ProblemDetailPage() {
  const { id = '' } = useParams()
  const { t } = useTranslation()
  const { can } = useAuth()
  const queryClient = useQueryClient()
  const [status, setStatus] = useState('')
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [priority, setPriority] = useState('')
  const [ownerUserId, setOwnerUserId] = useState('')
  const [configurationItemId, setConfigurationItemId] = useState('')
  const [rootCause, setRootCause] = useState('')
  const [workaround, setWorkaround] = useState('')
  const [formError, setFormError] = useState<string | null>(null)
  const [linkOpen, setLinkOpen] = useState(false)
  const [incidentSearch, setIncidentSearch] = useState('')

  const problemQuery = useQuery({
    queryKey: problemKeys.detail(id),
    queryFn: () => problemsApi.get(id),
    enabled: Boolean(id),
  })
  const incidentsQuery = useQuery({
    queryKey: problemKeys.incidents(id),
    queryFn: () => problemsApi.listIncidents(id),
    enabled: Boolean(id),
  })
  const metricsQuery = useQuery({
    queryKey: problemKeys.metrics(id),
    queryFn: () => problemsApi.metrics(id),
    enabled: Boolean(id),
  })
  const incidentSearchQuery = useQuery({
    queryKey: [...ticketKeys.all, 'link-search', incidentSearch],
    queryFn: () =>
      ticketsApi.list({
        pageSize: 20,
        type: 'Incident',
        search: incidentSearch || undefined,
      }),
    enabled: linkOpen,
  })

  const problem = problemQuery.data

  useEffect(() => {
    if (!problem) return
    setTitle(problem.title)
    setDescription(problem.description)
    setPriority(problem.priority)
    setOwnerUserId(problem.ownerUserId ?? '')
    setConfigurationItemId(problem.configurationItemId ?? '')
    setRootCause(problem.rootCause ?? '')
    setWorkaround(problem.workaround ?? '')
  }, [problem])

  const refresh = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: problemKeys.detail(id) }),
      queryClient.invalidateQueries({ queryKey: problemKeys.incidents(id) }),
      queryClient.invalidateQueries({ queryKey: problemKeys.metrics(id) }),
      queryClient.invalidateQueries({ queryKey: problemKeys.all }),
    ])
  }

  const statusMutation = useMutation({
    mutationFn: () =>
      problemsApi.changeStatus(id, status || problem?.status || 'New', problem?.rowVersion),
    onSuccess: async () => {
      setFormError(null)
      await refresh()
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('problems.error.generic'))
    },
  })

  const saveMutation = useMutation({
    mutationFn: () =>
      problemsApi.update(id, {
        title,
        description,
        priority: priority || problem?.priority || 'Medium',
        ownerUserId: ownerUserId || null,
        configurationItemId: configurationItemId || null,
        rootCause: rootCause || null,
        workaround: workaround || null,
        rowVersion: problem?.rowVersion,
      }),
    onSuccess: async () => {
      setFormError(null)
      await refresh()
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('problems.error.generic'))
    },
  })

  const linkMutation = useMutation({
    mutationFn: (incidentTicketId: string) => problemsApi.linkIncident(id, incidentTicketId),
    onSuccess: async () => {
      setLinkOpen(false)
      setFormError(null)
      await refresh()
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('problems.error.generic'))
    },
  })

  const unlinkMutation = useMutation({
    mutationFn: (ticketId: string) => problemsApi.unlinkIncident(id, ticketId),
    onSuccess: async () => {
      setFormError(null)
      await refresh()
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('problems.error.generic'))
    },
  })

  const knownErrorMutation = useMutation({
    mutationFn: (isKnownError: boolean) =>
      problemsApi.setKnownError(id, isKnownError, problemQuery.data?.rowVersion),
    onSuccess: async () => {
      setFormError(null)
      await refresh()
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('problems.error.generic'))
    },
  })

  if (problemQuery.isLoading) {
    return <Skeleton className="h-40 w-full" />
  }

  if (!problem) {
    return <p className="text-sm text-muted-foreground">{t('problems.notFound')}</p>
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={problem.problemNumber}
        description={problem.title}
        actions={
          <Button asChild variant="outline">
            <Link to="/it/problems">{t('problems.back')}</Link>
          </Button>
        }
      />

      <div className="flex flex-wrap gap-2">
        <Badge>{problem.status}</Badge>
        <Badge variant="outline">{problem.priority}</Badge>
        {problem.isKnownError ? <Badge variant="warning">{t('problems.knownError.badge')}</Badge> : null}
      </div>

      {metricsQuery.data ? (
        <section className="grid gap-2 rounded-md border border-border p-4 text-sm sm:grid-cols-3">
          <h2 className="sm:col-span-3 text-sm font-semibold">{t('problems.metrics.title')}</h2>
          <p>
            <span className="text-muted-foreground">{t('problems.metrics.linked')}: </span>
            {metricsQuery.data.linkedIncidentCount}
          </p>
          <p>
            <span className="text-muted-foreground">{t('problems.metrics.open')}: </span>
            {metricsQuery.data.openLinkedIncidents}
          </p>
          <p>
            <span className="text-muted-foreground">{t('problems.metrics.major')}: </span>
            {metricsQuery.data.majorLinkedIncidents}
          </p>
          <p>
            <span className="text-muted-foreground">{t('problems.metrics.first')}: </span>
            {metricsQuery.data.firstOccurrenceUtc
              ? new Date(metricsQuery.data.firstOccurrenceUtc).toLocaleString()
              : '—'}
          </p>
          <p>
            <span className="text-muted-foreground">{t('problems.metrics.latest')}: </span>
            {metricsQuery.data.latestOccurrenceUtc
              ? new Date(metricsQuery.data.latestOccurrenceUtc).toLocaleString()
              : '—'}
          </p>
          <p>
            <span className="text-muted-foreground">
              {t('problems.metrics.recent', { days: metricsQuery.data.recentWindowDays })}:{' '}
            </span>
            {metricsQuery.data.recentOccurrenceCount}
          </p>
        </section>
      ) : null}

      <section className="grid gap-4 md:grid-cols-2">
        <div className="space-y-3 text-sm">
          <p className="whitespace-pre-wrap">{problem.description}</p>
          <p>
            <span className="text-muted-foreground">{t('problems.columns.owner')}: </span>
            {problem.ownerUserId ?? '—'}
          </p>
          <p>
            <span className="text-muted-foreground">{t('problems.columns.ci')}: </span>
            {problem.configurationItemId ?? '—'}
          </p>
          <div>
            <p className="text-muted-foreground">{t('problems.fields.rootCause')}</p>
            <p className="whitespace-pre-wrap">{problem.rootCause ?? '—'}</p>
          </div>
          <div>
            <p className="text-muted-foreground">{t('problems.fields.workaround')}</p>
            <p className="whitespace-pre-wrap">{problem.workaround ?? '—'}</p>
          </div>
        </div>

        {can('problems.manage') ? (
          <div className="space-y-4 rounded-md border border-border p-4">
            <div className="space-y-2">
              <Label>{t('problems.actions.status')}</Label>
              <div className="flex flex-wrap gap-2">
                <Select value={status || problem.status} onValueChange={setStatus}>
                  <SelectTrigger className="w-[180px]">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {statuses.map((item) => (
                      <SelectItem key={item} value={item}>
                        {item}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <Button type="button" onClick={() => statusMutation.mutate()} disabled={statusMutation.isPending}>
                  {t('problems.actions.applyStatus')}
                </Button>
              </div>
            </div>

            <div className="space-y-2">
              <Label>{t('problems.knownError.title')}</Label>
              <Button
                type="button"
                variant="secondary"
                disabled={knownErrorMutation.isPending}
                onClick={() => knownErrorMutation.mutate(!problem.isKnownError)}
              >
                {problem.isKnownError ? t('problems.knownError.clear') : t('problems.knownError.mark')}
              </Button>
            </div>

            <div className="space-y-2">
              <Label htmlFor="edit-title">{t('problems.fields.title')}</Label>
              <Input id="edit-title" value={title} onChange={(event) => setTitle(event.target.value)} />
              <Label htmlFor="edit-description">{t('problems.fields.description')}</Label>
              <textarea
                id="edit-description"
                className="min-h-20 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                value={description}
                onChange={(event) => setDescription(event.target.value)}
              />
              <Label>{t('problems.fields.priority')}</Label>
              <Select value={priority || problem.priority} onValueChange={setPriority}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {priorities.map((item) => (
                    <SelectItem key={item} value={item}>
                      {item}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <Label htmlFor="edit-owner">{t('problems.fields.owner')}</Label>
              <Input id="edit-owner" value={ownerUserId} onChange={(event) => setOwnerUserId(event.target.value)} />
              <Label htmlFor="edit-ci">{t('problems.fields.ci')}</Label>
              <Input
                id="edit-ci"
                value={configurationItemId}
                onChange={(event) => setConfigurationItemId(event.target.value)}
              />
              <Label htmlFor="edit-root">{t('problems.fields.rootCause')}</Label>
              <textarea
                id="edit-root"
                className="min-h-16 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                value={rootCause}
                onChange={(event) => setRootCause(event.target.value)}
              />
              <Label htmlFor="edit-workaround">{t('problems.fields.workaround')}</Label>
              <textarea
                id="edit-workaround"
                className="min-h-16 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                value={workaround}
                onChange={(event) => setWorkaround(event.target.value)}
              />
              <Button type="button" onClick={() => saveMutation.mutate()} disabled={saveMutation.isPending}>
                {t('problems.actions.save')}
              </Button>
            </div>
          </div>
        ) : null}
      </section>

      <section className="space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 className="text-sm font-semibold">{t('problems.linkedIncidents')}</h2>
          {can('problems.manage') ? (
            <Button type="button" variant="secondary" onClick={() => setLinkOpen(true)}>
              {t('problems.linkIncident')}
            </Button>
          ) : null}
        </div>
        {(incidentsQuery.data ?? []).length === 0 ? (
          <p className="text-sm text-muted-foreground">{t('problems.linkedIncidentsEmpty')}</p>
        ) : (
          <ul className="space-y-2 text-sm">
            {(incidentsQuery.data ?? []).map((item) => (
              <li
                key={item.incidentTicketId}
                className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-border px-3 py-2"
              >
                <div>
                  <Link
                    className="font-medium text-primary underline-offset-2 hover:underline"
                    to={`/it/tickets/${item.incidentTicketId}`}
                  >
                    {item.ticketNumber}
                  </Link>
                  <p className="text-muted-foreground">{item.title}</p>
                </div>
                <div className="flex flex-wrap items-center gap-2">
                  {item.isMajorIncident ? (
                    <Badge variant="warning">{t('tickets.incident.majorBadge')}</Badge>
                  ) : null}
                  <Badge variant="secondary">{item.status}</Badge>
                  <Badge variant="outline">{item.priority}</Badge>
                  {can('problems.manage') ? (
                    <Button
                      type="button"
                      size="sm"
                      variant="outline"
                      onClick={() => unlinkMutation.mutate(item.incidentTicketId)}
                    >
                      {t('problems.unlink')}
                    </Button>
                  ) : null}
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>

      <Dialog open={linkOpen} onOpenChange={setLinkOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('problems.linkIncident')}</DialogTitle>
          </DialogHeader>
          <Input
            value={incidentSearch}
            onChange={(event) => setIncidentSearch(event.target.value)}
            placeholder={t('problems.searchIncidents')}
          />
          <ul className="max-h-64 space-y-2 overflow-auto text-sm">
            {(incidentSearchQuery.data?.items ?? []).map((ticket) => (
              <li key={ticket.id} className="flex items-center justify-between gap-2 rounded-md border border-border px-3 py-2">
                <div>
                  <p className="font-medium">{ticket.ticketNumber}</p>
                  <p className="text-muted-foreground">{ticket.title}</p>
                </div>
                <Button type="button" size="sm" onClick={() => linkMutation.mutate(ticket.id)}>
                  {t('problems.link')}
                </Button>
              </li>
            ))}
          </ul>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setLinkOpen(false)}>
              {t('problems.cancel')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {formError ? <p className="text-sm text-destructive">{formError}</p> : null}
    </div>
  )
}
