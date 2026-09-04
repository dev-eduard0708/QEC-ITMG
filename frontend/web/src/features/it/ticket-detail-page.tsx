import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo, useState, type ChangeEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { adminApi, ApiError, ticketsApi, evidenceApi } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { Timeline, type TimelineItem } from '@/components/shared/timeline'
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
import { Skeleton } from '@/components/ui/skeleton'
import { ticketKeys } from '@/features/it/query-keys'

const statuses = ['New', 'Open', 'InProgress', 'PendingRequester', 'Resolved', 'Closed', 'Cancelled'] as const

export function TicketDetailPage() {
  const { id = '' } = useParams()
  const { t } = useTranslation()
  const { can } = useAuth()
  const queryClient = useQueryClient()
  const [status, setStatus] = useState('')
  const [queueId, setQueueId] = useState('')
  const [assigneeId, setAssigneeId] = useState('')
  const [comment, setComment] = useState('')
  const [visibility, setVisibility] = useState<'EmployeeVisible' | 'Internal'>('EmployeeVisible')
  const [formError, setFormError] = useState<string | null>(null)
  const [majorIncident, setMajorIncident] = useState<boolean | null>(null)
  const [securityClassification, setSecurityClassification] = useState<string | null>(null)

  const ticketQuery = useQuery({
    queryKey: ticketKeys.detail(id),
    queryFn: () => ticketsApi.get(id),
    enabled: Boolean(id),
  })
  const relatedProblemsQuery = useQuery({
    queryKey: ticketKeys.relatedProblems(id),
    queryFn: () => ticketsApi.listRelatedProblems(id),
    enabled: Boolean(id) && ticketQuery.data?.type === 'Incident',
  })
  const queuesQuery = useQuery({
    queryKey: ticketKeys.queues(),
    queryFn: () => ticketsApi.listQueues(),
  })
  const usersQuery = useQuery({
    queryKey: ['admin', 'users', 'ticket-assign'],
    queryFn: () => adminApi.listUsers(),
    enabled: can('admin.users') && can('tickets.manage'),
  })
  const commentsQuery = useQuery({
    queryKey: ticketKeys.comments(id, 'it'),
    queryFn: () => ticketsApi.listComments(id),
    enabled: Boolean(id),
  })
  const attachmentsQuery = useQuery({
    queryKey: ticketKeys.attachments(id, 'it'),
    queryFn: () => ticketsApi.listAttachments(id),
    enabled: Boolean(id),
  })
  const timelineQuery = useQuery({
    queryKey: ticketKeys.timeline(id, 'it'),
    queryFn: () => ticketsApi.listTimeline(id),
    enabled: Boolean(id),
  })

  const refresh = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ticketKeys.detail(id) }),
      queryClient.invalidateQueries({ queryKey: ticketKeys.comments(id, 'it') }),
      queryClient.invalidateQueries({ queryKey: ticketKeys.attachments(id, 'it') }),
      queryClient.invalidateQueries({ queryKey: ticketKeys.timeline(id, 'it') }),
      queryClient.invalidateQueries({ queryKey: ticketKeys.all }),
    ])
  }

  const statusMutation = useMutation({
    mutationFn: () =>
      ticketsApi.changeStatus(id, status || ticketQuery.data?.status || 'Open', ticketQuery.data?.rowVersion),
    onSuccess: async () => {
      setFormError(null)
      await refresh()
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('tickets.error.generic'))
    },
  })

  const assignMutation = useMutation({
    mutationFn: () =>
      ticketsApi.assign(id, {
        queueId: queueId || ticketQuery.data?.queueId || null,
        assignedUserId: assigneeId || ticketQuery.data?.assignedUserId || null,
      }),
    onSuccess: async () => {
      setFormError(null)
      await refresh()
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('tickets.error.generic'))
    },
  })

  const commentMutation = useMutation({
    mutationFn: () => ticketsApi.addComment(id, comment, visibility),
    onSuccess: async () => {
      setComment('')
      setFormError(null)
      await refresh()
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('tickets.error.generic'))
    },
  })

  const uploadMutation = useMutation({
    mutationFn: (file: File) => ticketsApi.uploadAttachment(id, file),
    onSuccess: async () => {
      setFormError(null)
      await refresh()
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('tickets.error.generic'))
    },
  })

  const incidentMutation = useMutation({
    mutationFn: () => {
      const ticketData = ticketQuery.data
      if (!ticketData) throw new Error('missing ticket')
      return ticketsApi.updateIncident(id, {
        isMajorIncident: majorIncident ?? ticketData.isMajorIncident,
        securityClassification: can('incidents.security')
          ? (securityClassification ?? ticketData.securityClassification ?? 'None')
          : undefined,
        rowVersion: ticketData.rowVersion,
      })
    },
    onSuccess: async () => {
      setMajorIncident(null)
      setSecurityClassification(null)
      setFormError(null)
      await refresh()
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('tickets.error.generic'))
    },
  })

  const timelineItems = useMemo<TimelineItem[]>(
    () =>
      (timelineQuery.data ?? []).map((item) => ({
        id: item.id,
        timestamp: item.timestamp,
        title: item.title,
        description: item.description,
        actor: item.actor ? item.actor.slice(0, 8) : undefined,
        status: item.status ?? undefined,
        type: item.type,
      })),
    [timelineQuery.data],
  )

  if (ticketQuery.isLoading) {
    return <Skeleton className="h-40 w-full" />
  }

  const ticket = ticketQuery.data
  if (!ticket) {
    return <p className="text-sm text-muted-foreground">{t('tickets.notFound')}</p>
  }

  const onFileChange = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    if (file) {
      uploadMutation.mutate(file)
      event.target.value = ''
    }
  }

  const incidentMajor = majorIncident ?? ticket.isMajorIncident
  const incidentSecurity = securityClassification ?? ticket.securityClassification ?? 'None'

  return (
    <div className="space-y-6">
      <PageHeader
        title={ticket.ticketNumber}
        description={`${ticket.type} · ${ticket.title}`}
        actions={
          <div className="flex flex-wrap gap-2">
            <Button asChild variant="outline">
              <Link to="/it/tickets">{t('tickets.back')}</Link>
            </Button>
            {can('evidence.upload') ? (
              <Button
                type="button"
                variant="secondary"
                onClick={async () => {
                  const created = await evidenceApi.promote({
                    title: `Ticket ${ticket.ticketNumber}`,
                    sourceType: 'Ticket',
                    sourceRecordId: ticket.id,
                    evidenceType: 'Document',
                    description: ticket.title,
                  })
                  window.location.href = `/it/evidence/${created.id}`
                }}
              >
                {t('evidence.promote')}
              </Button>
            ) : null}
          </div>
        }
      />

      <div className="flex flex-wrap gap-2">
        <Badge variant="secondary">{ticket.type}</Badge>
        <Badge>{ticket.status}</Badge>
        <Badge variant="outline">{ticket.priority}</Badge>
        {ticket.isMajorIncident ? <Badge variant="warning">{t('tickets.incident.majorBadge')}</Badge> : null}
        {(ticket.responseBreached || ticket.resolutionBreached) && (
          <Badge variant="warning">{t('tickets.sla.breached')}</Badge>
        )}
      </div>

      {ticket.type === 'Incident' ? (
        <section className="space-y-3 rounded-md border border-border p-4">
          <h2 className="text-sm font-semibold">{t('tickets.incident.title')}</h2>
          {can('tickets.manage') ? (
            <div className="flex flex-wrap items-end gap-4">
              <label className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={incidentMajor}
                  onChange={(event) => setMajorIncident(event.target.checked)}
                />
                {t('tickets.incident.major')}
              </label>
              {can('incidents.security') ? (
                <div className="space-y-1">
                  <Label>{t('tickets.incident.security')}</Label>
                  <Select value={incidentSecurity} onValueChange={setSecurityClassification}>
                    <SelectTrigger className="w-[180px]">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {['None', 'Suspected', 'Confirmed'].map((item) => (
                        <SelectItem key={item} value={item}>
                          {item}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              ) : null}
              <Button type="button" onClick={() => incidentMutation.mutate()} disabled={incidentMutation.isPending}>
                {t('tickets.incident.save')}
              </Button>
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">
              {ticket.isMajorIncident ? t('tickets.incident.majorBadge') : t('tickets.incident.notMajor')}
              {can('incidents.security') && ticket.securityClassification
                ? ` · ${ticket.securityClassification}`
                : null}
            </p>
          )}
        </section>
      ) : null}

      <section className="grid gap-4 md:grid-cols-2">
        <div className="space-y-2 text-sm">
          <p>
            <span className="text-muted-foreground">{t('tickets.columns.requester')}: </span>
            {ticket.requesterUserId}
          </p>
          <p className="whitespace-pre-wrap">{ticket.description}</p>
          <p>
            <span className="text-muted-foreground">{t('tickets.fields.linkedCi')}: </span>
            {ticket.configurationItemId ?? t('tickets.none')}
          </p>
          <p>
            <span className="text-muted-foreground">{t('tickets.columns.queue')}: </span>
            {queuesQuery.data?.find((queue) => queue.id === ticket.queueId)?.name ?? ticket.queueId ?? '—'}
          </p>
          <p>
            <span className="text-muted-foreground">{t('tickets.columns.assignee')}: </span>
            {ticket.assignedUserId ?? '—'}
          </p>
          <p>
            <span className="text-muted-foreground">{t('tickets.fields.responseDue')}: </span>
            {ticket.responseDueAtUtc ? new Date(ticket.responseDueAtUtc).toLocaleString() : '—'}
          </p>
          <p>
            <span className="text-muted-foreground">{t('tickets.fields.resolutionDue')}: </span>
            {ticket.resolutionDueAtUtc ? new Date(ticket.resolutionDueAtUtc).toLocaleString() : '—'}
          </p>
        </div>

        {can('tickets.manage') ? (
          <div className="space-y-4 rounded-md border border-border p-4">
            <div className="space-y-2">
              <Label>{t('tickets.actions.status')}</Label>
              <div className="flex flex-wrap gap-2">
                <Select value={status || ticket.status} onValueChange={setStatus}>
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
                  {t('tickets.actions.applyStatus')}
                </Button>
              </div>
            </div>

            <div className="space-y-2">
              <Label>{t('tickets.actions.assign')}</Label>
              <Select value={queueId || ticket.queueId || 'none'} onValueChange={(value) => setQueueId(value === 'none' ? '' : value)}>
                <SelectTrigger>
                  <SelectValue placeholder={t('tickets.fields.queuePlaceholder')} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">{t('tickets.none')}</SelectItem>
                  {(queuesQuery.data ?? []).map((queue) => (
                    <SelectItem key={queue.id} value={queue.id}>
                      {queue.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {can('admin.users') ? (
                <Select
                  value={assigneeId || ticket.assignedUserId || 'none'}
                  onValueChange={(value) => setAssigneeId(value === 'none' ? '' : value)}
                >
                  <SelectTrigger>
                    <SelectValue placeholder={t('tickets.fields.assigneePlaceholder')} />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">{t('tickets.none')}</SelectItem>
                    {(usersQuery.data ?? []).map((user) => (
                      <SelectItem key={user.id} value={user.id}>
                        {user.displayName}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              ) : (
                <Input
                  value={assigneeId || ticket.assignedUserId || ''}
                  onChange={(event) => setAssigneeId(event.target.value)}
                  placeholder={t('tickets.fields.assigneeId')}
                />
              )}
              <Button type="button" onClick={() => assignMutation.mutate()} disabled={assignMutation.isPending}>
                {t('tickets.actions.applyAssign')}
              </Button>
            </div>
          </div>
        ) : null}
      </section>

      <section className="space-y-3">
        <h2 className="text-sm font-semibold">{t('tickets.comments')}</h2>
        <div className="space-y-2">
          {(commentsQuery.data ?? []).map((item) => (
            <div key={item.id} className="rounded-md border border-border px-3 py-2 text-sm">
              <div className="mb-1 flex flex-wrap gap-2">
                <Badge variant={item.visibility === 'Internal' ? 'warning' : 'secondary'}>
                  {item.visibility}
                </Badge>
              </div>
              <p className="whitespace-pre-wrap">{item.body}</p>
              <p className="mt-1 text-xs text-muted-foreground">
                {new Date(item.createdAtUtc).toLocaleString()}
              </p>
            </div>
          ))}
        </div>
        {can('tickets.manage') ? (
          <div className="space-y-2">
            <Label htmlFor="it-comment">{t('tickets.addComment')}</Label>
            <textarea
              id="it-comment"
              className="min-h-20 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
              value={comment}
              onChange={(event) => setComment(event.target.value)}
            />
            <Select value={visibility} onValueChange={(value) => setVisibility(value as typeof visibility)}>
              <SelectTrigger className="w-[200px]">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="EmployeeVisible">{t('tickets.visibility.employee')}</SelectItem>
                <SelectItem value="Internal">{t('tickets.visibility.internal')}</SelectItem>
              </SelectContent>
            </Select>
            <Button
              type="button"
              disabled={!comment.trim() || commentMutation.isPending}
              onClick={() => commentMutation.mutate()}
            >
              {t('tickets.postComment')}
            </Button>
          </div>
        ) : null}
      </section>

      <section className="space-y-3">
        <h2 className="text-sm font-semibold">{t('tickets.attachments')}</h2>
        <ul className="space-y-2 text-sm">
          {(attachmentsQuery.data ?? []).map((item) => (
            <li key={item.id} className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-border px-3 py-2">
              <div>
                <a
                  className="font-medium text-primary underline-offset-2 hover:underline"
                  href={ticketsApi.attachmentContentUrl(id, item.id)}
                  target="_blank"
                  rel="noreferrer"
                >
                  {item.fileName}
                </a>
                <p className="text-xs text-muted-foreground">
                  {item.sizeBytes} bytes · {item.scanStatus}
                </p>
              </div>
            </li>
          ))}
        </ul>
        {can('tickets.manage') ? (
          <div className="space-y-2">
            <Label htmlFor="it-upload">{t('tickets.upload')}</Label>
            <Input id="it-upload" type="file" onChange={onFileChange} />
          </div>
        ) : null}
      </section>

      {ticket.type === 'Incident' ? (
        <section className="space-y-3">
          <h2 className="text-sm font-semibold">{t('tickets.relatedProblems')}</h2>
          {(relatedProblemsQuery.data ?? []).length === 0 ? (
            <p className="text-sm text-muted-foreground">{t('tickets.relatedProblemsEmpty')}</p>
          ) : (
            <ul className="space-y-2 text-sm">
              {(relatedProblemsQuery.data ?? []).map((item) => (
                <li key={item.problemId} className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-border px-3 py-2">
                  <div>
                    <Link className="font-medium text-primary underline-offset-2 hover:underline" to={`/it/problems/${item.problemId}`}>
                      {item.problemNumber}
                    </Link>
                    <p className="text-muted-foreground">{item.title}</p>
                  </div>
                  <Badge variant="secondary">{item.status}</Badge>
                </li>
              ))}
            </ul>
          )}
        </section>
      ) : null}

      <section className="space-y-3">
        <h2 className="text-sm font-semibold">{t('tickets.timeline')}</h2>
        <Timeline items={timelineItems} emptyMessage={t('tickets.timelineEmpty')} />
      </section>

      {formError ? <p className="text-sm text-destructive">{formError}</p> : null}
    </div>
  )
}
