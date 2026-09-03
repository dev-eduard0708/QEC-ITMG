import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import type { ColumnDef } from '@tanstack/react-table'
import { ApiError, kbApi, type KnowledgeArticle } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { DataTable } from '@/components/shared/data-table'
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

const schema = z.object({
  title: z.string().trim().min(1),
  slug: z.string().trim().min(1),
  summary: z.string().optional(),
  body: z.string().trim().min(1),
})

type FormValues = z.infer<typeof schema>

export function ItKnowledgePage() {
  const { t } = useTranslation()
  const { can } = useAuth()
  const queryClient = useQueryClient()
  const [status, setStatus] = useState('all')
  const [search, setSearch] = useState('')
  const [editorOpen, setEditorOpen] = useState(false)
  const [editing, setEditing] = useState<KnowledgeArticle | null>(null)
  const [formError, setFormError] = useState<string | null>(null)

  const listQuery = useQuery({
    queryKey: ['kb', 'admin', status, search],
    queryFn: () =>
      kbApi.listAdmin({
        status: status === 'all' ? undefined : status,
        search: search || undefined,
      }),
    enabled: can('kb.read'),
  })

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { title: '', slug: '', summary: '', body: '' },
  })

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['kb'] })
  }

  const saveMutation = useMutation({
    mutationFn: (values: FormValues) => {
      const payload = {
        title: values.title,
        slug: values.slug,
        body: values.body,
        summary: values.summary?.trim() || null,
      }
      return editing ? kbApi.update(editing.id, payload) : kbApi.create(payload)
    },
    onSuccess: async () => {
      setEditorOpen(false)
      setEditing(null)
      setFormError(null)
      form.reset({ title: '', slug: '', summary: '', body: '' })
      await refresh()
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('kb.error.generic'))
    },
  })

  const publishMutation = useMutation({
    mutationFn: (id: string) => kbApi.publish(id),
    onSuccess: refresh,
  })

  const archiveMutation = useMutation({
    mutationFn: (id: string) => kbApi.archive(id),
    onSuccess: refresh,
  })

  const columns = useMemo<ColumnDef<KnowledgeArticle, unknown>[]>(
    () => [
      { accessorKey: 'title', header: t('kb.columns.title') },
      { accessorKey: 'slug', header: t('kb.columns.slug') },
      {
        accessorKey: 'status',
        header: t('kb.columns.status'),
        cell: ({ row }) => <Badge variant="secondary">{row.original.status}</Badge>,
      },
      {
        id: 'actions',
        header: t('kb.columns.actions'),
        cell: ({ row }) => (
          <div className="flex flex-wrap gap-2">
            {can('kb.manage') ? (
              <>
                <Button
                  size="sm"
                  variant="outline"
                  onClick={(event) => {
                    event.stopPropagation()
                    setEditing(row.original)
                    form.reset({
                      title: row.original.title,
                      slug: row.original.slug,
                      summary: row.original.summary ?? '',
                      body: row.original.body,
                    })
                    setEditorOpen(true)
                  }}
                >
                  {t('kb.edit')}
                </Button>
                {row.original.status !== 'Published' && row.original.status !== 'Archived' ? (
                  <Button
                    size="sm"
                    variant="secondary"
                    onClick={(event) => {
                      event.stopPropagation()
                      publishMutation.mutate(row.original.id)
                    }}
                  >
                    {t('kb.publish')}
                  </Button>
                ) : null}
                {row.original.status !== 'Archived' ? (
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={(event) => {
                      event.stopPropagation()
                      archiveMutation.mutate(row.original.id)
                    }}
                  >
                    {t('kb.archive')}
                  </Button>
                ) : null}
              </>
            ) : null}
          </div>
        ),
      },
    ],
    [archiveMutation, can, form, publishMutation, t],
  )

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('kb.adminTitle')}
        description={t('kb.adminDescription')}
        actions={
          can('kb.manage') ? (
            <Button
              onClick={() => {
                setEditing(null)
                form.reset({ title: '', slug: '', summary: '', body: '' })
                setEditorOpen(true)
              }}
            >
              {t('kb.create')}
            </Button>
          ) : null
        }
      />

      <div className="flex flex-wrap gap-3">
        <Input
          className="max-w-xs"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          placeholder={t('kb.searchPlaceholder')}
        />
        <Select value={status} onValueChange={setStatus}>
          <SelectTrigger className="w-[160px]">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {['all', 'Draft', 'Published', 'Archived'].map((option) => (
              <SelectItem key={option} value={option}>
                {option === 'all' ? t('kb.filters.all') : option}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <DataTable
        columns={columns}
        data={listQuery.data ?? []}
        isLoading={listQuery.isLoading}
        emptyMessage={t('kb.empty')}
      />

      <Dialog open={editorOpen} onOpenChange={setEditorOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>{editing ? t('kb.editTitle') : t('kb.createTitle')}</DialogTitle>
          </DialogHeader>
          <form
            className="space-y-3"
            onSubmit={form.handleSubmit((values) => saveMutation.mutate(values))}
          >
            <div className="space-y-1">
              <Label htmlFor="kb-title">{t('kb.fields.title')}</Label>
              <Input id="kb-title" {...form.register('title')} />
            </div>
            <div className="space-y-1">
              <Label htmlFor="kb-slug">{t('kb.fields.slug')}</Label>
              <Input id="kb-slug" {...form.register('slug')} />
            </div>
            <div className="space-y-1">
              <Label htmlFor="kb-summary">{t('kb.fields.summary')}</Label>
              <Input id="kb-summary" {...form.register('summary')} />
            </div>
            <div className="space-y-1">
              <Label htmlFor="kb-body">{t('kb.fields.body')}</Label>
              <textarea
                id="kb-body"
                className="min-h-40 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                {...form.register('body')}
              />
            </div>
            {formError ? <p className="text-sm text-destructive">{formError}</p> : null}
            <DialogFooter>
              <Button type="submit" disabled={saveMutation.isPending}>
                {t('kb.save')}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  )
}
