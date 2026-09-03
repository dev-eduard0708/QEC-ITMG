import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { MoreHorizontal, Plus, Search } from 'lucide-react'
import { adminApi, ApiError, type AdminUser } from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Checkbox } from '@/components/ui/checkbox'
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { DataTable } from '@/components/shared/data-table'
import { adminKeys } from '@/features/admin/query-keys'
import type { ColumnDef } from '@tanstack/react-table'

const createSchema = z.object({
  displayName: z.string().trim().min(1),
  upn: z.string().trim().min(1),
  userType: z.enum(['Employee', 'Vendor', 'Service']),
  timeZone: z.string().optional(),
})

const editSchema = z.object({
  displayName: z.string().trim().min(1),
  userType: z.enum(['Employee', 'Vendor', 'Service']),
  status: z.enum(['Active', 'Disabled']),
  timeZone: z.string().optional(),
  directoryObjectId: z.string().optional(),
  roleIds: z.array(z.string()),
})

type CreateForm = z.infer<typeof createSchema>
type EditForm = z.infer<typeof editSchema>

export function AdminUsersPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [createOpen, setCreateOpen] = useState(false)
  const [editing, setEditing] = useState<AdminUser | null>(null)
  const [formError, setFormError] = useState<string | null>(null)

  const usersQuery = useQuery({
    queryKey: adminKeys.users(search),
    queryFn: () => adminApi.listUsers(search),
  })

  const rolesQuery = useQuery({
    queryKey: adminKeys.roles(),
    queryFn: () => adminApi.listRoles(),
    enabled: createOpen || editing !== null,
  })

  const createForm = useForm<CreateForm>({
    resolver: zodResolver(createSchema),
    defaultValues: {
      displayName: '',
      upn: '',
      userType: 'Employee',
      timeZone: '',
    },
  })

  const editForm = useForm<EditForm>({
    resolver: zodResolver(editSchema),
  })

  const createMutation = useMutation({
    mutationFn: adminApi.createUser,
    onSuccess: async () => {
      setCreateOpen(false)
      createForm.reset()
      setFormError(null)
      await queryClient.invalidateQueries({ queryKey: adminKeys.all })
    },
    onError: (error: unknown) => {
      setFormError(error instanceof ApiError ? error.message : t('admin.error.generic'))
    },
  })

  const updateMutation = useMutation({
    mutationFn: async (values: EditForm) => {
      if (!editing) throw new Error('No user selected')
      await adminApi.updateUser(editing.id, {
        displayName: values.displayName,
        userType: values.userType,
        status: values.status,
        timeZone: values.timeZone || null,
        directoryObjectId: values.directoryObjectId || null,
        rowVersion: editing.rowVersion,
      })
      return adminApi.replaceUserRoles(editing.id, values.roleIds)
    },
    onSuccess: async () => {
      setEditing(null)
      setFormError(null)
      await queryClient.invalidateQueries({ queryKey: adminKeys.all })
    },
    onError: (error: unknown) => {
      setFormError(error instanceof ApiError ? error.message : t('admin.error.generic'))
    },
  })

  const users = usersQuery.data ?? []
  const roles = rolesQuery.data ?? []

  const selectedRoleIds = editForm.watch('roleIds') ?? []

  const statusBadge = useMemo(
    () => (status: string) =>
      status === 'Active' ? (
        <Badge variant="success">{t('admin.users.status.active')}</Badge>
      ) : (
        <Badge variant="warning">{t('admin.users.status.disabled')}</Badge>
      ),
    [t],
  )

  function openEdit(user: AdminUser) {
    setEditing(user)
    setFormError(null)
    editForm.reset({
      displayName: user.displayName,
      userType: user.userType as EditForm['userType'],
      status: user.status as EditForm['status'],
      timeZone: user.timeZone ?? '',
      directoryObjectId: user.directoryObjectId ?? '',
      roleIds: user.roles.map((role) => role.id),
    })
  }

  const columns = useMemo<ColumnDef<AdminUser>[]>(
    () => [
      {
        accessorKey: 'displayName',
        header: t('admin.users.columns.displayName'),
        cell: ({ row }) => <span className="font-medium">{row.original.displayName}</span>,
      },
      {
        accessorKey: 'upn',
        header: t('admin.users.columns.upn'),
      },
      {
        id: 'userType',
        header: t('admin.users.columns.type'),
        cell: ({ row }) =>
          t(`admin.users.type.${row.original.userType}`, { defaultValue: row.original.userType }),
      },
      {
        accessorKey: 'status',
        header: t('admin.users.columns.status'),
        cell: ({ row }) => statusBadge(row.original.status),
      },
      {
        id: 'roles',
        header: t('admin.users.columns.roles'),
        cell: ({ row }) => (
          <div className="flex flex-wrap gap-1">
            {row.original.roles.length === 0 ? (
              <span className="text-muted-foreground">—</span>
            ) : (
              row.original.roles.map((role) => (
                <Badge key={role.id} variant="secondary">
                  {role.name}
                </Badge>
              ))
            )}
          </div>
        ),
      },
      {
        id: 'actions',
        header: () => null,
        size: 48,
        cell: ({ row }) => (
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button
                variant="ghost"
                size="icon"
                aria-label={t('admin.users.actions')}
                onClick={(event) => event.stopPropagation()}
              >
                <MoreHorizontal className="h-4 w-4" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuItem onClick={() => openEdit(row.original)}>
                {t('admin.users.edit')}
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        ),
      },
    ],
    [statusBadge, t],
  )

  return (
    <div>
      <PageHeader
        title={t('admin.users.title')}
        description={t('admin.users.description')}
        actions={
          <Button onClick={() => { setFormError(null); setCreateOpen(true) }}>
            <Plus className="h-4 w-4" />
            {t('admin.users.create')}
          </Button>
        }
      />

      <div className="mb-4 flex flex-col gap-2 sm:flex-row">
        <div className="relative flex-1">
          <Search className="pointer-events-none absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            className="ps-9"
            value={searchInput}
            onChange={(event) => setSearchInput(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === 'Enter') setSearch(searchInput)
            }}
            placeholder={t('admin.users.searchPlaceholder')}
            aria-label={t('admin.users.searchPlaceholder')}
          />
        </div>
        <Button variant="outline" onClick={() => setSearch(searchInput)}>
          {t('admin.users.search')}
        </Button>
      </div>

      {usersQuery.isLoading ? (
        <div className="space-y-2">
          <Skeleton className="h-12 w-full" />
          <Skeleton className="h-12 w-full" />
          <Skeleton className="h-12 w-full" />
        </div>
      ) : null}

      {usersQuery.isError ? (
        <Card>
          <CardContent className="py-6 text-sm text-destructive">
            {usersQuery.error instanceof ApiError
              ? usersQuery.error.message
              : t('admin.error.generic')}
            {usersQuery.error instanceof ApiError && usersQuery.error.status === 401
              ? ` — ${t('admin.error.authRequired')}`
              : null}
            {usersQuery.error instanceof ApiError && usersQuery.error.status === 403
              ? ` — ${t('admin.error.permissionDenied')}`
              : null}
          </CardContent>
        </Card>
      ) : null}

      {!usersQuery.isLoading && !usersQuery.isError ? (
        <>
          <div className="hidden md:block">
            <DataTable
              columns={columns}
              data={users}
              emptyMessage={t('admin.users.empty')}
              getRowId={(user) => user.id}
              onRowClick={openEdit}
            />
          </div>

          <div className="grid gap-3 md:hidden">
            {users.map((user) => (
              <Card key={user.id}>
                <CardContent className="space-y-3 py-4">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <div className="font-medium">{user.displayName}</div>
                      <div className="text-sm text-muted-foreground">{user.upn}</div>
                    </div>
                    {statusBadge(user.status)}
                  </div>
                  <div className="text-sm text-muted-foreground">
                    {t(`admin.users.type.${user.userType}`, { defaultValue: user.userType })}
                  </div>
                  <div className="flex flex-wrap gap-1">
                    {user.roles.map((role) => (
                      <Badge key={role.id} variant="secondary">
                        {role.name}
                      </Badge>
                    ))}
                  </div>
                  <Button variant="outline" size="sm" onClick={() => openEdit(user)}>
                    {t('admin.users.edit')}
                  </Button>
                </CardContent>
              </Card>
            ))}
          </div>

          {users.length === 0 ? (
            <p className="mt-4 text-sm text-muted-foreground md:hidden">{t('admin.users.empty')}</p>
          ) : null}
        </>
      ) : null}

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('admin.users.createTitle')}</DialogTitle>
            <DialogDescription>{t('admin.users.createDescription')}</DialogDescription>
          </DialogHeader>
          <form
            className="space-y-4"
            onSubmit={createForm.handleSubmit((values) =>
              createMutation.mutate({
                displayName: values.displayName,
                upn: values.upn,
                userType: values.userType,
                timeZone: values.timeZone || null,
              }),
            )}
          >
            <div className="space-y-2">
              <Label htmlFor="create-displayName">{t('admin.users.fields.displayName')}</Label>
              <Input id="create-displayName" {...createForm.register('displayName')} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="create-upn">{t('admin.users.fields.upn')}</Label>
              <Input id="create-upn" {...createForm.register('upn')} />
            </div>
            <div className="space-y-2">
              <Label>{t('admin.users.fields.userType')}</Label>
              <Select
                value={createForm.watch('userType')}
                onValueChange={(value) =>
                  createForm.setValue('userType', value as CreateForm['userType'])
                }
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Employee">{t('admin.users.type.Employee')}</SelectItem>
                  <SelectItem value="Vendor">{t('admin.users.type.Vendor')}</SelectItem>
                  <SelectItem value="Service">{t('admin.users.type.Service')}</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="create-timeZone">{t('admin.users.fields.timeZone')}</Label>
              <Input id="create-timeZone" {...createForm.register('timeZone')} />
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
        <DialogContent className="max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{t('admin.users.editTitle')}</DialogTitle>
            <DialogDescription>{editing?.upn}</DialogDescription>
          </DialogHeader>
          <form
            className="space-y-4"
            onSubmit={editForm.handleSubmit((values) => updateMutation.mutate(values))}
          >
            <div className="space-y-2">
              <Label htmlFor="edit-displayName">{t('admin.users.fields.displayName')}</Label>
              <Input id="edit-displayName" {...editForm.register('displayName')} />
            </div>
            <div className="space-y-2">
              <Label>{t('admin.users.fields.userType')}</Label>
              <Select
                value={editForm.watch('userType')}
                onValueChange={(value) =>
                  editForm.setValue('userType', value as EditForm['userType'])
                }
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Employee">{t('admin.users.type.Employee')}</SelectItem>
                  <SelectItem value="Vendor">{t('admin.users.type.Vendor')}</SelectItem>
                  <SelectItem value="Service">{t('admin.users.type.Service')}</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>{t('admin.users.fields.status')}</Label>
              <Select
                value={editForm.watch('status')}
                onValueChange={(value) =>
                  editForm.setValue('status', value as EditForm['status'])
                }
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Active">{t('admin.users.status.active')}</SelectItem>
                  <SelectItem value="Disabled">{t('admin.users.status.disabled')}</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="edit-timeZone">{t('admin.users.fields.timeZone')}</Label>
              <Input id="edit-timeZone" {...editForm.register('timeZone')} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="edit-directoryObjectId">{t('admin.users.fields.directoryObjectId')}</Label>
              <Input id="edit-directoryObjectId" {...editForm.register('directoryObjectId')} />
            </div>
            <div className="space-y-2">
              <Label>{t('admin.users.fields.roles')}</Label>
              <div className="max-h-48 space-y-2 overflow-y-auto rounded-md border border-border p-3">
                {roles.map((role) => {
                  const checked = selectedRoleIds.includes(role.id)
                  return (
                    <label key={role.id} className="flex items-center gap-2 text-sm">
                      <Checkbox
                        checked={checked}
                        onCheckedChange={(value) => {
                          const next = value === true
                            ? [...selectedRoleIds, role.id]
                            : selectedRoleIds.filter((id) => id !== role.id)
                          editForm.setValue('roleIds', next, { shouldDirty: true })
                        }}
                      />
                      <span>{role.name}</span>
                    </label>
                  )
                })}
                {roles.length === 0 ? (
                  <p className="text-sm text-muted-foreground">{t('admin.users.noRoles')}</p>
                ) : null}
              </div>
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
        </DialogContent>
      </Dialog>
    </div>
  )
}
