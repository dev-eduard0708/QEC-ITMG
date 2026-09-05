import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo, useState, type ChangeEvent } from 'react'
import { Link, useLocation, useParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { ApiError, meApi } from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Timeline, type TimelineItem } from '@/components/shared/timeline'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { equipmentKeys, ticketKeys } from '@/features/it/query-keys'
import {
  categoryLabelKey,
  formatDeviceLabel,
  friendlyStatusKey,
  friendlyTicketTypeKey,
} from '@/features/employee/employee-request-helpers'

type LocationState = {
  createdNumber?: string
  attachWarning?: string | null
}

export function RequestDetailPage() {
  const { id = '' } = useParams()
  const { t } = useTranslation()
  const location = useLocation()
  const state = (location.state as LocationState | null) ?? null
  const queryClient = useQueryClient()
  const [comment, setComment] = useState('')
  const [formError, setFormError] = useState<string | null>(null)

  const ticketQuery = useQuery({
    queryKey: ticketKeys.mineDetail(id),
    queryFn: () => meApi.getTicket(id),
    enabled: Boolean(id),
  })
  const commentsQuery = useQuery({
    queryKey: ticketKeys.comments(id, 'me'),
    queryFn: () => meApi.listTicketComments(id),
    enabled: Boolean(id),
  })
  const attachmentsQuery = useQuery({
    queryKey: ticketKeys.attachments(id, 'me'),
    queryFn: () => meApi.listTicketAttachments(id),
    enabled: Boolean(id),
  })
  const timelineQuery = useQuery({
    queryKey: ticketKeys.timeline(id, 'me'),
    queryFn: () => meApi.listTicketTimeline(id),
    enabled: Boolean(id),
  })
  const equipmentQuery = useQuery({
    queryKey: equipmentKeys.mine,
    queryFn: () => meApi.listEquipment(),
  })

  const refresh = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ticketKeys.mineDetail(id) }),
      queryClient.invalidateQueries({ queryKey: ticketKeys.comments(id, 'me') }),
      queryClient.invalidateQueries({ queryKey: ticketKeys.attachments(id, 'me') }),
      queryClient.invalidateQueries({ queryKey: ticketKeys.timeline(id, 'me') }),
      queryClient.invalidateQueries({ queryKey: ticketKeys.mine('') }),
    ])
  }

  const commentMutation = useMutation({
    mutationFn: () => meApi.addTicketComment(id, comment),
    onSuccess: async () => {
      setComment('')
      setFormError(null)
      await refresh()
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('requests.error.generic'))
    },
  })

  const uploadMutation = useMutation({
    mutationFn: (file: File) => meApi.uploadTicketAttachment(id, file),
    onSuccess: async () => {
      setFormError(null)
      await refresh()
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('requests.error.generic'))
    },
  })

  const timelineItems = useMemo<TimelineItem[]>(
    () =>
      (timelineQuery.data ?? []).map((item) => ({
        id: item.id,
        timestamp: item.timestamp,
        title: item.title,
        description: item.description,
        actor: item.actor ? t('employee.activity.someone') : undefined,
        status: item.status ?? undefined,
        type: item.type,
      })),
    [timelineQuery.data, t],
  )

  const affectedLabel = useMemo(() => {
    const ticket = ticketQuery.data
    if (!ticket?.configurationItemId) return null
    const match = (equipmentQuery.data ?? []).find(
      (asset) => asset.configurationItemId === ticket.configurationItemId,
    )
    if (match) return formatDeviceLabel(match)
    return t('employee.request.linkedDevice')
  }, [equipmentQuery.data, ticketQuery.data, t])

  if (ticketQuery.isLoading) {
    return <Skeleton className="h-40 w-full" />
  }

  const ticket = ticketQuery.data
  if (!ticket) {
    return <p className="text-sm text-muted-foreground">{t('requests.notFound')}</p>
  }

  const onFileChange = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    if (file) {
      uploadMutation.mutate(file)
      event.target.value = ''
    }
  }

  const categoryKey = categoryLabelKey(ticket.category)
  const comments = commentsQuery.data ?? []
  const latestComment = comments.length > 0 ? comments[comments.length - 1] : undefined

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <PageHeader
        title={ticket.ticketNumber}
        description={ticket.title}
        actions={
          <Button asChild variant="outline">
            <Link to="/employee/requests">{t('requests.back')}</Link>
          </Button>
        }
      />

      {state?.createdNumber ? (
        <div className="rounded-xl border border-primary/30 bg-primary/5 px-4 py-3 text-sm">
          {t('employee.request.createdNotice', { number: state.createdNumber })}
        </div>
      ) : null}
      {state?.attachWarning ? (
        <div className="rounded-xl border border-amber-500/40 bg-amber-500/10 px-4 py-3 text-sm">
          {state.attachWarning}
        </div>
      ) : null}

      <div className="flex flex-wrap gap-2">
        <Badge variant="secondary">{t(friendlyTicketTypeKey(ticket.type))}</Badge>
        <Badge>{t(friendlyStatusKey(ticket.status))}</Badge>
        {categoryKey ? <Badge variant="outline">{t(categoryKey)}</Badge> : null}
      </div>

      <section className="space-y-2 rounded-2xl border p-4">
        <h2 className="text-sm font-semibold">{t('employee.detail.whatYouToldUs')}</h2>
        <p className="whitespace-pre-wrap text-sm text-muted-foreground">{ticket.description}</p>
        {affectedLabel ? (
          <p className="text-sm">
            <span className="text-muted-foreground">{t('employee.detail.affected')}: </span>
            {affectedLabel}
          </p>
        ) : null}
        <p className="text-xs text-muted-foreground">
          {t('employee.createdAt', { date: new Date(ticket.createdAtUtc).toLocaleString() })}
        </p>
      </section>

      <section className="space-y-2 rounded-2xl border p-4">
        <h2 className="text-sm font-semibold">{t('employee.detail.latestUpdate')}</h2>
        {latestComment ? (
          <div className="text-sm">
            <p className="whitespace-pre-wrap">{latestComment.body}</p>
            <p className="mt-1 text-xs text-muted-foreground">
              {new Date(latestComment.createdAtUtc).toLocaleString()}
            </p>
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">{t('employee.detail.noUpdateYet')}</p>
        )}
      </section>

      <section className="space-y-3">
        <h2 className="text-sm font-semibold">{t('requests.comments')}</h2>
        <div className="space-y-2">
          {comments.map((item) => (
            <div key={item.id} className="rounded-xl border border-border px-3 py-2 text-sm">
              <p className="whitespace-pre-wrap">{item.body}</p>
              <p className="mt-1 text-xs text-muted-foreground">
                {new Date(item.createdAtUtc).toLocaleString()}
              </p>
            </div>
          ))}
          {comments.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t('employee.detail.noComments')}</p>
          ) : null}
        </div>
        <div className="space-y-2">
          <Label htmlFor="comment">{t('requests.addComment')}</Label>
          <textarea
            id="comment"
            className="min-h-20 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
            value={comment}
            onChange={(event) => setComment(event.target.value)}
          />
          <Button
            type="button"
            disabled={!comment.trim() || commentMutation.isPending}
            onClick={() => commentMutation.mutate()}
          >
            {t('requests.postComment')}
          </Button>
        </div>
      </section>

      <section className="space-y-3">
        <h2 className="text-sm font-semibold">{t('requests.attachments')}</h2>
        <ul className="space-y-2 text-sm">
          {(attachmentsQuery.data ?? []).map((item) => (
            <li
              key={item.id}
              className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-border px-3 py-2"
            >
              <a
                className="font-medium text-primary underline-offset-2 hover:underline"
                href={meApi.ticketAttachmentContentUrl(id, item.id)}
                target="_blank"
                rel="noreferrer"
              >
                {item.fileName}
              </a>
            </li>
          ))}
        </ul>
        <div className="space-y-2">
          <Label htmlFor="upload">{t('employee.request.attachment')}</Label>
          <Input id="upload" type="file" onChange={onFileChange} />
        </div>
      </section>

      <section className="space-y-3">
        <h2 className="text-sm font-semibold">{t('requests.timeline')}</h2>
        <Timeline items={timelineItems} emptyMessage={t('requests.timelineEmpty')} />
      </section>

      {formError ? <p className="text-sm text-destructive">{formError}</p> : null}
    </div>
  )
}
