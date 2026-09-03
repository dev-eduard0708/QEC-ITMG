import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { MoreHorizontal, Plus } from 'lucide-react'
import {
  adminApi,
  ApiError,
  type LookupItem,
} from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { adminKeys } from '@/features/admin/query-keys'
import { cn } from '@/lib/utils'

type LookupKind = 'departments' | 'locations'

const formSchema = z.object({
  name: z.string().trim().min(1),
  description: z.string().optional(),
})

type FormValues = z.infer<typeof formSchema>

export function AdminLookupsPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [tab, setTab] = useState<LookupKind>('departments')
  const [createOpen, setCreateOpen] = useState(false)
  const [editing, setEditing] = useState<LookupItem | null>(null)
  const [formError, setFormError] = useState<string | null>(null)

  const listQuery = useQuery({
    queryKey: adminKeys.lookups(tab),
    queryFn: () =>
      tab === 'departments' ? adminApi.listDepartments() : adminApi.listLocations(),
  })

  const createForm = useForm<FormValues>({
    resolver: zodResolver(formSchema),
    defaultValues: { name: '', description: '' },
  })

  const editForm = useForm<FormValues>({
    resolver: zodResolver(formSchema),
    defaultValues: { name: '', description: '' },
  })

  const createMutation = useMutation({
    mutationFn: (values: FormValues) => {
      const payload = {
        name: values.name,
        description: values.description?.trim() || null,
      }
      return tab === 'departments'
        ? adminApi.createDepartment(payload)
        : adminApi.createLocation(payload)
    },
    onSuccess: async () => {
      setCreateOpen(false)
      createForm.reset({ name: '', description: '' })
      setFormError(null)
      await queryClient.invalidateQueries({ queryKey: adminKeys.lookups(tab) })
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('admin.error.generic'))
    },
  })

  const updateMutation = useMutation({
    mutationFn: (input: { item: LookupItem; values: FormValues; isActive: boolean }) => {
      const payload = {
        name: input.values.name,
        description: input.values.description?.trim() || null,
        isActive: input.isActive,
        rowVersion: input.item.rowVersion,
      }
      return tab === 'departments'
        ? adminApi.updateDepartment(input.item.id, payload)
        : adminApi.updateLocation(input.item.id, payload)
    },
    onSuccess: async () => {
      setEditing(null)
      setFormError(null)
      await queryClient.invalidateQueries({ queryKey: adminKeys.lookups(tab) })
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('admin.error.generic'))
    },
  })

  function openEdit(item: LookupItem) {
    setFormError(null)
    setEditing(item)
    editForm.reset({
      name: item.name,
      description: item.description ?? '',
    })
  }

  const items = listQuery.data ?? []

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('admin.lookups.title')}
        description={t('admin.lookups.description')}
        actions={
          <Button
            type="button"
            onClick={() => {
              setFormError(null)
              createForm.reset({ name: '', description: '' })
              setCreateOpen(true)
            }}
          >
            <Plus className="h-4 w-4" />
            {t('admin.lookups.create')}
          </Button>
        }
      />

      <div className="flex gap-1 rounded-md border border-border bg-card p-1 w-fit">
        {(['departments', 'locations'] as const).map((kind) => (
          <button
            key={kind}
            type="button"
            className={cn(
              'rounded-sm px-3 py-1.5 text-sm font-medium transition-colors',
              tab === kind
                ? 'bg-primary text-primary-foreground'
                : 'text-muted-foreground hover:bg-accent hover:text-accent-foreground',
            )}
            onClick={() => setTab(kind)}
          >
            {t(`admin.lookups.tabs.${kind}`)}
          </button>
        ))}
      </div>

      <Card>
        <CardContent className="p-0">
          {listQuery.isLoading ? (
            <div className="space-y-2 p-4">
              <Skeleton className="h-8 w-full" />
              <Skeleton className="h-8 w-full" />
            </div>
          ) : listQuery.isError ? (
            <p className="p-4 text-sm text-destructive">
              {listQuery.error instanceof ApiError
                ? listQuery.error.message
                : t('admin.error.generic')}
            </p>
          ) : items.length === 0 ? (
            <p className="p-6 text-sm text-muted-foreground">{t('admin.lookups.empty')}</p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('admin.lookups.columns.name')}</TableHead>
                  <TableHead>{t('admin.lookups.columns.description')}</TableHead>
                  <TableHead>{t('admin.lookups.columns.status')}</TableHead>
                  <TableHead className="w-12" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {items.map((item) => (
                  <TableRow key={item.id}>
                    <TableCell className="font-medium">{item.name}</TableCell>
                    <TableCell className="text-muted-foreground">
                      {item.description || t('admin.lookups.noDescription')}
                    </TableCell>
                    <TableCell>
                      <Badge variant={item.isActive ? 'default' : 'secondary'}>
                        {item.isActive
                          ? t('admin.lookups.status.active')
                          : t('admin.lookups.status.disabled')}
                      </Badge>
                    </TableCell>
                    <TableCell>
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <Button variant="ghost" size="icon" aria-label={t('admin.lookups.actions')}>
                            <MoreHorizontal className="h-4 w-4" />
                          </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end">
                          <DropdownMenuItem onClick={() => openEdit(item)}>
                            {t('admin.lookups.edit')}
                          </DropdownMenuItem>
                          <DropdownMenuItem
                            disabled={updateMutation.isPending}
                            onClick={() =>
                              updateMutation.mutate({
                                item,
                                values: {
                                  name: item.name,
                                  description: item.description ?? '',
                                },
                                isActive: !item.isActive,
                              })
                            }
                          >
                            {item.isActive
                              ? t('admin.lookups.disable')
                              : t('admin.lookups.enable')}
                          </DropdownMenuItem>
                        </DropdownMenuContent>
                      </DropdownMenu>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('admin.lookups.createTitle')}</DialogTitle>
            <DialogDescription>{t('admin.lookups.createDescription')}</DialogDescription>
          </DialogHeader>
          <form
            className="space-y-4"
            onSubmit={createForm.handleSubmit((values) => createMutation.mutate(values))}
          >
            <div className="space-y-2">
              <Label htmlFor="lookup-name">{t('admin.lookups.fields.name')}</Label>
              <Input id="lookup-name" {...createForm.register('name')} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="lookup-description">{t('admin.lookups.fields.description')}</Label>
              <Input id="lookup-description" {...createForm.register('description')} />
            </div>
            {formError ? <p className="text-sm text-destructive">{formError}</p> : null}
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setCreateOpen(false)}>
                {t('admin.cancel')}
              </Button>
              <Button type="submit" disabled={createMutation.isPending}>
                {t('admin.save')}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <Dialog open={editing !== null} onOpenChange={(open) => !open && setEditing(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('admin.lookups.editTitle')}</DialogTitle>
          </DialogHeader>
          {editing ? (
            <form
              className="space-y-4"
              onSubmit={editForm.handleSubmit((values) =>
                updateMutation.mutate({
                  item: editing,
                  values,
                  isActive: editing.isActive,
                }),
              )}
            >
              <div className="space-y-2">
                <Label htmlFor="edit-lookup-name">{t('admin.lookups.fields.name')}</Label>
                <Input id="edit-lookup-name" {...editForm.register('name')} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-lookup-description">
                  {t('admin.lookups.fields.description')}
                </Label>
                <Input id="edit-lookup-description" {...editForm.register('description')} />
              </div>
              {formError ? <p className="text-sm text-destructive">{formError}</p> : null}
              <DialogFooter>
                <Button type="button" variant="outline" onClick={() => setEditing(null)}>
                  {t('admin.cancel')}
                </Button>
                <Button type="submit" disabled={updateMutation.isPending}>
                  {t('admin.save')}
                </Button>
              </DialogFooter>
            </form>
          ) : null}
        </DialogContent>
      </Dialog>
    </div>
  )
}
