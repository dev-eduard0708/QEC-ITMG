import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { MoreHorizontal, Plus } from 'lucide-react'
import { adminApi, ApiError, type AdminPermission, type AdminRole } from '@/api/client'
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

const roleSchema = z.object({
  name: z.string().trim().min(1),
  description: z.string().optional(),
  permissionIds: z.array(z.string()),
})

type RoleForm = z.infer<typeof roleSchema>

function groupPermissions(permissions: AdminPermission[]) {
  const groups = new Map<string, AdminPermission[]>()
  for (const permission of permissions) {
    const resource = permission.key.split('.')[0] ?? 'other'
    const list = groups.get(resource) ?? []
    list.push(permission)
    groups.set(resource, list)
  }
  return [...groups.entries()].sort(([a], [b]) => a.localeCompare(b))
}

export function AdminRolesPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [createOpen, setCreateOpen] = useState(false)
  const [editing, setEditing] = useState<AdminRole | null>(null)
  const [formError, setFormError] = useState<string | null>(null)

  const rolesQuery = useQuery({
    queryKey: adminKeys.roles(),
    queryFn: () => adminApi.listRoles(),
  })

  const permissionsQuery = useQuery({
    queryKey: adminKeys.permissions(),
    queryFn: () => adminApi.listPermissions(),
    enabled: createOpen || editing !== null,
  })

  const form = useForm<RoleForm>({
    resolver: zodResolver(roleSchema),
    defaultValues: {
      name: '',
      description: '',
      permissionIds: [],
    },
  })

  const createMutation = useMutation({
    mutationFn: async (values: RoleForm) => {
      const created = await adminApi.createRole({
        name: values.name,
        description: values.description || null,
      })
      if (values.permissionIds.length > 0) {
        return adminApi.replaceRolePermissions(created.id, values.permissionIds)
      }
      return created
    },
    onSuccess: async () => {
      setCreateOpen(false)
      form.reset()
      setFormError(null)
      await queryClient.invalidateQueries({ queryKey: adminKeys.all })
    },
    onError: (error: unknown) => {
      setFormError(error instanceof ApiError ? error.message : t('admin.error.generic'))
    },
  })

  const updateMutation = useMutation({
    mutationFn: async (values: RoleForm) => {
      if (!editing) throw new Error('No role selected')
      await adminApi.updateRole(editing.id, {
        name: values.name,
        description: values.description || null,
        rowVersion: editing.rowVersion,
      })
      return adminApi.replaceRolePermissions(editing.id, values.permissionIds)
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

  const roles = rolesQuery.data ?? []
  const permissions = permissionsQuery.data ?? []
  const grouped = useMemo(() => groupPermissions(permissions), [permissions])
  const selectedPermissionIds = form.watch('permissionIds') ?? []

  async function openEdit(role: AdminRole) {
    setFormError(null)
    const detail = await adminApi.getRole(role.id)
    setEditing(detail)
    form.reset({
      name: detail.name,
      description: detail.description ?? '',
      permissionIds: detail.permissions.map((permission) => permission.id),
    })
  }

  function openCreate() {
    setFormError(null)
    form.reset({ name: '', description: '', permissionIds: [] })
    setCreateOpen(true)
  }

  function PermissionPicker() {
    return (
      <div className="max-h-64 space-y-4 overflow-y-auto rounded-md border border-border p-3">
        {grouped.map(([resource, items]) => (
          <div key={resource} className="space-y-2">
            <div className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
              {resource}
            </div>
            {items.map((permission) => {
              const checked = selectedPermissionIds.includes(permission.id)
              return (
                <label key={permission.id} className="flex items-start gap-2 text-sm">
                  <Checkbox
                    className="mt-0.5"
                    checked={checked}
                    onCheckedChange={(value) => {
                      const next =
                        value === true
                          ? [...selectedPermissionIds, permission.id]
                          : selectedPermissionIds.filter((id) => id !== permission.id)
                      form.setValue('permissionIds', next, { shouldDirty: true })
                    }}
                  />
                  <span>
                    <span className="font-medium">{permission.key}</span>
                    {permission.description ? (
                      <span className="mt-0.5 block text-xs text-muted-foreground">
                        {permission.description}
                      </span>
                    ) : null}
                  </span>
                </label>
              )
            })}
          </div>
        ))}
        {permissions.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t('admin.roles.noPermissions')}</p>
        ) : null}
      </div>
    )
  }

  return (
    <div>
      <PageHeader
        title={t('admin.roles.title')}
        description={t('admin.roles.description')}
        actions={
          <Button onClick={openCreate}>
            <Plus className="h-4 w-4" />
            {t('admin.roles.create')}
          </Button>
        }
      />

      {rolesQuery.isLoading ? (
        <div className="space-y-2">
          <Skeleton className="h-12 w-full" />
          <Skeleton className="h-12 w-full" />
        </div>
      ) : null}

      {rolesQuery.isError ? (
        <Card>
          <CardContent className="py-6 text-sm text-destructive">
            {rolesQuery.error instanceof ApiError
              ? rolesQuery.error.message
              : t('admin.error.generic')}
            {rolesQuery.error instanceof ApiError && rolesQuery.error.status === 401
              ? ` — ${t('admin.error.authRequired')}`
              : null}
            {rolesQuery.error instanceof ApiError && rolesQuery.error.status === 403
              ? ` — ${t('admin.error.permissionDenied')}`
              : null}
          </CardContent>
        </Card>
      ) : null}

      {!rolesQuery.isLoading && !rolesQuery.isError ? (
        <>
          <div className="hidden md:block rounded-lg border border-border bg-card">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('admin.roles.columns.name')}</TableHead>
                  <TableHead>{t('admin.roles.columns.description')}</TableHead>
                  <TableHead>{t('admin.roles.columns.system')}</TableHead>
                  <TableHead>{t('admin.roles.columns.permissions')}</TableHead>
                  <TableHead className="w-12" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {roles.map((role) => (
                  <TableRow key={role.id}>
                    <TableCell className="font-medium">{role.name}</TableCell>
                    <TableCell>{role.description ?? '—'}</TableCell>
                    <TableCell>
                      {role.isSystem ? (
                        <Badge variant="secondary">{t('admin.roles.system')}</Badge>
                      ) : (
                        '—'
                      )}
                    </TableCell>
                    <TableCell>{role.permissionCount}</TableCell>
                    <TableCell>
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <Button variant="ghost" size="icon" aria-label={t('admin.roles.actions')}>
                            <MoreHorizontal className="h-4 w-4" />
                          </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end">
                          <DropdownMenuItem onClick={() => void openEdit(role)}>
                            {t('admin.roles.edit')}
                          </DropdownMenuItem>
                        </DropdownMenuContent>
                      </DropdownMenu>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>

          <div className="grid gap-3 md:hidden">
            {roles.map((role) => (
              <Card key={role.id}>
                <CardContent className="space-y-3 py-4">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <div className="font-medium">{role.name}</div>
                      <div className="text-sm text-muted-foreground">
                        {role.description ?? t('admin.roles.noDescription')}
                      </div>
                    </div>
                    {role.isSystem ? <Badge variant="secondary">{t('admin.roles.system')}</Badge> : null}
                  </div>
                  <div className="text-sm text-muted-foreground">
                    {t('admin.roles.permissionCount', { count: role.permissionCount })}
                  </div>
                  <Button variant="outline" size="sm" onClick={() => void openEdit(role)}>
                    {t('admin.roles.edit')}
                  </Button>
                </CardContent>
              </Card>
            ))}
          </div>

          {roles.length === 0 ? (
            <p className="mt-4 text-sm text-muted-foreground">{t('admin.roles.empty')}</p>
          ) : null}
        </>
      ) : null}

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{t('admin.roles.createTitle')}</DialogTitle>
            <DialogDescription>{t('admin.roles.createDescription')}</DialogDescription>
          </DialogHeader>
          <form
            className="space-y-4"
            onSubmit={form.handleSubmit((values) => createMutation.mutate(values))}
          >
            <div className="space-y-2">
              <Label htmlFor="create-role-name">{t('admin.roles.fields.name')}</Label>
              <Input id="create-role-name" {...form.register('name')} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="create-role-description">{t('admin.roles.fields.description')}</Label>
              <Input id="create-role-description" {...form.register('description')} />
            </div>
            <div className="space-y-2">
              <Label>{t('admin.roles.fields.permissions')}</Label>
              <PermissionPicker />
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
            <DialogTitle>{t('admin.roles.editTitle')}</DialogTitle>
            <DialogDescription>{editing?.name}</DialogDescription>
          </DialogHeader>
          <form
            className="space-y-4"
            onSubmit={form.handleSubmit((values) => updateMutation.mutate(values))}
          >
            <div className="space-y-2">
              <Label htmlFor="edit-role-name">{t('admin.roles.fields.name')}</Label>
              <Input
                id="edit-role-name"
                disabled={editing?.isSystem}
                {...form.register('name')}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="edit-role-description">{t('admin.roles.fields.description')}</Label>
              <Input id="edit-role-description" {...form.register('description')} />
            </div>
            <div className="space-y-2">
              <Label>{t('admin.roles.fields.permissions')}</Label>
              <PermissionPicker />
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
