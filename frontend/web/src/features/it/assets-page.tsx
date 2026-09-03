import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import type { ColumnDef } from '@tanstack/react-table'
import { Plus, Search } from 'lucide-react'
import { ApiError, assetsApi, type Asset } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { DataTable } from '@/components/shared/data-table'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
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
import { assetKeys } from '@/features/it/query-keys'

const createSchema = z.object({
  assetType: z.string().trim().min(1),
  name: z.string().trim().min(1),
  serialNumber: z.string().optional(),
  manufacturer: z.string().optional(),
  model: z.string().optional(),
})

type CreateForm = z.infer<typeof createSchema>

export function AssetsPage() {
  const { t } = useTranslation()
  const { can } = useAuth()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [createOpen, setCreateOpen] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)

  const listQuery = useQuery({
    queryKey: assetKeys.list(search),
    queryFn: () => assetsApi.list(search),
  })

  const createForm = useForm<CreateForm>({
    resolver: zodResolver(createSchema),
    defaultValues: {
      assetType: 'Laptop',
      name: '',
      serialNumber: '',
      manufacturer: '',
      model: '',
    },
  })

  const createMutation = useMutation({
    mutationFn: (values: CreateForm) =>
      assetsApi.create({
        assetType: values.assetType,
        name: values.name,
        serialNumber: values.serialNumber?.trim() || null,
        manufacturer: values.manufacturer?.trim() || null,
        model: values.model?.trim() || null,
      }),
    onSuccess: async (asset) => {
      setCreateOpen(false)
      createForm.reset({ assetType: 'Laptop', name: '', serialNumber: '', manufacturer: '', model: '' })
      setFormError(null)
      await queryClient.invalidateQueries({ queryKey: assetKeys.all })
      navigate(`/it/assets/${asset.id}`)
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('assets.error.generic'))
    },
  })

  const columns = useMemo<ColumnDef<Asset, unknown>[]>(
    () => [
      {
        accessorKey: 'assetNumber',
        header: t('assets.columns.assetNumber'),
      },
      {
        accessorKey: 'name',
        header: t('assets.columns.name'),
      },
      {
        accessorKey: 'assetType',
        header: t('assets.columns.type'),
      },
      {
        accessorKey: 'status',
        header: t('assets.columns.status'),
        cell: ({ row }) => <Badge variant="secondary">{row.original.status}</Badge>,
      },
      {
        accessorKey: 'serialNumber',
        header: t('assets.columns.serial'),
        cell: ({ row }) => row.original.serialNumber ?? '—',
      },
      {
        id: 'assignedTo',
        header: t('assets.columns.assignedTo'),
        cell: ({ row }) =>
          row.original.activeAssignedToUserId
            ? row.original.activeAssignedToUserId.slice(0, 8)
            : '—',
      },
      {
        id: 'location',
        header: t('assets.columns.location'),
        cell: ({ row }) =>
          row.original.locationId ? row.original.locationId.slice(0, 8) : '—',
      },
    ],
    [t],
  )

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('assets.title')}
        description={t('assets.description')}
        actions={
          can('assets.manage') ? (
            <Button onClick={() => setCreateOpen(true)}>
              <Plus className="h-4 w-4" />
              <span className="ms-1">{t('assets.create')}</span>
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
          placeholder={t('assets.searchPlaceholder')}
          className="max-w-sm"
        />
        <Button type="submit" variant="secondary">
          <Search className="h-4 w-4" />
          <span className="ms-1">{t('assets.search')}</span>
        </Button>
      </form>

      <DataTable
        columns={columns}
        data={listQuery.data ?? []}
        isLoading={listQuery.isLoading}
        emptyMessage={t('assets.empty')}
        getRowId={(row) => row.id}
        onRowClick={(row) => navigate(`/it/assets/${row.id}`)}
      />

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('assets.createTitle')}</DialogTitle>
            <DialogDescription>{t('assets.createDescription')}</DialogDescription>
          </DialogHeader>
          <form
            className="space-y-4"
            onSubmit={createForm.handleSubmit((values) => createMutation.mutate(values))}
          >
            <div className="space-y-2">
              <Label htmlFor="assetType">{t('assets.fields.assetType')}</Label>
              <Input id="assetType" {...createForm.register('assetType')} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="assetName">{t('assets.fields.name')}</Label>
              <Input id="assetName" {...createForm.register('name')} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="serial">{t('assets.fields.serial')}</Label>
              <Input id="serial" {...createForm.register('serialNumber')} />
            </div>
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="manufacturer">{t('assets.fields.manufacturer')}</Label>
                <Input id="manufacturer" {...createForm.register('manufacturer')} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="model">{t('assets.fields.model')}</Label>
                <Input id="model" {...createForm.register('model')} />
              </div>
            </div>
            {formError ? <p className="text-sm text-destructive">{formError}</p> : null}
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setCreateOpen(false)}>
                {t('admin.cancel')}
              </Button>
              <Button type="submit" disabled={createMutation.isPending}>
                {t('assets.create')}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  )
}
