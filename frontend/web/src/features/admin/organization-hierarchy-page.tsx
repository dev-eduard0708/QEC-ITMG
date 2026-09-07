import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Plus, Users } from 'lucide-react'
import {
  ApiError,
  organizationHierarchyApi,
  type OrganizationPositionNode,
  type OrganizationPositionOccupant,
  type OrganizationUserPosition,
} from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
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
import { Sheet, SheetContent } from '@/components/ui/sheet'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { cn } from '@/lib/utils'

const hierarchyKeys = {
  departments: ['organization', 'departments'] as const,
  hierarchy: (departmentId?: string) =>
    ['organization', 'hierarchy', departmentId ?? 'default'] as const,
  positions: (departmentId?: string) =>
    ['organization', 'positions', departmentId ?? 'all'] as const,
  userPositions: (userId: string) => ['organization', 'user-positions', userId] as const,
  activeUsers: (search: string) => ['organization', 'active-users', search] as const,
}

function initials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean)
  if (parts.length === 0) return '?'
  if (parts.length === 1) return parts[0]!.slice(0, 2).toUpperCase()
  return `${parts[0]![0] ?? ''}${parts[1]![0] ?? ''}`.toUpperCase()
}

function localizedName(
  language: string,
  nameEn: string,
  nameAr: string,
): string {
  return language.startsWith('ar') ? nameAr || nameEn : nameEn || nameAr
}

function flattenPositions(nodes: OrganizationPositionNode[]): OrganizationPositionNode[] {
  const result: OrganizationPositionNode[] = []
  const walk = (list: OrganizationPositionNode[]) => {
    for (const node of list) {
      result.push(node)
      walk(node.children)
    }
  }
  walk(nodes)
  return result
}

function collectDescendantIds(node: OrganizationPositionNode): Set<string> {
  const ids = new Set<string>([node.id])
  for (const child of node.children) {
    for (const id of collectDescendantIds(child)) ids.add(id)
  }
  return ids
}

function findNode(
  nodes: OrganizationPositionNode[],
  id: string,
): OrganizationPositionNode | null {
  for (const node of nodes) {
    if (node.id === id) return node
    const found = findNode(node.children, id)
    if (found) return found
  }
  return null
}

type PositionFormState = {
  key: string
  nameEn: string
  nameAr: string
  descriptionEn: string
  descriptionAr: string
  parentPositionId: string
  isManagerial: boolean
  sortOrder: string
  isActive: boolean
}

const emptyForm = (): PositionFormState => ({
  key: '',
  nameEn: '',
  nameAr: '',
  descriptionEn: '',
  descriptionAr: '',
  parentPositionId: '',
  isManagerial: false,
  sortOrder: '100',
  isActive: true,
})

export function OrganizationHierarchyPage() {
  const { t, i18n } = useTranslation()
  const { can } = useAuth()
  const canManage = can('organization.hierarchy.manage')
  const queryClient = useQueryClient()
  const language = i18n.language ?? 'en'

  const [departmentId, setDepartmentId] = useState<string>('')
  const [selectedPositionId, setSelectedPositionId] = useState<string | null>(null)
  const [selectedUser, setSelectedUser] = useState<OrganizationPositionOccupant | null>(null)
  const [createOpen, setCreateOpen] = useState(false)
  const [editOpen, setEditOpen] = useState(false)
  const [parentOpen, setParentOpen] = useState(false)
  const [assignOpen, setAssignOpen] = useState(false)
  const [form, setForm] = useState<PositionFormState>(emptyForm)
  const [parentId, setParentId] = useState<string>('')
  const [assignSearch, setAssignSearch] = useState('')
  const [formError, setFormError] = useState<string | null>(null)

  const departmentsQuery = useQuery({
    queryKey: hierarchyKeys.departments,
    queryFn: () => organizationHierarchyApi.listDepartments(),
  })

  const resolvedDepartmentId = useMemo(() => {
    if (departmentId) return departmentId
    const departments = departmentsQuery.data ?? []
    const it = departments.find((d) => d.name === 'IT')
    return it?.id ?? departments[0]?.id ?? ''
  }, [departmentId, departmentsQuery.data])

  const hierarchyQuery = useQuery({
    queryKey: hierarchyKeys.hierarchy(resolvedDepartmentId || undefined),
    queryFn: () =>
      organizationHierarchyApi.getHierarchy(resolvedDepartmentId || undefined),
    enabled: Boolean(resolvedDepartmentId) || departmentsQuery.isSuccess,
  })

  const roots = hierarchyQuery.data?.roots ?? []
  const flat = flattenPositions(roots)
  const selectedPosition = selectedPositionId
    ? findNode(roots, selectedPositionId)
    : null

  const userPositionsQuery = useQuery({
    queryKey: hierarchyKeys.userPositions(selectedUser?.userId ?? ''),
    queryFn: () => organizationHierarchyApi.listUserPositions(selectedUser!.userId),
    enabled: Boolean(selectedUser?.userId),
  })

  const activeUsersQuery = useQuery({
    queryKey: hierarchyKeys.activeUsers(assignSearch),
    queryFn: () => organizationHierarchyApi.searchActiveUsers(assignSearch),
    enabled: assignOpen && canManage,
  })

  const invalidate = async () => {
    await queryClient.invalidateQueries({ queryKey: ['organization'] })
  }

  const createMutation = useMutation({
    mutationFn: () =>
      organizationHierarchyApi.createPosition({
        departmentId: resolvedDepartmentId,
        key: form.key,
        nameEn: form.nameEn,
        nameAr: form.nameAr,
        descriptionEn: form.descriptionEn || null,
        descriptionAr: form.descriptionAr || null,
        parentPositionId: form.parentPositionId || null,
        isManagerial: form.isManagerial,
        sortOrder: Number(form.sortOrder) || 0,
        isActive: form.isActive,
      }),
    onSuccess: async () => {
      setCreateOpen(false)
      setForm(emptyForm())
      setFormError(null)
      await invalidate()
    },
    onError: (error: unknown) => {
      setFormError(error instanceof ApiError ? error.message : t('admin.error.generic'))
    },
  })

  const updateMutation = useMutation({
    mutationFn: () =>
      organizationHierarchyApi.updatePosition(selectedPosition!.id, {
        nameEn: form.nameEn,
        nameAr: form.nameAr,
        descriptionEn: form.descriptionEn || null,
        descriptionAr: form.descriptionAr || null,
        isManagerial: form.isManagerial,
        sortOrder: Number(form.sortOrder) || 0,
        isActive: form.isActive,
      }),
    onSuccess: async () => {
      setEditOpen(false)
      setFormError(null)
      await invalidate()
    },
    onError: (error: unknown) => {
      setFormError(error instanceof ApiError ? error.message : t('admin.error.generic'))
    },
  })

  const parentMutation = useMutation({
    mutationFn: () =>
      organizationHierarchyApi.changeParent(
        selectedPosition!.id,
        parentId || null,
      ),
    onSuccess: async () => {
      setParentOpen(false)
      setFormError(null)
      await invalidate()
    },
    onError: (error: unknown) => {
      setFormError(error instanceof ApiError ? error.message : t('admin.error.generic'))
    },
  })

  const deactivateMutation = useMutation({
    mutationFn: () => organizationHierarchyApi.deactivatePosition(selectedPosition!.id),
    onSuccess: async () => {
      await invalidate()
    },
  })

  const assignMutation = useMutation({
    mutationFn: (userId: string) =>
      organizationHierarchyApi.assignUser(selectedPosition!.id, userId),
    onSuccess: async () => {
      setAssignOpen(false)
      setAssignSearch('')
      await invalidate()
    },
    onError: (error: unknown) => {
      setFormError(error instanceof ApiError ? error.message : t('admin.error.generic'))
    },
  })

  const removeMutation = useMutation({
    mutationFn: (args: { positionId: string; assignmentId: string }) =>
      organizationHierarchyApi.removeAssignment(args.positionId, args.assignmentId),
    onSuccess: async () => {
      await invalidate()
      if (selectedUser) {
        await queryClient.invalidateQueries({
          queryKey: hierarchyKeys.userPositions(selectedUser.userId),
        })
      }
    },
  })

  const setPrimaryMutation = useMutation({
    mutationFn: (args: { positionId: string; assignmentId: string }) =>
      organizationHierarchyApi.setPrimary(args.positionId, args.assignmentId),
    onSuccess: async () => {
      await invalidate()
      if (selectedUser) {
        await queryClient.invalidateQueries({
          queryKey: hierarchyKeys.userPositions(selectedUser.userId),
        })
      }
    },
  })

  const openCreate = () => {
    setForm(emptyForm())
    setFormError(null)
    setCreateOpen(true)
  }

  const openEdit = () => {
    if (!selectedPosition) return
    setForm({
      key: selectedPosition.key,
      nameEn: selectedPosition.nameEn,
      nameAr: selectedPosition.nameAr,
      descriptionEn: selectedPosition.descriptionEn ?? '',
      descriptionAr: selectedPosition.descriptionAr ?? '',
      parentPositionId: selectedPosition.parentPositionId ?? '',
      isManagerial: selectedPosition.isManagerial,
      sortOrder: String(selectedPosition.sortOrder),
      isActive: selectedPosition.isActive,
    })
    setFormError(null)
    setEditOpen(true)
  }

  const openChangeParent = () => {
    if (!selectedPosition) return
    setParentId(selectedPosition.parentPositionId ?? '')
    setFormError(null)
    setParentOpen(true)
  }

  const parentOptions = (() => {
    if (!selectedPosition) return flat
    const blocked = collectDescendantIds(selectedPosition)
    return flat.filter((p) => !blocked.has(p.id))
  })()

  const assignedUserIds = new Set((selectedPosition?.occupants ?? []).map((o) => o.userId))

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('admin.hierarchy.title')}
        description={t('admin.hierarchy.description')}
      />

      <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div className="w-full max-w-xs space-y-1.5">
          <Label>{t('admin.hierarchy.department')}</Label>
          <Select
            value={resolvedDepartmentId}
            onValueChange={(value) => setDepartmentId(value)}
          >
            <SelectTrigger>
              <SelectValue placeholder={t('admin.hierarchy.department')} />
            </SelectTrigger>
            <SelectContent>
              {(departmentsQuery.data ?? []).map((dept) => (
                <SelectItem key={dept.id} value={dept.id}>
                  {dept.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        {canManage ? (
          <Button onClick={openCreate}>
            <Plus className="me-1.5 h-4 w-4" />
            {t('admin.hierarchy.addPosition')}
          </Button>
        ) : null}
      </div>

      {hierarchyQuery.isLoading ? (
        <div className="space-y-3">
          <Skeleton className="h-28 w-full max-w-sm" />
          <Skeleton className="h-28 w-full max-w-sm" />
        </div>
      ) : roots.length === 0 ? (
        <p className="rounded-md border border-dashed border-border p-8 text-center text-sm text-muted-foreground">
          {t('admin.hierarchy.empty')}
        </p>
      ) : (
        <div className="overflow-x-auto pb-4">
          <div className="inline-flex min-w-full flex-col items-stretch gap-0 md:items-center">
            {roots.map((root) => (
              <PositionTree
                key={root.id}
                node={root}
                language={language}
                onSelectPosition={setSelectedPositionId}
                onSelectUser={setSelectedUser}
              />
            ))}
          </div>
        </div>
      )}

      <Sheet
        open={Boolean(selectedPosition)}
        onOpenChange={(open) => {
          if (!open) setSelectedPositionId(null)
        }}
      >
        <SheetContent className="inset-y-0 start-auto end-0 w-full max-w-md overflow-y-auto border-s bg-background p-6 text-foreground shadow-xl">
          {selectedPosition ? (
            <div className="space-y-5 pe-6">
              <div>
                <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                  {t('admin.hierarchy.position')}
                </p>
                <h3 className="mt-1 text-xl font-semibold">
                  {localizedName(language, selectedPosition.nameEn, selectedPosition.nameAr)}
                </h3>
                <p className="mt-1 text-xs text-muted-foreground">{selectedPosition.key}</p>
                {!selectedPosition.isActive ? (
                  <Badge variant="secondary" className="mt-2">
                    {t('admin.hierarchy.inactive')}
                  </Badge>
                ) : null}
              </div>

              <div className="space-y-1 text-sm">
                <p className="font-medium text-muted-foreground">
                  {t('admin.hierarchy.parentPosition')}
                </p>
                <p>
                  {selectedPosition.parentPositionId
                    ? localizedName(
                        language,
                        findNode(roots, selectedPosition.parentPositionId)?.nameEn ?? '—',
                        findNode(roots, selectedPosition.parentPositionId)?.nameAr ?? '—',
                      )
                    : t('admin.hierarchy.noParent')}
                </p>
              </div>

              {(selectedPosition.descriptionEn || selectedPosition.descriptionAr) && (
                <div className="space-y-1 text-sm">
                  <p className="font-medium text-muted-foreground">
                    {t('admin.hierarchy.descriptionLabel')}
                  </p>
                  <p>
                    {language.startsWith('ar')
                      ? selectedPosition.descriptionAr || selectedPosition.descriptionEn
                      : selectedPosition.descriptionEn || selectedPosition.descriptionAr}
                  </p>
                </div>
              )}

              <div className="space-y-3">
                <div className="flex items-center justify-between gap-2">
                  <h4 className="font-medium">{t('admin.hierarchy.assignedPeople')}</h4>
                  {canManage && selectedPosition.isActive ? (
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => {
                        setFormError(null)
                        setAssignSearch('')
                        setAssignOpen(true)
                      }}
                    >
                      <Plus className="me-1 h-3.5 w-3.5" />
                      {t('admin.hierarchy.assignUser')}
                    </Button>
                  ) : null}
                </div>
                {selectedPosition.occupants.length === 0 ? (
                  <p className="text-sm text-muted-foreground">{t('admin.hierarchy.vacant')}</p>
                ) : (
                  <ul className="space-y-2">
                    {selectedPosition.occupants.map((occupant) => (
                      <li key={occupant.assignmentId}>
                        <button
                          type="button"
                          className="flex w-full items-center gap-3 rounded-md border border-border px-3 py-2 text-start hover:bg-accent"
                          onClick={() => setSelectedUser(occupant)}
                        >
                          <UserAvatar occupant={occupant} />
                          <span className="min-w-0 flex-1">
                            <span className="block truncate text-sm font-medium">
                              {occupant.displayName}
                              {occupant.isPrimary ? (
                                <Badge className="ms-2" variant="secondary">
                                  {t('admin.hierarchy.primary')}
                                </Badge>
                              ) : null}
                            </span>
                            <span className="block truncate text-xs text-muted-foreground">
                              {occupant.upn}
                            </span>
                          </span>
                        </button>
                      </li>
                    ))}
                  </ul>
                )}
              </div>

              {canManage ? (
                <div className="flex flex-wrap gap-2 border-t border-border pt-4">
                  <Button variant="outline" size="sm" onClick={openEdit}>
                    {t('admin.hierarchy.editPosition')}
                  </Button>
                  <Button variant="outline" size="sm" onClick={openChangeParent}>
                    {t('admin.hierarchy.changeParent')}
                  </Button>
                  {selectedPosition.isActive ? (
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => deactivateMutation.mutate()}
                      disabled={deactivateMutation.isPending}
                    >
                      {t('admin.hierarchy.deactivate')}
                    </Button>
                  ) : null}
                </div>
              ) : null}
            </div>
          ) : null}
        </SheetContent>
      </Sheet>

      <Sheet
        open={Boolean(selectedUser)}
        onOpenChange={(open) => {
          if (!open) setSelectedUser(null)
        }}
      >
        <SheetContent className="inset-y-0 start-auto end-0 w-full max-w-md overflow-y-auto border-s bg-background p-6 text-foreground shadow-xl">
          {selectedUser ? (
            <div className="space-y-5 pe-6">
              <div className="flex items-center gap-3">
                <UserAvatar occupant={selectedUser} className="h-12 w-12 text-sm" />
                <div>
                  <h3 className="text-lg font-semibold">{selectedUser.displayName}</h3>
                  <p className="text-sm text-muted-foreground">{selectedUser.upn}</p>
                  {!selectedUser.isActiveUser ? (
                    <Badge variant="secondary" className="mt-1">
                      {t('admin.hierarchy.userInactive')}
                    </Badge>
                  ) : null}
                </div>
              </div>

              {can('admin.users') ? (
                <Button asChild variant="outline" size="sm">
                  <Link to="/it/admin/users">{t('admin.hierarchy.openUsersAdmin')}</Link>
                </Button>
              ) : null}

              <div className="space-y-3">
                <h4 className="font-medium">{t('admin.hierarchy.positions')}</h4>
                {userPositionsQuery.isLoading ? (
                  <Skeleton className="h-16 w-full" />
                ) : (userPositionsQuery.data ?? []).length === 0 ? (
                  <p className="text-sm text-muted-foreground">{t('admin.hierarchy.noUserPositions')}</p>
                ) : (
                  <UserPositionsList
                    language={language}
                    positions={userPositionsQuery.data ?? []}
                    canManage={canManage}
                    onSetPrimary={(positionId, assignmentId) =>
                      setPrimaryMutation.mutate({ positionId, assignmentId })
                    }
                    onRemove={(positionId, assignmentId) =>
                      removeMutation.mutate({ positionId, assignmentId })
                    }
                  />
                )}
              </div>
            </div>
          ) : null}
        </SheetContent>
      </Sheet>

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>{t('admin.hierarchy.addPosition')}</DialogTitle>
            <DialogDescription>{t('admin.hierarchy.addPositionHint')}</DialogDescription>
          </DialogHeader>
          <PositionFormFields
            form={form}
            setForm={setForm}
            parentChoices={flat}
            language={language}
            showKey
          />
          {formError ? <p className="text-sm text-destructive">{formError}</p> : null}
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)}>
              {t('admin.cancel')}
            </Button>
            <Button
              onClick={() => createMutation.mutate()}
              disabled={createMutation.isPending || !resolvedDepartmentId}
            >
              {t('admin.save')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={editOpen} onOpenChange={setEditOpen}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>{t('admin.hierarchy.editPosition')}</DialogTitle>
          </DialogHeader>
          <PositionFormFields
            form={form}
            setForm={setForm}
            parentChoices={flat}
            language={language}
            showKey={false}
          />
          {formError ? <p className="text-sm text-destructive">{formError}</p> : null}
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditOpen(false)}>
              {t('admin.cancel')}
            </Button>
            <Button onClick={() => updateMutation.mutate()} disabled={updateMutation.isPending}>
              {t('admin.save')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={parentOpen} onOpenChange={setParentOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('admin.hierarchy.changeParent')}</DialogTitle>
          </DialogHeader>
          <div className="space-y-2">
            <Label>{t('admin.hierarchy.parentPosition')}</Label>
            <Select
              value={parentId || '__none__'}
              onValueChange={(value) => setParentId(value === '__none__' ? '' : value)}
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="__none__">{t('admin.hierarchy.noParent')}</SelectItem>
                {parentOptions.map((p) => (
                  <SelectItem key={p.id} value={p.id}>
                    {localizedName(language, p.nameEn, p.nameAr)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          {formError ? <p className="text-sm text-destructive">{formError}</p> : null}
          <DialogFooter>
            <Button variant="outline" onClick={() => setParentOpen(false)}>
              {t('admin.cancel')}
            </Button>
            <Button onClick={() => parentMutation.mutate()} disabled={parentMutation.isPending}>
              {t('admin.save')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={assignOpen} onOpenChange={setAssignOpen}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>{t('admin.hierarchy.assignUser')}</DialogTitle>
            <DialogDescription>{t('admin.hierarchy.assignUserHint')}</DialogDescription>
          </DialogHeader>
          <Input
            value={assignSearch}
            onChange={(e) => setAssignSearch(e.target.value)}
            placeholder={t('admin.hierarchy.searchUsers')}
          />
          {formError ? <p className="text-sm text-destructive">{formError}</p> : null}
          <ul className="max-h-72 space-y-2 overflow-y-auto">
            {(activeUsersQuery.data ?? []).map((user) => {
              const already = assignedUserIds.has(user.id)
              return (
                <li
                  key={user.id}
                  className="flex items-center justify-between gap-3 rounded-md border border-border px-3 py-2"
                >
                  <div className="min-w-0">
                    <p className="truncate text-sm font-medium">{user.displayName}</p>
                    <p className="truncate text-xs text-muted-foreground">{user.upn}</p>
                  </div>
                  <Button
                    size="sm"
                    disabled={already || assignMutation.isPending}
                    onClick={() => assignMutation.mutate(user.id)}
                  >
                    {already ? t('admin.hierarchy.alreadyAssigned') : t('admin.hierarchy.assign')}
                  </Button>
                </li>
              )
            })}
          </ul>
        </DialogContent>
      </Dialog>
    </div>
  )
}

function UserAvatar({
  occupant,
  className,
}: {
  occupant: Pick<OrganizationPositionOccupant, 'displayName' | 'avatarUrl'>
  className?: string
}) {
  return (
    <Avatar className={cn('h-9 w-9', className)}>
      {occupant.avatarUrl ? (
        <AvatarImage src={occupant.avatarUrl} alt={occupant.displayName} />
      ) : null}
      <AvatarFallback>{initials(occupant.displayName)}</AvatarFallback>
    </Avatar>
  )
}

function PositionTree({
  node,
  language,
  onSelectPosition,
  onSelectUser,
}: {
  node: OrganizationPositionNode
  language: string
  onSelectPosition: (id: string) => void
  onSelectUser: (occupant: OrganizationPositionOccupant) => void
}) {
  return (
    <div className="flex w-full flex-col items-stretch md:items-center">
      <PositionCard
        node={node}
        language={language}
        onSelectPosition={onSelectPosition}
        onSelectUser={onSelectUser}
      />
      {node.children.length > 0 ? (
        <div className="flex w-full flex-col items-stretch md:items-center">
          <div className="mx-auto h-6 w-px bg-border" aria-hidden />
          <div
            className={cn(
              'flex w-full flex-col gap-6 md:flex-row md:justify-center md:gap-8',
              node.children.length > 1 && 'md:relative',
            )}
          >
            {node.children.length > 1 ? (
              <div
                className="pointer-events-none absolute start-[12.5%] end-[12.5%] top-0 hidden h-px bg-border md:block"
                aria-hidden
              />
            ) : null}
            {node.children.map((child) => (
              <div key={child.id} className="relative flex min-w-0 flex-1 flex-col items-stretch md:items-center">
                <div className="mx-auto hidden h-6 w-px bg-border md:block" aria-hidden />
                <div className="ms-4 border-s border-border ps-4 md:ms-0 md:border-0 md:ps-0">
                  <PositionTree
                    node={child}
                    language={language}
                    onSelectPosition={onSelectPosition}
                    onSelectUser={onSelectUser}
                  />
                </div>
              </div>
            ))}
          </div>
        </div>
      ) : null}
    </div>
  )
}

function PositionCard({
  node,
  language,
  onSelectPosition,
  onSelectUser,
}: {
  node: OrganizationPositionNode
  language: string
  onSelectPosition: (id: string) => void
  onSelectUser: (occupant: OrganizationPositionOccupant) => void
}) {
  const { t } = useTranslation()
  const visible = node.occupants.slice(0, 3)
  const extra = Math.max(0, node.occupants.length - visible.length)

  return (
    <button
      type="button"
      onClick={() => onSelectPosition(node.id)}
      className={cn(
        'w-full max-w-sm rounded-lg border border-border bg-card p-4 text-start shadow-sm transition hover:border-primary/40 hover:shadow-md',
        !node.isActive && 'opacity-70',
      )}
    >
      <div className="flex items-start justify-between gap-2">
        <div>
          <p className="font-semibold text-foreground">
            {localizedName(language, node.nameEn, node.nameAr)}
          </p>
          {node.isManagerial ? (
            <Badge variant="secondary" className="mt-1">
              {t('admin.hierarchy.managerial')}
            </Badge>
          ) : null}
        </div>
        <span className="inline-flex items-center gap-1 text-xs text-muted-foreground">
          <Users className="h-3.5 w-3.5" />
          {node.occupants.length}
        </span>
      </div>

      <div className="mt-3 space-y-2">
        {node.occupants.length === 0 ? (
          <p className="text-sm italic text-muted-foreground">{t('admin.hierarchy.vacant')}</p>
        ) : (
          visible.map((occupant) => (
            <div
              key={occupant.assignmentId}
              className="flex items-center gap-2"
              onClick={(e) => {
                e.stopPropagation()
                onSelectUser(occupant)
              }}
              onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') {
                  e.stopPropagation()
                  onSelectUser(occupant)
                }
              }}
              role="link"
              tabIndex={0}
            >
              <UserAvatar occupant={occupant} className="h-8 w-8" />
              <span className="min-w-0">
                <span className="block truncate text-sm font-medium">{occupant.displayName}</span>
                <span className="block truncate text-xs text-muted-foreground">{occupant.upn}</span>
              </span>
            </div>
          ))
        )}
        {extra > 0 ? (
          <p className="text-xs text-muted-foreground">
            {t('admin.hierarchy.moreOccupants', { count: extra })}
          </p>
        ) : null}
      </div>

      <p className="mt-3 text-xs text-muted-foreground">
        {t('admin.hierarchy.occupantCount', { count: node.occupants.length })}
      </p>
    </button>
  )
}

function PositionFormFields({
  form,
  setForm,
  parentChoices,
  language,
  showKey,
}: {
  form: PositionFormState
  setForm: (next: PositionFormState) => void
  parentChoices: OrganizationPositionNode[]
  language: string
  showKey: boolean
}) {
  const { t } = useTranslation()
  return (
    <div className="grid gap-3">
      {showKey ? (
        <div className="space-y-1.5">
          <Label htmlFor="pos-key">{t('admin.hierarchy.fields.key')}</Label>
          <Input
            id="pos-key"
            value={form.key}
            onChange={(e) => setForm({ ...form, key: e.target.value })}
          />
        </div>
      ) : null}
      <div className="grid gap-3 sm:grid-cols-2">
        <div className="space-y-1.5">
          <Label htmlFor="pos-name-en">{t('admin.hierarchy.fields.nameEn')}</Label>
          <Input
            id="pos-name-en"
            value={form.nameEn}
            onChange={(e) => setForm({ ...form, nameEn: e.target.value })}
          />
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="pos-name-ar">{t('admin.hierarchy.fields.nameAr')}</Label>
          <Input
            id="pos-name-ar"
            value={form.nameAr}
            onChange={(e) => setForm({ ...form, nameAr: e.target.value })}
            dir="rtl"
          />
        </div>
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="pos-desc-en">{t('admin.hierarchy.fields.descriptionEn')}</Label>
        <Textarea
          id="pos-desc-en"
          value={form.descriptionEn}
          onChange={(e) => setForm({ ...form, descriptionEn: e.target.value })}
        />
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="pos-desc-ar">{t('admin.hierarchy.fields.descriptionAr')}</Label>
        <Textarea
          id="pos-desc-ar"
          value={form.descriptionAr}
          onChange={(e) => setForm({ ...form, descriptionAr: e.target.value })}
          dir="rtl"
        />
      </div>
      {showKey ? (
        <div className="space-y-1.5">
          <Label>{t('admin.hierarchy.parentPosition')}</Label>
          <Select
            value={form.parentPositionId || '__none__'}
            onValueChange={(value) =>
              setForm({ ...form, parentPositionId: value === '__none__' ? '' : value })
            }
          >
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="__none__">{t('admin.hierarchy.noParent')}</SelectItem>
              {parentChoices.map((p) => (
                <SelectItem key={p.id} value={p.id}>
                  {localizedName(language, p.nameEn, p.nameAr)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      ) : null}
      <div className="space-y-1.5">
        <Label htmlFor="pos-sort">{t('admin.hierarchy.fields.sortOrder')}</Label>
        <Input
          id="pos-sort"
          type="number"
          value={form.sortOrder}
          onChange={(e) => setForm({ ...form, sortOrder: e.target.value })}
        />
      </div>
      <label className="flex items-center gap-2 text-sm">
        <Checkbox
          checked={form.isManagerial}
          onCheckedChange={(checked) =>
            setForm({ ...form, isManagerial: checked === true })
          }
        />
        {t('admin.hierarchy.managerial')}
      </label>
      <label className="flex items-center gap-2 text-sm">
        <Checkbox
          checked={form.isActive}
          onCheckedChange={(checked) => setForm({ ...form, isActive: checked === true })}
        />
        {t('admin.hierarchy.active')}
      </label>
    </div>
  )
}

function UserPositionsList({
  language,
  positions,
  canManage,
  onSetPrimary,
  onRemove,
}: {
  language: string
  positions: OrganizationUserPosition[]
  canManage: boolean
  onSetPrimary: (positionId: string, assignmentId: string) => void
  onRemove: (positionId: string, assignmentId: string) => void
}) {
  const { t } = useTranslation()
  const primary = positions.filter((p) => p.isPrimary)
  const additional = positions.filter((p) => !p.isPrimary)

  const renderRow = (item: OrganizationUserPosition) => (
    <li
      key={item.assignmentId}
      className="rounded-md border border-border px-3 py-2 text-sm"
    >
      <p className="font-medium">
        {localizedName(language, item.positionNameEn, item.positionNameAr)}
      </p>
      {canManage ? (
        <div className="mt-2 flex flex-wrap gap-2">
          {!item.isPrimary ? (
            <Button
              size="sm"
              variant="outline"
              onClick={() => onSetPrimary(item.positionId, item.assignmentId)}
            >
              {t('admin.hierarchy.setPrimary')}
            </Button>
          ) : null}
          <Button
            size="sm"
            variant="outline"
            onClick={() => onRemove(item.positionId, item.assignmentId)}
          >
            {t('admin.hierarchy.removeAssignment')}
          </Button>
        </div>
      ) : null}
    </li>
  )

  const remainder = primary.length > 0 ? additional : positions

  return (
    <div className="space-y-4">
      {primary.length > 0 ? (
        <div className="space-y-2">
          <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            {t('admin.hierarchy.primaryPosition')}
          </p>
          <ul className="space-y-2">{primary.map(renderRow)}</ul>
        </div>
      ) : null}
      {remainder.length > 0 ? (
        <div className="space-y-2">
          <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            {primary.length > 0
              ? t('admin.hierarchy.additionalPositions')
              : t('admin.hierarchy.positions')}
          </p>
          <ul className="space-y-2">{remainder.map(renderRow)}</ul>
        </div>
      ) : null}
    </div>
  )
}
