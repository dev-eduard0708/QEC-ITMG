import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo, useState, type ChangeEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { ApiError, meApi } from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Timeline, type TimelineItem } from '@/components/shared/timeline'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { ticketKeys } from '@/features/it/query-keys'

export function RequestDetailPage() {
  const { id = '' } = useParams()
  const { t } = useTranslation()
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
    return <p className="text-sm text-muted-foreground">{t('requests.notFound')}</p>
  }

  const onFileChange = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    if (file) {
      uploadMutation.mutate(file)
      event.target.value = ''
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={ticket.ticketNumber}
        description={ticket.title}
        actions={
          <Button asChild variant="outline">
            <Link to="/employee/requests">{t('requests.back')}</Link>
          </Button>
        }
      />

      <div className="flex flex-wrap gap-2">
        <Badge variant="secondary">{ticket.type}</Badge>
        <Badge>{ticket.status}</Badge>
        <Badge variant="outline">{ticket.priority}</Badge>
      </div>

      <section className="space-y-2">
        <h2 className="text-sm font-semibold">{t('requests.fields.description')}</h2>
        <p className="whitespace-pre-wrap text-sm text-muted-foreground">{ticket.description}</p>
        {ticket.configurationItemId ? (
          <p className="text-sm">
            <span className="text-muted-foreground">{t('requests.fields.relatedCi')}: </span>
            {ticket.configurationItemId}
          </p>
        ) : null}
      </section>

      <section className="space-y-3">
        <h2 className="text-sm font-semibold">{t('requests.comments')}</h2>
        <div className="space-y-2">
          {(commentsQuery.data ?? []).map((item) => (
            <div key={item.id} className="rounded-md border border-border px-3 py-2 text-sm">
              <p className="whitespace-pre-wrap">{item.body}</p>
              <p className="mt-1 text-xs text-muted-foreground">
                {new Date(item.createdAtUtc).toLocaleString()}
              </p>
            </div>
          ))}
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
            <li key={item.id} className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-border px-3 py-2">
              <div>
                <a
                  className="font-medium text-primary underline-offset-2 hover:underline"
                  href={meApi.ticketAttachmentContentUrl(id, item.id)}
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
        <div className="space-y-2">
          <Label htmlFor="upload">{t('requests.upload')}</Label>
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
