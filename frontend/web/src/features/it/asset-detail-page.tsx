import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo, useState, type ReactNode } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import {
  adminApi,
  ApiError,
  assetsApi,
  type AssetAssignment,
} from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { Timeline, type TimelineItem } from '@/components/shared/timeline'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import {
  Dialog,
  DialogContent,
  DialogDescription,
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
import { assetKeys } from '@/features/it/query-keys'

const editSchema = z.object({
  assetType: z.string().trim().min(1),
  name: z.string().trim().min(1),
  status: z.string().trim().min(1),
  serialNumber: z.string().optional(),
  manufacturer: z.string().optional(),
  model: z.string().optional(),
  notes: z.string().optional(),
})

type EditForm = z.infer<typeof editSchema>

export function AssetDetailPage() {
  const { id = '' } = useParams()
  const { t } = useTranslation()
  const { can } = useAuth()
  const queryClient = useQueryClient()
  const [editOpen, setEditOpen] = useState(false)
  const [assignOpen, setAssignOpen] = useState(false)
  const [assigneeId, setAssigneeId] = useState('')
  const [formError, setFormError] = useState<string | null>(null)

  const assetQuery = useQuery({
    queryKey: assetKeys.detail(id),
    queryFn: () => assetsApi.get(id),
    enabled: Boolean(id),
  })

  const assignmentsQuery = useQuery({
    queryKey: assetKeys.assignments(id),
    queryFn: () => assetsApi.listAssignments(id),
    enabled: Boolean(id),
  })

  const usersQuery = useQuery({
    queryKey: ['admin', 'users', 'assign-picker'],
    queryFn: () => adminApi.listUsers(),
    enabled: can('admin.users') && assignOpen,
  })

  const editForm = useForm<EditForm>({
    resolver: zodResolver(editSchema),
  })

  const refresh = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: assetKeys.detail(id) }),
      queryClient.invalidateQueries({ queryKey: assetKeys.assignments(id) }),
      queryClient.invalidateQueries({ queryKey: assetKeys.all }),
    ])
  }

  const updateMutation = useMutation({
    mutationFn: (values: EditForm) => {
      const asset = assetQuery.data
      if (!asset) throw new Error('missing asset')
      return assetsApi.update(id, {
        assetType: values.assetType,
        name: values.name,
        status: values.status,
        serialNumber: values.serialNumber?.trim() || null,
        manufacturer: values.manufacturer?.trim() || null,
        model: values.model?.trim() || null,
        notes: values.notes?.trim() || null,
        configurationItemId: asset.configurationItemId,
        locationId: asset.locationId,
        rowVersion: asset.rowVersion,
      })
    },
    onSuccess: async () => {
      setEditOpen(false)
      setFormError(null)
      await refresh()
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('assets.error.generic'))
    },
  })

  const assignMutation = useMutation({
    mutationFn: () => assetsApi.assign(id, assigneeId),
    onSuccess: async () => {
      setAssignOpen(false)
      setAssigneeId('')
      setFormError(null)
      await refresh()
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('assets.error.generic'))
    },
  })

  const returnMutation = useMutation({
    mutationFn: () => assetsApi.returnAsset(id),
    onSuccess: async () => {
      setFormError(null)
      await refresh()
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('assets.error.generic'))
    },
  })

  const timelineItems = useMemo<TimelineItem[]>(() => {
    const rows = assignmentsQuery.data ?? []
    return rows.map((row: AssetAssignment) => ({
      id: row.id,
      timestamp: row.assignedAtUtc,
      title: row.isActive
        ? t('assets.history.activeAssignment')
        : t('assets.history.returnedAssignment'),
      description: row.notes,
      actor: `${t('assets.history.assignee')}: ${row.assignedToUserId.slice(0, 8)} · ${t('assets.history.assigner')}: ${row.assignedByUserId.slice(0, 8)}`,
      status: row.isActive ? t('assets.history.current') : row.returnedAtUtc ?? undefined,
      type: 'custody',
    }))
  }, [assignmentsQuery.data, t])

  if (assetQuery.isLoading) {
    return <Skeleton className="h-40 w-full" />
  }

  const asset = assetQuery.data
  if (!asset) {
    return <p className="text-sm text-muted-foreground">{t('assets.notFound')}</p>
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={asset.name}
        description={`${asset.assetNumber} · ${asset.assetType}`}
        actions={
          <div className="flex flex-wrap gap-2">
            <Button asChild variant="outline">
              <Link to="/it/assets">{t('assets.back')}</Link>
            </Button>
            {can('assets.manage') ? (
              <>
                <Button
                  variant="secondary"
                  onClick={() => {
                    editForm.reset({
                      assetType: asset.assetType,
                      name: asset.name,
                      status: asset.status,
                      serialNumber: asset.serialNumber ?? '',
                      manufacturer: asset.manufacturer ?? '',
                      model: asset.model ?? '',
                      notes: asset.notes ?? '',
                    })
                    setEditOpen(true)
                  }}
                >
                  {t('assets.edit')}
                </Button>
                {asset.activeAssignedToUserId ? (
                  <Button
                    variant="outline"
                    disabled={returnMutation.isPending}
                    onClick={() => returnMutation.mutate()}
                  >
                    {t('assets.return')}
                  </Button>
                ) : (
                  <Button onClick={() => setAssignOpen(true)}>{t('assets.assign')}</Button>
                )}
              </>
            ) : null}
          </div>
        }
      />

      {formError ? <p className="text-sm text-destructive">{formError}</p> : null}

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>{t('assets.detail.identity')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2 text-sm">
            <DetailRow label={t('assets.columns.status')} value={<Badge variant="secondary">{asset.status}</Badge>} />
            <DetailRow label={t('assets.columns.serial')} value={asset.serialNumber ?? '—'} />
            <DetailRow label={t('assets.fields.manufacturer')} value={asset.manufacturer ?? '—'} />
            <DetailRow label={t('assets.fields.model')} value={asset.model ?? '—'} />
            <DetailRow
              label={t('assets.columns.location')}
              value={asset.locationId ? asset.locationId.slice(0, 8) : '—'}
            />
            <DetailRow label={t('assets.fields.notes')} value={asset.notes ?? '—'} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>{t('assets.detail.custody')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2 text-sm">
            <DetailRow
              label={t('assets.columns.assignedTo')}
              value={asset.activeAssignedToUserId ? asset.activeAssignedToUserId.slice(0, 8) : t('assets.unassigned')}
            />
            <DetailRow
              label={t('assets.fields.assignedAt')}
              value={
                asset.activeAssignedAtUtc
                  ? new Date(asset.activeAssignedAtUtc).toLocaleString()
                  : '—'
              }
            />
            <DetailRow
              label={t('assets.detail.linkedCi')}
              value={asset.configurationItemNumber ?? t('assets.detail.noCi')}
            />
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>{t('assets.detail.history')}</CardTitle>
        </CardHeader>
        <CardContent>
          <Timeline
            items={timelineItems}
            emptyMessage={t('assets.history.empty')}
          />
        </CardContent>
      </Card>

      <Dialog open={editOpen} onOpenChange={setEditOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('assets.editTitle')}</DialogTitle>
            <DialogDescription>{t('assets.editDescription')}</DialogDescription>
          </DialogHeader>
          <form
            className="space-y-4"
            onSubmit={editForm.handleSubmit((values) => updateMutation.mutate(values))}
          >
            <div className="space-y-2">
              <Label>{t('assets.fields.assetType')}</Label>
              <Input {...editForm.register('assetType')} />
            </div>
            <div className="space-y-2">
              <Label>{t('assets.fields.name')}</Label>
              <Input {...editForm.register('name')} />
            </div>
            <div className="space-y-2">
              <Label>{t('assets.fields.status')}</Label>
              <Input {...editForm.register('status')} />
            </div>
            <div className="space-y-2">
              <Label>{t('assets.fields.serial')}</Label>
              <Input {...editForm.register('serialNumber')} />
            </div>
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label>{t('assets.fields.manufacturer')}</Label>
                <Input {...editForm.register('manufacturer')} />
              </div>
              <div className="space-y-2">
                <Label>{t('assets.fields.model')}</Label>
                <Input {...editForm.register('model')} />
              </div>
            </div>
            <div className="space-y-2">
              <Label>{t('assets.fields.notes')}</Label>
              <Input {...editForm.register('notes')} />
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setEditOpen(false)}>
                {t('admin.cancel')}
              </Button>
              <Button type="submit" disabled={updateMutation.isPending}>
                {t('admin.save')}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <Dialog open={assignOpen} onOpenChange={setAssignOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('assets.assignTitle')}</DialogTitle>
            <DialogDescription>{t('assets.assignDescription')}</DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            {can('admin.users') ? (
              <div className="space-y-2">
                <Label>{t('assets.fields.assignee')}</Label>
                <Select value={assigneeId} onValueChange={setAssigneeId}>
                  <SelectTrigger>
                    <SelectValue placeholder={t('assets.fields.assigneePlaceholder')} />
                  </SelectTrigger>
                  <SelectContent>
                    {(usersQuery.data ?? []).map((user) => (
                      <SelectItem key={user.id} value={user.id}>
                        {user.displayName} ({user.upn})
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            ) : (
              <div className="space-y-2">
                <Label>{t('assets.fields.assigneeId')}</Label>
                <Input value={assigneeId} onChange={(event) => setAssigneeId(event.target.value)} />
              </div>
            )}
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setAssignOpen(false)}>
                {t('admin.cancel')}
              </Button>
              <Button
                disabled={!assigneeId || assignMutation.isPending}
                onClick={() => assignMutation.mutate()}
              >
                {t('assets.assign')}
              </Button>
            </DialogFooter>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  )
}

function DetailRow({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="flex flex-wrap items-baseline justify-between gap-2 border-b border-border/60 py-2 last:border-0">
      <span className="text-muted-foreground">{label}</span>
      <span className="text-foreground">{value}</span>
    </div>
  )
}
