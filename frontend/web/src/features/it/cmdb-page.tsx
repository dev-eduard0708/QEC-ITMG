import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import type { ColumnDef } from '@tanstack/react-table'
import { Plus, Search } from 'lucide-react'
import { ApiError, cmdbApi, remoteSupportApi, type ConfigurationItem } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { DataTable } from '@/components/shared/data-table'
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
import { Checkbox } from '@/components/ui/checkbox'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { cmdbKeys } from '@/features/it/query-keys'

const createSchema = z.object({
  ciTypeId: z.string().min(1),
  name: z.string().trim().min(1),
  criticality: z.string().optional(),
  description: z.string().optional(),
})

const editSchema = z.object({
  name: z.string().trim().min(1),
  status: z.string().trim().min(1),
  criticality: z.string().optional(),
  description: z.string().optional(),
})

const relationshipSchema = z.object({
  targetCiId: z.string().min(1),
  relationshipType: z.string().min(1),
  notes: z.string().optional(),
})

type CreateForm = z.infer<typeof createSchema>
type EditForm = z.infer<typeof editSchema>
type RelationshipForm = z.infer<typeof relationshipSchema>

export function CmdbPage() {
  const { t } = useTranslation()
  const { can } = useAuth()
  const queryClient = useQueryClient()
  const [searchParams] = useSearchParams()
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [createOpen, setCreateOpen] = useState(false)
  const [editing, setEditing] = useState<ConfigurationItem | null>(null)
  const [selectedCiId, setSelectedCiId] = useState<string | null>(null)
  const [formError, setFormError] = useState<string | null>(null)
  const [remoteNodeId, setRemoteNodeId] = useState('')
  const [remoteProvider, setRemoteProvider] = useState('')
  const [unattendedPermitted, setUnattendedPermitted] = useState(false)

  const typesQuery = useQuery({
    queryKey: cmdbKeys.types(),
    queryFn: () => cmdbApi.listCiTypes(),
  })

  const listQuery = useQuery({
    queryKey: cmdbKeys.cis(search),
    queryFn: () => cmdbApi.listCis(search),
  })

  const relationshipsQuery = useQuery({
    queryKey: cmdbKeys.relationships(selectedCiId ?? ''),
    queryFn: () => cmdbApi.listRelationships(selectedCiId!),
    enabled: Boolean(selectedCiId),
  })

  const createForm = useForm<CreateForm>({
    resolver: zodResolver(createSchema),
    defaultValues: { ciTypeId: '', name: '', criticality: 'Medium', description: '' },
  })

  const editForm = useForm<EditForm>({
    resolver: zodResolver(editSchema),
  })

  const relationshipForm = useForm<RelationshipForm>({
    resolver: zodResolver(relationshipSchema),
    defaultValues: { targetCiId: '', relationshipType: 'DependsOn', notes: '' },
  })

  const syncRemoteMappingFields = (ci: ConfigurationItem) => {
    setRemoteNodeId(ci.remoteEngineNodeId ?? '')
    setRemoteProvider(ci.remoteEngineProvider ?? '')
    setUnattendedPermitted(ci.unattendedRemotePermitted ?? false)
  }

  const createMutation = useMutation({
    mutationFn: (values: CreateForm) =>
      cmdbApi.createCi({
        ciTypeId: values.ciTypeId,
        name: values.name,
        criticality: values.criticality || null,
        description: values.description?.trim() || null,
      }),
    onSuccess: async () => {
      setCreateOpen(false)
      createForm.reset({ ciTypeId: '', name: '', criticality: 'Medium', description: '' })
      setFormError(null)
      await queryClient.invalidateQueries({ queryKey: cmdbKeys.all })
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('cmdb.error.generic'))
    },
  })

  const updateMutation = useMutation({
    mutationFn: (values: EditForm) => {
      if (!editing) throw new Error('missing ci')
      return cmdbApi.updateCi(editing.id, {
        name: values.name,
        status: values.status,
        criticality: values.criticality || null,
        description: values.description?.trim() || null,
        locationId: editing.locationId,
        rowVersion: editing.rowVersion,
      })
    },
    onSuccess: async () => {
      setEditing(null)
      setFormError(null)
      await queryClient.invalidateQueries({ queryKey: cmdbKeys.all })
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('cmdb.error.generic'))
    },
  })

  const remoteMappingMutation = useMutation({
    mutationFn: () => {
      if (!selectedCi) throw new Error('missing ci')
      return remoteSupportApi.setCiRemoteMapping(selectedCi.id, {
        remoteEngineNodeId: remoteNodeId.trim() || null,
        remoteEngineProvider: remoteProvider.trim() || null,
        unattendedRemotePermitted: unattendedPermitted,
        rowVersion: selectedCi.rowVersion,
      })
    },
    onSuccess: async () => {
      setFormError(null)
      await queryClient.invalidateQueries({ queryKey: cmdbKeys.all })
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('cmdb.error.generic'))
    },
  })

  const relationshipMutation = useMutation({
    mutationFn: (values: RelationshipForm) => {
      if (!selectedCiId) throw new Error('missing ci')
      return cmdbApi.createRelationship(selectedCiId, {
        targetCiId: values.targetCiId,
        relationshipType: values.relationshipType,
        notes: values.notes?.trim() || null,
      })
    },
    onSuccess: async () => {
      relationshipForm.reset({ targetCiId: '', relationshipType: 'DependsOn', notes: '' })
      setFormError(null)
      await queryClient.invalidateQueries({
        queryKey: cmdbKeys.relationships(selectedCiId ?? ''),
      })
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('cmdb.error.generic'))
    },
  })

  const columns = useMemo<ColumnDef<ConfigurationItem, unknown>[]>(
    () => [
      { accessorKey: 'ciNumber', header: t('cmdb.columns.ciNumber') },
      { accessorKey: 'name', header: t('cmdb.columns.name') },
      { accessorKey: 'ciTypeName', header: t('cmdb.columns.type') },
      {
        accessorKey: 'status',
        header: t('cmdb.columns.status'),
        cell: ({ row }) => <Badge variant="secondary">{row.original.status}</Badge>,
      },
      {
        accessorKey: 'criticality',
        header: t('cmdb.columns.criticality'),
        cell: ({ row }) => row.original.criticality ?? '—',
      },
      {
        id: 'owner',
        header: t('cmdb.columns.owner'),
        cell: ({ row }) =>
          row.original.ownerUserId ? row.original.ownerUserId.slice(0, 8) : '—',
      },
      {
        id: 'location',
        header: t('cmdb.columns.location'),
        cell: ({ row }) =>
          row.original.locationId ? row.original.locationId.slice(0, 8) : '—',
      },
    ],
    [t],
  )

  const selectedCi = (listQuery.data ?? []).find((item) => item.id === selectedCiId) ?? null

  // Deep link (e.g. from Remote Support) preselects the CI so its mapping panel opens.
  useEffect(() => {
    const requestedCiId = searchParams.get('ci')
    if (!requestedCiId || selectedCiId) return
    const match = (listQuery.data ?? []).find((item) => item.id === requestedCiId)
    if (!match) return
    setSelectedCiId(match.id)
    syncRemoteMappingFields(match)
  }, [searchParams, selectedCiId, listQuery.data])

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('cmdb.title')}
        description={t('cmdb.description')}
        actions={
          can('cmdb.manage') ? (
            <Button onClick={() => setCreateOpen(true)}>
              <Plus className="h-4 w-4" />
              <span className="ms-1">{t('cmdb.create')}</span>
            </Button>
          ) : undefined
        }
      />

      <form
        className="flex flex-wrap gap-2"
        onSubmit={(event) => {
          event.preventDefault()
          setSearch(searchInput.trim())
        }}
      >
        <Input
          value={searchInput}
          onChange={(event) => setSearchInput(event.target.value)}
          placeholder={t('cmdb.searchPlaceholder')}
          className="max-w-sm"
        />
        <Button type="submit" variant="secondary">
          <Search className="h-4 w-4" />
          <span className="ms-1">{t('cmdb.search')}</span>
        </Button>
      </form>

      {formError ? <p className="text-sm text-destructive">{formError}</p> : null}

      <DataTable
        columns={columns}
        data={listQuery.data ?? []}
        isLoading={listQuery.isLoading}
        emptyMessage={t('cmdb.empty')}
        getRowId={(row) => row.id}
        onRowClick={(row) => {
          setSelectedCiId(row.id)
          syncRemoteMappingFields(row)
          if (can('cmdb.manage')) {
            setEditing(row)
            editForm.reset({
              name: row.name,
              status: row.status,
              criticality: row.criticality ?? '',
              description: row.description ?? '',
            })
          }
        }}
      />

      {selectedCi ? (
        <Card>
          <CardHeader className="flex flex-row items-center justify-between gap-2">
            <CardTitle>
              {t('cmdb.relationships.title')} — {selectedCi.ciNumber}
            </CardTitle>
            {can('cmdb.manage') ? (
              <Button
                size="sm"
                variant="outline"
                onClick={() => {
                  setEditing(selectedCi)
                  syncRemoteMappingFields(selectedCi)
                  editForm.reset({
                    name: selectedCi.name,
                    status: selectedCi.status,
                    criticality: selectedCi.criticality ?? '',
                    description: selectedCi.description ?? '',
                  })
                }}
              >
                {t('cmdb.edit')}
              </Button>
            ) : null}
          </CardHeader>
          <CardContent className="space-y-4">
            <ul className="space-y-2 text-sm">
              {(relationshipsQuery.data ?? []).length === 0 ? (
                <li className="text-muted-foreground">{t('cmdb.relationships.empty')}</li>
              ) : (
                (relationshipsQuery.data ?? []).map((rel) => (
                  <li key={rel.id} className="flex flex-wrap items-center justify-between gap-2 border-b border-border/60 py-2">
                    <span>
                      {rel.relationshipType}: {rel.sourceCiId.slice(0, 8)} → {rel.targetCiId.slice(0, 8)}
                    </span>
                    {can('cmdb.manage') ? (
                      <Button
                        size="sm"
                        variant="ghost"
                        onClick={async () => {
                          await cmdbApi.deleteRelationship(rel.id)
                          await queryClient.invalidateQueries({
                            queryKey: cmdbKeys.relationships(selectedCi.id),
                          })
                        }}
                      >
                        {t('cmdb.relationships.delete')}
                      </Button>
                    ) : null}
                  </li>
                ))
              )}
            </ul>

            {can('cmdb.manage') ? (
              <form
                className="grid gap-3 sm:grid-cols-3"
                onSubmit={relationshipForm.handleSubmit((values) =>
                  relationshipMutation.mutate(values),
                )}
              >
                <div className="space-y-2 sm:col-span-1">
                  <Label>{t('cmdb.relationships.target')}</Label>
                  <Select
                    value={relationshipForm.watch('targetCiId')}
                    onValueChange={(value) => relationshipForm.setValue('targetCiId', value)}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder={t('cmdb.relationships.targetPlaceholder')} />
                    </SelectTrigger>
                    <SelectContent>
                      {(listQuery.data ?? [])
                        .filter((item) => item.id !== selectedCi.id)
                        .map((item) => (
                          <SelectItem key={item.id} value={item.id}>
                            {item.ciNumber} — {item.name}
                          </SelectItem>
                        ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label>{t('cmdb.relationships.type')}</Label>
                  <Select
                    value={relationshipForm.watch('relationshipType')}
                    onValueChange={(value) => relationshipForm.setValue('relationshipType', value)}
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {['DependsOn', 'HostedOn', 'ConnectsTo', 'Supports', 'Contains'].map((type) => (
                        <SelectItem key={type} value={type}>
                          {type}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="flex items-end">
                  <Button type="submit" disabled={relationshipMutation.isPending}>
                    {t('cmdb.relationships.add')}
                  </Button>
                </div>
              </form>
            ) : null}
          </CardContent>
        </Card>
      ) : null}

      {selectedCi && can('remote.admin') ? (
        <Card>
          <CardHeader>
            <CardTitle>
              {t('cmdb.remoteMapping.title')} — {selectedCi.ciNumber}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <form
              className="grid gap-4 sm:grid-cols-2"
              onSubmit={(event) => {
                event.preventDefault()
                remoteMappingMutation.mutate()
              }}
            >
              <div className="space-y-2">
                <Label>{t('cmdb.remoteMapping.nodeId')}</Label>
                <Input value={remoteNodeId} onChange={(event) => setRemoteNodeId(event.target.value)} />
              </div>
              <div className="space-y-2">
                <Label>{t('cmdb.remoteMapping.provider')}</Label>
                <Input value={remoteProvider} onChange={(event) => setRemoteProvider(event.target.value)} />
              </div>
              <div className="flex items-center gap-2 sm:col-span-2">
                <Checkbox
                  id="unattended-permitted"
                  checked={unattendedPermitted}
                  onCheckedChange={(checked) => setUnattendedPermitted(checked === true)}
                />
                <Label htmlFor="unattended-permitted">{t('cmdb.remoteMapping.unattendedPermitted')}</Label>
              </div>
              <div className="sm:col-span-2">
                <Button type="submit" disabled={remoteMappingMutation.isPending}>
                  {t('cmdb.remoteMapping.save')}
                </Button>
              </div>
            </form>
          </CardContent>
        </Card>
      ) : null}

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('cmdb.createTitle')}</DialogTitle>
            <DialogDescription>{t('cmdb.createDescription')}</DialogDescription>
          </DialogHeader>
          <form
            className="space-y-4"
            onSubmit={createForm.handleSubmit((values) => createMutation.mutate(values))}
          >
            <div className="space-y-2">
              <Label>{t('cmdb.fields.type')}</Label>
              <Select
                value={createForm.watch('ciTypeId')}
                onValueChange={(value) => createForm.setValue('ciTypeId', value)}
              >
                <SelectTrigger>
                  <SelectValue placeholder={t('cmdb.fields.typePlaceholder')} />
                </SelectTrigger>
                <SelectContent>
                  {(typesQuery.data ?? []).map((type) => (
                    <SelectItem key={type.id} value={type.id}>
                      {type.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>{t('cmdb.fields.name')}</Label>
              <Input {...createForm.register('name')} />
            </div>
            <div className="space-y-2">
              <Label>{t('cmdb.fields.criticality')}</Label>
              <Input {...createForm.register('criticality')} />
            </div>
            <div className="space-y-2">
              <Label>{t('cmdb.fields.description')}</Label>
              <Input {...createForm.register('description')} />
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setCreateOpen(false)}>
                {t('admin.cancel')}
              </Button>
              <Button type="submit" disabled={createMutation.isPending}>
                {t('cmdb.create')}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <Dialog open={editing !== null} onOpenChange={(open) => !open && setEditing(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('cmdb.editTitle')}</DialogTitle>
            <DialogDescription>{t('cmdb.editDescription')}</DialogDescription>
          </DialogHeader>
          <form
            className="space-y-4"
            onSubmit={editForm.handleSubmit((values) => updateMutation.mutate(values))}
          >
            <div className="space-y-2">
              <Label>{t('cmdb.fields.name')}</Label>
              <Input {...editForm.register('name')} />
            </div>
            <div className="space-y-2">
              <Label>{t('cmdb.fields.status')}</Label>
              <Input {...editForm.register('status')} />
            </div>
            <div className="space-y-2">
              <Label>{t('cmdb.fields.criticality')}</Label>
              <Input {...editForm.register('criticality')} />
            </div>
            <div className="space-y-2">
              <Label>{t('cmdb.fields.description')}</Label>
              <Input {...editForm.register('description')} />
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setEditing(null)}>
                {t('admin.cancel')}
              </Button>
              <Button type="submit" disabled={updateMutation.isPending}>
                {t('admin.save')}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  )
}
