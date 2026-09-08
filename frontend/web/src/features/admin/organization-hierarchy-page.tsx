import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Plus, Users } from 'lucide-react'
import {
  ApiError,
  organizationHierarchyApi,
  type OrganizationActiveUser,
  type OrganizationCompanyDepartmentCard,
  type OrganizationDepartmentMember,
  type OrganizationDepartmentSummary,
  type OrganizationPosition,
  type OrganizationPositionNode,
  type OrganizationPositionOccupant,
  type OrganizationUnitType,
  type OrganizationUserPosition,
} from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { UserAvatar } from '@/components/user-avatar'
import {
  buildOrganizationDepartmentForest,
  buildUnitBreadcrumb,
  OrganizationDepartmentTree,
} from '@/features/admin/organization-department-tree'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
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
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
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
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { Textarea } from '@/components/ui/textarea'
import { cn } from '@/lib/utils'

const hierarchyKeys = {
  departments: ['organization', 'departments'] as const,
  companyView: ['organization', 'company-view'] as const,
  hierarchy: (departmentId?: string) =>
    ['organization', 'hierarchy', departmentId ?? 'default'] as const,
  positions: (departmentId?: string) =>
    ['organization', 'positions', departmentId ?? 'all'] as const,
  members: (departmentId: string) => ['organization', 'members', departmentId] as const,
  people: (search: string, departmentId: string, status: string) =>
    ['organization', 'people', search, departmentId, status] as const,
  profile: (userId: string) => ['organization', 'profile', userId] as const,
  activeUsers: (search: string, departmentId: string, searchAll: boolean) =>
    ['organization', 'active-users', search, departmentId, searchAll] as const,
}

type HierarchyTab = 'company' | 'departments' | 'people' | 'positions'
type PeopleStatusFilter = 'all' | 'active' | 'inactive'
type PositionsViewMode = 'hierarchy' | 'list'

function localizedName(language: string, nameEn: string, nameAr: string | null | undefined): string {
  return language.startsWith('ar') ? nameAr || nameEn : nameEn || nameAr || ''
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

function findDefaultDepartmentId(departments: OrganizationDepartmentSummary[]): string {
  const it =
    departments.find((d) => (d.code ?? '').toUpperCase() === 'IT') ??
    departments.find((d) => (d.nameEn ?? d.name ?? '').toUpperCase() === 'IT')
  return it?.id ?? departments[0]?.id ?? ''
}

function departmentLabel(
  language: string,
  dept: Pick<OrganizationDepartmentSummary, 'nameEn' | 'nameAr' | 'name'>,
): string {
  return localizedName(language, dept.nameEn || dept.name || '', dept.nameAr)
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

const emptyPositionForm = (): PositionFormState => ({
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

type DepartmentFormState = {
  nameEn: string
  nameAr: string
  code: string
  descriptionEn: string
  descriptionAr: string
  parentDepartmentId: string
  sortOrder: string
  isActive: boolean
  unitType: OrganizationUnitType
}

const UNIT_TYPE_OPTIONS: OrganizationUnitType[] = [
  'Company',
  'Department',
  'Section',
  'Office',
  'Team',
  'Other',
]

const emptyDepartmentForm = (): DepartmentFormState => ({
  nameEn: '',
  nameAr: '',
  code: '',
  descriptionEn: '',
  descriptionAr: '',
  parentDepartmentId: '',
  sortOrder: '100',
  isActive: true,
  unitType: 'Department',
})

function collectDepartmentDescendantIds(
  departments: OrganizationDepartmentSummary[],
  rootId: string,
): Set<string> {
  const childrenByParent = new Map<string, string[]>()
  for (const dept of departments) {
    if (!dept.parentDepartmentId) continue
    const list = childrenByParent.get(dept.parentDepartmentId) ?? []
    list.push(dept.id)
    childrenByParent.set(dept.parentDepartmentId, list)
  }
  const ids = new Set<string>()
  const walk = (id: string) => {
    for (const childId of childrenByParent.get(id) ?? []) {
      if (ids.has(childId)) continue
      ids.add(childId)
      walk(childId)
    }
  }
  walk(rootId)
  return ids
}

export function OrganizationHierarchyPage() {
  const { t, i18n } = useTranslation()
  const { can } = useAuth()
  const canManage = can('organization.hierarchy.manage')
  const queryClient = useQueryClient()
  const language = i18n.language ?? 'en'

  const [tab, setTab] = useState<HierarchyTab>('company')
  const [departmentId, setDepartmentId] = useState<string>('')
  const [selectedPositionId, setSelectedPositionId] = useState<string | null>(null)
  const [profileUserId, setProfileUserId] = useState<string | null>(null)
  const [createOpen, setCreateOpen] = useState(false)
  const [editOpen, setEditOpen] = useState(false)
  const [parentOpen, setParentOpen] = useState(false)
  const [assignOpen, setAssignOpen] = useState(false)
  const [form, setForm] = useState<PositionFormState>(emptyPositionForm)
  const [parentId, setParentId] = useState<string>('')
  const [assignSearch, setAssignSearch] = useState('')
  const [searchAllUsers, setSearchAllUsers] = useState(false)
  const [pendingAssignUser, setPendingAssignUser] = useState<OrganizationActiveUser | null>(null)
  const [formError, setFormError] = useState<string | null>(null)

  const [manageDepartmentsOpen, setManageDepartmentsOpen] = useState(false)
  const [membersDepartmentId, setMembersDepartmentId] = useState<string | null>(null)
  const [departmentFormOpen, setDepartmentFormOpen] = useState(false)
  const [editingDepartment, setEditingDepartment] = useState<OrganizationDepartmentSummary | null>(
    null,
  )
  const [departmentForm, setDepartmentForm] = useState<DepartmentFormState>(emptyDepartmentForm)
  const [departmentFormError, setDepartmentFormError] = useState<string | null>(null)
  const [addMemberOpen, setAddMemberOpen] = useState(false)
  const [memberSearch, setMemberSearch] = useState('')
  const [selectedMemberIds, setSelectedMemberIds] = useState<string[]>([])
  const [addMemberAsPrimary, setAddMemberAsPrimary] = useState(false)
  const [memberActionError, setMemberActionError] = useState<string | null>(null)
  const [moveDepartmentId, setMoveDepartmentId] = useState<string | null>(null)
  const [moveParentId, setMoveParentId] = useState<string>('')
  const [moveError, setMoveError] = useState<string | null>(null)
  const [unitActionError, setUnitActionError] = useState<string | null>(null)
  const [selectedCompanyUnitId, setSelectedCompanyUnitId] = useState<string | null>(null)
  const [assignMemberPositionOpen, setAssignMemberPositionOpen] = useState(false)
  const [assignMemberUserId, setAssignMemberUserId] = useState<string | null>(null)
  const [assignMemberPositionId, setAssignMemberPositionId] = useState<string>('')

  const [peopleSearch, setPeopleSearch] = useState('')
  const [peopleDepartmentId, setPeopleDepartmentId] = useState<string>('__all__')
  const [peopleStatus, setPeopleStatus] = useState<PeopleStatusFilter>('all')
  const [positionsViewMode, setPositionsViewMode] = useState<PositionsViewMode>('hierarchy')

  const departmentsQuery = useQuery({
    queryKey: hierarchyKeys.departments,
    queryFn: () => organizationHierarchyApi.listDepartments(),
  })

  const companyViewQuery = useQuery({
    queryKey: hierarchyKeys.companyView,
    queryFn: () => organizationHierarchyApi.companyView(),
    enabled: tab === 'company',
  })

  const resolvedDepartmentId = useMemo(() => {
    if (departmentId) return departmentId
    return findDefaultDepartmentId(departmentsQuery.data ?? [])
  }, [departmentId, departmentsQuery.data])

  const hierarchyQuery = useQuery({
    queryKey: hierarchyKeys.hierarchy(resolvedDepartmentId || undefined),
    queryFn: () =>
      organizationHierarchyApi.getHierarchy(resolvedDepartmentId || undefined),
    enabled:
      Boolean(resolvedDepartmentId) &&
      (tab === 'departments' || tab === 'positions' || Boolean(selectedPositionId)),
  })

  const positionsListQuery = useQuery({
    queryKey: hierarchyKeys.positions(resolvedDepartmentId || undefined),
    queryFn: () => organizationHierarchyApi.listPositions(resolvedDepartmentId || undefined),
    enabled: tab === 'positions' && positionsViewMode === 'list' && Boolean(resolvedDepartmentId),
  })

  const peopleQuery = useQuery({
    queryKey: hierarchyKeys.people(peopleSearch, peopleDepartmentId, peopleStatus),
    queryFn: () =>
      organizationHierarchyApi.listPeople({
        search: peopleSearch || undefined,
        departmentId: peopleDepartmentId === '__all__' ? undefined : peopleDepartmentId,
        activeOnly: peopleStatus === 'active' ? true : undefined,
      }),
    enabled: tab === 'people',
  })

  const profileQuery = useQuery({
    queryKey: hierarchyKeys.profile(profileUserId ?? ''),
    queryFn: () => organizationHierarchyApi.getProfileSummary(profileUserId!),
    enabled: Boolean(profileUserId),
  })

  const membersQuery = useQuery({
    queryKey: hierarchyKeys.members(membersDepartmentId ?? ''),
    queryFn: () => organizationHierarchyApi.listMembers(membersDepartmentId!),
    enabled: Boolean(membersDepartmentId),
  })

  const roots = hierarchyQuery.data?.roots ?? []
  const flat = flattenPositions(roots)
  const selectedPosition = selectedPositionId
    ? findNode(roots, selectedPositionId)
    : null

  const assignDepartmentId = selectedPosition?.departmentId ?? resolvedDepartmentId

  const assignMembersQuery = useQuery({
    queryKey: hierarchyKeys.members(assignDepartmentId || ''),
    queryFn: () => organizationHierarchyApi.listMembers(assignDepartmentId!),
    enabled: assignOpen && Boolean(assignDepartmentId),
  })

  const assignMemberIds = useMemo(
    () => new Set((assignMembersQuery.data ?? []).map((m) => m.userId)),
    [assignMembersQuery.data],
  )

  const activeUsersQuery = useQuery({
    queryKey: hierarchyKeys.activeUsers(
      assignSearch,
      assignDepartmentId || '',
      searchAllUsers,
    ),
    queryFn: () =>
      organizationHierarchyApi.searchActiveUsers(assignSearch, {
        departmentId: searchAllUsers ? undefined : assignDepartmentId || undefined,
        searchAll: searchAllUsers || undefined,
      }),
    enabled: assignOpen && canManage,
  })

  const addMemberUsersQuery = useQuery({
    queryKey: hierarchyKeys.activeUsers(memberSearch, '', true),
    queryFn: () =>
      organizationHierarchyApi.searchActiveUsers(memberSearch, { searchAll: true }),
    enabled: addMemberOpen && canManage,
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
      setForm(emptyPositionForm())
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
      organizationHierarchyApi.changeParent(selectedPosition!.id, parentId || null),
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
    mutationFn: (args: { userId: string; addToDepartment?: boolean }) =>
      organizationHierarchyApi.assignUser(selectedPosition!.id, args.userId, {
        addToDepartment: args.addToDepartment,
      }),
    onSuccess: async () => {
      setAssignOpen(false)
      setAssignSearch('')
      setSearchAllUsers(false)
      setPendingAssignUser(null)
      setFormError(null)
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
      if (profileUserId) {
        await queryClient.invalidateQueries({
          queryKey: hierarchyKeys.profile(profileUserId),
        })
      }
    },
  })

  const setPrimaryMutation = useMutation({
    mutationFn: (args: { positionId: string; assignmentId: string }) =>
      organizationHierarchyApi.setPrimary(args.positionId, args.assignmentId),
    onSuccess: async () => {
      await invalidate()
      if (profileUserId) {
        await queryClient.invalidateQueries({
          queryKey: hierarchyKeys.profile(profileUserId),
        })
      }
    },
  })

  const createDepartmentMutation = useMutation({
    mutationFn: () =>
      organizationHierarchyApi.createDepartment({
        nameEn: departmentForm.nameEn,
        nameAr: departmentForm.nameAr || null,
        code: departmentForm.code,
        descriptionEn: departmentForm.descriptionEn || null,
        descriptionAr: departmentForm.descriptionAr || null,
        parentDepartmentId: departmentForm.parentDepartmentId || null,
        sortOrder: Number(departmentForm.sortOrder) || 0,
        unitType: departmentForm.unitType,
      }),
    onSuccess: async () => {
      setDepartmentFormOpen(false)
      setDepartmentForm(emptyDepartmentForm())
      setEditingDepartment(null)
      setDepartmentFormError(null)
      await invalidate()
    },
    onError: (error: unknown) => {
      setDepartmentFormError(
        error instanceof ApiError ? error.message : t('admin.error.generic'),
      )
    },
  })

  const updateDepartmentMutation = useMutation({
    mutationFn: () =>
      organizationHierarchyApi.updateDepartment(editingDepartment!.id, {
        nameEn: departmentForm.nameEn,
        nameAr: departmentForm.nameAr || null,
        code: departmentForm.code,
        descriptionEn: departmentForm.descriptionEn || null,
        descriptionAr: departmentForm.descriptionAr || null,
        parentDepartmentId: departmentForm.parentDepartmentId || null,
        sortOrder: Number(departmentForm.sortOrder) || 0,
        isActive: departmentForm.isActive,
        unitType: departmentForm.unitType,
      }),
    onSuccess: async () => {
      setDepartmentFormOpen(false)
      setDepartmentForm(emptyDepartmentForm())
      setEditingDepartment(null)
      setDepartmentFormError(null)
      await invalidate()
    },
    onError: (error: unknown) => {
      setDepartmentFormError(
        error instanceof ApiError ? error.message : t('admin.error.generic'),
      )
    },
  })

  const deactivateDepartmentMutation = useMutation({
    mutationFn: (id: string) => organizationHierarchyApi.deactivateDepartment(id),
    onSuccess: async () => {
      setUnitActionError(null)
      await invalidate()
    },
    onError: (error: unknown) => {
      setUnitActionError(error instanceof ApiError ? error.message : t('admin.error.generic'))
    },
  })

  const reactivateDepartmentMutation = useMutation({
    mutationFn: (id: string) => organizationHierarchyApi.reactivateDepartment(id),
    onSuccess: async () => {
      setUnitActionError(null)
      await invalidate()
    },
    onError: (error: unknown) => {
      setUnitActionError(error instanceof ApiError ? error.message : t('admin.error.generic'))
    },
  })

  const moveDepartmentMutation = useMutation({
    mutationFn: () =>
      organizationHierarchyApi.moveDepartment(moveDepartmentId!, moveParentId || null),
    onSuccess: async () => {
      setMoveDepartmentId(null)
      setMoveParentId('')
      setMoveError(null)
      await invalidate()
    },
    onError: (error: unknown) => {
      setMoveError(error instanceof ApiError ? error.message : t('admin.error.generic'))
    },
  })

  const addMemberMutation = useMutation({
    mutationFn: () =>
      organizationHierarchyApi.addMembersBatch(
        membersDepartmentId!,
        selectedMemberIds,
        selectedMemberIds.length === 1 ? addMemberAsPrimary : addMemberAsPrimary,
      ),
    onSuccess: async () => {
      setAddMemberOpen(false)
      setMemberSearch('')
      setSelectedMemberIds([])
      setAddMemberAsPrimary(false)
      setMemberActionError(null)
      await invalidate()
    },
    onError: (error: unknown) => {
      setMemberActionError(error instanceof ApiError ? error.message : t('admin.error.generic'))
    },
  })

  const removeMemberMutation = useMutation({
    mutationFn: (userId: string) =>
      organizationHierarchyApi.removeMember(membersDepartmentId!, userId),
    onSuccess: async () => {
      setMemberActionError(null)
      await invalidate()
    },
    onError: (error: unknown) => {
      setMemberActionError(error instanceof ApiError ? error.message : t('admin.error.generic'))
    },
  })

  const setPrimaryDepartmentMutation = useMutation({
    mutationFn: (userId: string) =>
      organizationHierarchyApi.setPrimaryDepartment(membersDepartmentId!, userId),
    onSuccess: async () => {
      await invalidate()
    },
  })

  const assignMemberPositionMutation = useMutation({
    mutationFn: () =>
      organizationHierarchyApi.assignUser(assignMemberPositionId, assignMemberUserId!, {
        addToDepartment: false,
      }),
    onSuccess: async () => {
      setAssignMemberPositionOpen(false)
      setAssignMemberUserId(null)
      setAssignMemberPositionId('')
      setMemberActionError(null)
      await invalidate()
    },
    onError: (error: unknown) => {
      setMemberActionError(error instanceof ApiError ? error.message : t('admin.error.generic'))
    },
  })

  const memberPositionsQuery = useQuery({
    queryKey: hierarchyKeys.positions(membersDepartmentId || undefined),
    queryFn: () => organizationHierarchyApi.listPositions(membersDepartmentId || undefined, true),
    enabled: assignMemberPositionOpen && Boolean(membersDepartmentId),
  })

  const openCreate = () => {
    setForm(emptyPositionForm())
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

  const openCreateDepartment = (parentId?: string) => {
    setEditingDepartment(null)
    setDepartmentForm({
      ...emptyDepartmentForm(),
      parentDepartmentId: parentId ?? '',
    })
    setDepartmentFormError(null)
    setDepartmentFormOpen(true)
  }

  const openEditDepartment = (dept: OrganizationDepartmentSummary) => {
    setEditingDepartment(dept)
    setDepartmentForm({
      nameEn: dept.nameEn || dept.name || '',
      nameAr: dept.nameAr ?? '',
      code: dept.code || '',
      descriptionEn: dept.descriptionEn ?? '',
      descriptionAr: dept.descriptionAr ?? '',
      parentDepartmentId: dept.parentDepartmentId ?? '',
      sortOrder: String(dept.sortOrder ?? 0),
      isActive: dept.isActive,
      unitType: dept.unitType ?? 'Department',
    })
    setDepartmentFormError(null)
    setDepartmentFormOpen(true)
  }

  const openMoveDepartment = (dept: OrganizationDepartmentSummary) => {
    setMoveDepartmentId(dept.id)
    setMoveParentId(dept.parentDepartmentId ?? '')
    setMoveError(null)
  }

  const openManageEmployees = (departmentId: string) => {
    setManageDepartmentsOpen(true)
    setMembersDepartmentId(departmentId)
    setMemberActionError(null)
  }

  const parentOptions = (() => {
    if (!selectedPosition) return flat
    const blocked = collectDescendantIds(selectedPosition)
    return flat.filter((p) => !blocked.has(p.id))
  })()

  const assignedUserIds = new Set((selectedPosition?.occupants ?? []).map((o) => o.userId))

  const assignDepartmentName = useMemo(() => {
    const deptId = selectedPosition?.departmentId ?? resolvedDepartmentId
    const dept = (departmentsQuery.data ?? []).find((d) => d.id === deptId)
    return dept ? departmentLabel(language, dept) : ''
  }, [
    departmentsQuery.data,
    language,
    resolvedDepartmentId,
    selectedPosition?.departmentId,
  ])

  const tryAssignUser = (user: OrganizationActiveUser) => {
    if (!searchAllUsers || assignMemberIds.has(user.id)) {
      assignMutation.mutate({ userId: user.id })
      return
    }
    setPendingAssignUser(user)
  }

  const openCompanyDepartment = (id: string) => {
    setSelectedCompanyUnitId(id)
    setDepartmentId(id)
    setTab('departments')
  }

  const findDepartmentById = (id: string) =>
    (departmentsQuery.data ?? []).find((d) => d.id === id)

  const companyForest = useMemo(
    () => buildOrganizationDepartmentForest(companyViewQuery.data ?? []),
    [companyViewQuery.data],
  )

  const selectedCompanyBreadcrumb = useMemo(() => {
    if (!selectedCompanyUnitId) return []
    return buildUnitBreadcrumb(companyForest, selectedCompanyUnitId)
  }, [companyForest, selectedCompanyUnitId])

  const moveParentChoices = useMemo(() => {
    const departments = departmentsQuery.data ?? []
    if (!moveDepartmentId) return departments
    const exclude = collectDepartmentDescendantIds(departments, moveDepartmentId)
    exclude.add(moveDepartmentId)
    return departments
      .filter((d) => !exclude.has(d.id))
      .sort((a, b) => a.sortOrder - b.sortOrder || (a.nameEn || '').localeCompare(b.nameEn || ''))
  }, [departmentsQuery.data, moveDepartmentId])

  const tabs: { id: HierarchyTab; labelKey: string }[] = [
    { id: 'company', labelKey: 'admin.hierarchy.tabs.companyView' },
    { id: 'departments', labelKey: 'admin.hierarchy.tabs.departments' },
    { id: 'people', labelKey: 'admin.hierarchy.tabs.people' },
    { id: 'positions', labelKey: 'admin.hierarchy.tabs.positions' },
  ]

  const membersDepartment = (departmentsQuery.data ?? []).find(
    (d) => d.id === membersDepartmentId,
  )
  const selectedDepartment = (departmentsQuery.data ?? []).find(
    (d) => d.id === resolvedDepartmentId,
  )
  const departmentParentChoices = useMemo(() => {
    const departments = departmentsQuery.data ?? []
    const exclude = editingDepartment
      ? collectDepartmentDescendantIds(departments, editingDepartment.id)
      : new Set<string>()
    if (editingDepartment) exclude.add(editingDepartment.id)
    return departments
      .filter((d) => !exclude.has(d.id))
      .sort((a, b) => a.sortOrder - b.sortOrder || (a.nameEn || '').localeCompare(b.nameEn || ''))
  }, [departmentsQuery.data, editingDepartment])
  const childDepartments = useMemo(
    () =>
      (departmentsQuery.data ?? []).filter(
        (d) => d.parentDepartmentId === resolvedDepartmentId,
      ),
    [departmentsQuery.data, resolvedDepartmentId],
  )

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('admin.hierarchy.title')}
        description={t('admin.hierarchy.description')}
      />

      <div className="flex flex-wrap gap-2" role="tablist" aria-label={t('admin.hierarchy.title')}>
        {tabs.map((item) => (
          <Button
            key={item.id}
            type="button"
            size="sm"
            variant={tab === item.id ? 'default' : 'outline'}
            onClick={() => setTab(item.id)}
            aria-selected={tab === item.id}
          >
            {t(item.labelKey)}
          </Button>
        ))}
      </div>

      {tab === 'company' ? (
        <div className="space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div>
              <h2 className="text-base font-semibold">{t('admin.hierarchy.organizationalUnits')}</h2>
              <p className="text-sm text-muted-foreground">
                {t('admin.hierarchy.organizationalUnitsHint')}
              </p>
            </div>
            {canManage ? (
              <Button onClick={() => openCreateDepartment(selectedCompanyUnitId ?? undefined)}>
                <Plus className="me-1.5 h-4 w-4" />
                {t('admin.hierarchy.addOrganizationalUnit')}
              </Button>
            ) : null}
          </div>
          {selectedCompanyBreadcrumb.length > 0 ? (
            <nav aria-label={t('admin.hierarchy.breadcrumb')} className="text-sm text-muted-foreground">
              {selectedCompanyBreadcrumb.map((node, index) => (
                <span key={node.id}>
                  {index > 0 ? <span className="mx-1">/</span> : null}
                  <button
                    type="button"
                    className="hover:text-foreground hover:underline"
                    onClick={() => setSelectedCompanyUnitId(node.id)}
                  >
                    {departmentLabel(language, node)}
                  </button>
                </span>
              ))}
            </nav>
          ) : null}
          {unitActionError ? (
            <p className="text-sm text-destructive">{unitActionError}</p>
          ) : null}
          <CompanyViewTab
            language={language}
            isLoading={companyViewQuery.isLoading}
            cards={companyViewQuery.data ?? []}
            selectedId={selectedCompanyUnitId}
            canManage={canManage}
            onSelect={(id) => {
              setSelectedCompanyUnitId(id)
              openCompanyDepartment(id)
            }}
            onEdit={(id) => {
              const dept = findDepartmentById(id)
              if (dept) openEditDepartment(dept)
            }}
            onMove={(id) => {
              const dept = findDepartmentById(id)
              if (dept) openMoveDepartment(dept)
            }}
            onDeactivate={(id) => deactivateDepartmentMutation.mutate(id)}
            onReactivate={(id) => reactivateDepartmentMutation.mutate(id)}
            onManageEmployees={openManageEmployees}
          />
        </div>
      ) : null}

      {tab === 'departments' ? (
        <div className="space-y-4">
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
                      {departmentLabel(language, dept)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="flex flex-wrap gap-2">
              <Button variant="outline" onClick={() => setManageDepartmentsOpen(true)}>
                {t('admin.hierarchy.manageDepartments')}
              </Button>
              {canManage ? (
                <Button onClick={openCreate} disabled={!resolvedDepartmentId}>
                  <Plus className="me-1.5 h-4 w-4" />
                  {t('admin.hierarchy.addPosition')}
                </Button>
              ) : null}
            </div>
          </div>

          {selectedDepartment?.code === 'PM' ? (
            <ProjectManagementScopePanel
              language={language}
              descriptionEn={selectedDepartment.descriptionEn}
              descriptionAr={selectedDepartment.descriptionAr}
            />
          ) : null}

          {selectedDepartment?.code === 'EXEC' && childDepartments.length > 0 ? (
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-base">
                  {t('admin.hierarchy.executiveChildDepartments')}
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-2 text-sm text-muted-foreground">
                <p>{t('admin.hierarchy.executiveChildDepartmentsHint')}</p>
                <ul className="flex flex-wrap gap-2">
                  {childDepartments.map((dept) => (
                    <li key={dept.id}>
                      <Button
                        type="button"
                        size="sm"
                        variant="outline"
                        onClick={() => setDepartmentId(dept.id)}
                      >
                        {departmentLabel(language, dept)}
                      </Button>
                    </li>
                  ))}
                </ul>
              </CardContent>
            </Card>
          ) : null}

          <HierarchyChart
            isLoading={hierarchyQuery.isLoading}
            roots={roots}
            language={language}
            onSelectPosition={setSelectedPositionId}
            onSelectUser={(occupant) => setProfileUserId(occupant.userId)}
          />
        </div>
      ) : null}

      {tab === 'people' ? (
        <PeopleTab
          language={language}
          search={peopleSearch}
          onSearchChange={setPeopleSearch}
          departmentId={peopleDepartmentId}
          onDepartmentChange={setPeopleDepartmentId}
          status={peopleStatus}
          onStatusChange={setPeopleStatus}
          departments={departmentsQuery.data ?? []}
          isLoading={peopleQuery.isLoading}
          canManage={canManage}
          onManageOrganization={() => setManageDepartmentsOpen(true)}
          rows={
            peopleStatus === 'inactive'
              ? (peopleQuery.data ?? []).filter((row) => !row.isActive)
              : (peopleQuery.data ?? [])
          }
          onOpenProfile={setProfileUserId}
        />
      ) : null}

      {tab === 'positions' ? (
        <div className="space-y-4">
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
                      {departmentLabel(language, dept)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="flex flex-wrap gap-2">
              <Button
                size="sm"
                variant={positionsViewMode === 'hierarchy' ? 'default' : 'outline'}
                onClick={() => setPositionsViewMode('hierarchy')}
              >
                {t('admin.hierarchy.viewHierarchy')}
              </Button>
              <Button
                size="sm"
                variant={positionsViewMode === 'list' ? 'default' : 'outline'}
                onClick={() => setPositionsViewMode('list')}
              >
                {t('admin.hierarchy.viewList')}
              </Button>
              {canManage ? (
                <Button onClick={openCreate} disabled={!resolvedDepartmentId}>
                  <Plus className="me-1.5 h-4 w-4" />
                  {t('admin.hierarchy.addPosition')}
                </Button>
              ) : null}
            </div>
          </div>

          {positionsViewMode === 'hierarchy' ? (
            <HierarchyChart
              isLoading={hierarchyQuery.isLoading}
              roots={roots}
              language={language}
              onSelectPosition={setSelectedPositionId}
              onSelectUser={(occupant) => setProfileUserId(occupant.userId)}
            />
          ) : positionsListQuery.isLoading ? (
            <div className="space-y-2">
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
            </div>
          ) : (positionsListQuery.data ?? []).length === 0 ? (
            <p className="rounded-md border border-dashed border-border p-8 text-center text-sm text-muted-foreground">
              {t('admin.hierarchy.empty')}
            </p>
          ) : (
            <PositionsFlatList
              language={language}
              positions={positionsListQuery.data ?? []}
              onSelect={setSelectedPositionId}
            />
          )}
        </div>
      ) : null}

      <Sheet
        open={Boolean(selectedPositionId)}
        onOpenChange={(open) => {
          if (!open) setSelectedPositionId(null)
        }}
      >
        <SheetContent className="inset-y-0 start-auto end-0 w-full max-w-md overflow-y-auto border-s bg-background p-6 text-foreground shadow-xl">
          {!selectedPosition && selectedPositionId ? (
            <div className="space-y-4 pe-6">
              <Skeleton className="h-8 w-48" />
              <Skeleton className="h-24 w-full" />
            </div>
          ) : null}
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
                        setSearchAllUsers(false)
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
                          onClick={() => setProfileUserId(occupant.userId)}
                        >
                          <UserAvatar
                            displayName={occupant.displayName}
                            profileImageUrl={occupant.avatarUrl}
                          />
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
        open={Boolean(profileUserId)}
        onOpenChange={(open) => {
          if (!open) setProfileUserId(null)
        }}
      >
        <SheetContent className="inset-y-0 start-auto end-0 w-full max-w-md overflow-y-auto border-s bg-background p-6 text-foreground shadow-xl">
          {profileQuery.isLoading ? (
            <div className="space-y-4 pe-6">
              <Skeleton className="h-20 w-20 rounded-full" />
              <Skeleton className="h-6 w-48" />
              <Skeleton className="h-4 w-64" />
            </div>
          ) : profileQuery.data ? (
            <ProfileSummarySheet
              language={language}
              profile={profileQuery.data}
              canManage={canManage}
              canOpenUsersAdmin={can('admin.users')}
              onSetPrimary={(positionId, assignmentId) =>
                setPrimaryMutation.mutate({ positionId, assignmentId })
              }
              onRemove={(positionId, assignmentId) =>
                removeMutation.mutate({ positionId, assignmentId })
              }
            />
          ) : null}
        </SheetContent>
      </Sheet>

      <Sheet
        open={manageDepartmentsOpen}
        onOpenChange={(open) => {
          setManageDepartmentsOpen(open)
          if (!open) {
            setMembersDepartmentId(null)
            setAddMemberOpen(false)
          }
        }}
      >
        <SheetContent className="inset-y-0 start-auto end-0 w-full max-w-lg overflow-y-auto border-s bg-background p-6 text-foreground shadow-xl">
          <div className="space-y-5 pe-6">
            <div className="flex items-start justify-between gap-3">
              <div>
                <h3 className="text-lg font-semibold">{t('admin.hierarchy.organizationalUnits')}</h3>
                <p className="mt-1 text-sm text-muted-foreground">
                  {t('admin.hierarchy.organizationalUnitsHint')}
                </p>
              </div>
              {canManage && !membersDepartmentId ? (
                <Button size="sm" onClick={() => openCreateDepartment()}>
                  <Plus className="me-1 h-3.5 w-3.5" />
                  {t('admin.hierarchy.addOrganizationalUnit')}
                </Button>
              ) : null}
            </div>

            {membersDepartmentId && membersDepartment ? (
              <MembersPanel
                departmentName={departmentLabel(language, membersDepartment)}
                breadcrumb={buildUnitBreadcrumb(
                  buildOrganizationDepartmentForest(
                    (departmentsQuery.data ?? []).map((d) => ({
                      id: d.id,
                      nameEn: d.nameEn || d.name || '',
                      nameAr: d.nameAr,
                      code: d.code,
                      parentDepartmentId: d.parentDepartmentId,
                      unitType: d.unitType ?? 'Department',
                      peopleCount: d.memberCount,
                      positionCount: d.positionCount,
                      memberCount: d.memberCount,
                      isActive: d.isActive,
                      sortOrder: d.sortOrder,
                    })),
                  ),
                  membersDepartmentId,
                ).map((n) => localizedName(language, n.nameEn, n.nameAr))}
                members={membersQuery.data ?? []}
                isLoading={membersQuery.isLoading}
                canManage={canManage}
                actionError={memberActionError}
                onBack={() => {
                  setMembersDepartmentId(null)
                  setMemberActionError(null)
                }}
                onAdd={() => {
                  setMemberSearch('')
                  setSelectedMemberIds([])
                  setAddMemberAsPrimary(false)
                  setMemberActionError(null)
                  setAddMemberOpen(true)
                }}
                onRemove={(userId) => removeMemberMutation.mutate(userId)}
                onSetPrimary={(userId) => setPrimaryDepartmentMutation.mutate(userId)}
                onAssignPosition={(userId) => {
                  setAssignMemberUserId(userId)
                  setAssignMemberPositionId('')
                  setAssignMemberPositionOpen(true)
                }}
                onOpenProfile={setProfileUserId}
              />
            ) : departmentsQuery.isLoading ? (
              <div className="space-y-2">
                <Skeleton className="h-14 w-full" />
                <Skeleton className="h-14 w-full" />
              </div>
            ) : (
              <ul className="space-y-2">
                {(departmentsQuery.data ?? []).map((dept) => (
                  <li
                    key={dept.id}
                    className="rounded-md border border-border px-3 py-3"
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0">
                        <p className="font-medium">
                          {departmentLabel(language, dept)}
                        </p>
                        <p className="text-xs text-muted-foreground">{dept.code}</p>
                        <p className="mt-1 text-xs text-muted-foreground">
                          <Badge variant="outline" className="me-1">
                            {t(`admin.hierarchy.unitTypes.${dept.unitType ?? 'Department'}`)}
                          </Badge>
                          {t('admin.hierarchy.peopleCount', { count: dept.memberCount })}
                          {' · '}
                          {t('admin.hierarchy.positionsCount', { count: dept.positionCount })}
                        </p>
                        {!dept.isActive ? (
                          <Badge variant="secondary" className="mt-2">
                            {t('admin.hierarchy.inactive')}
                          </Badge>
                        ) : null}
                      </div>
                      <div className="flex flex-col gap-1">
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => setMembersDepartmentId(dept.id)}
                        >
                          {t('admin.hierarchy.members')}
                        </Button>
                        {canManage ? (
                          <>
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => openEditDepartment(dept)}
                            >
                              {t('admin.hierarchy.editOrganizationalUnit')}
                            </Button>
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => openMoveDepartment(dept)}
                            >
                              {t('admin.hierarchy.moveOrganizationalUnit')}
                            </Button>
                            {dept.isActive ? (
                              <Button
                                size="sm"
                                variant="outline"
                                disabled={deactivateDepartmentMutation.isPending}
                                onClick={() => deactivateDepartmentMutation.mutate(dept.id)}
                              >
                                {t('admin.hierarchy.deactivate')}
                              </Button>
                            ) : (
                              <Button
                                size="sm"
                                variant="outline"
                                disabled={reactivateDepartmentMutation.isPending}
                                onClick={() => reactivateDepartmentMutation.mutate(dept.id)}
                              >
                                {t('admin.hierarchy.reactivate')}
                              </Button>
                            )}
                          </>
                        ) : null}
                      </div>
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </div>
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

      <Dialog
        open={assignOpen}
        onOpenChange={(open) => {
          setAssignOpen(open)
          if (!open) {
            setSearchAllUsers(false)
            setPendingAssignUser(null)
          }
        }}
      >
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
          <label className="flex items-center gap-2 text-sm">
            <Checkbox
              checked={searchAllUsers}
              onCheckedChange={(checked) => setSearchAllUsers(checked === true)}
            />
            {t('admin.hierarchy.searchAllQecUsers')}
          </label>
          {formError ? <p className="text-sm text-destructive">{formError}</p> : null}
          <ul className="max-h-72 space-y-2 overflow-y-auto">
            {(activeUsersQuery.data ?? []).map((user) => {
              const already = assignedUserIds.has(user.id)
              return (
                <li
                  key={user.id}
                  className="flex items-center justify-between gap-3 rounded-md border border-border px-3 py-2"
                >
                  <div className="flex min-w-0 items-center gap-3">
                    <UserAvatar displayName={user.displayName} size="sm" />
                    <div className="min-w-0">
                      <p className="truncate text-sm font-medium">{user.displayName}</p>
                      <p className="truncate text-xs text-muted-foreground">{user.upn}</p>
                    </div>
                  </div>
                  <Button
                    size="sm"
                    disabled={already || assignMutation.isPending}
                    onClick={() => tryAssignUser(user)}
                  >
                    {already ? t('admin.hierarchy.alreadyAssigned') : t('admin.hierarchy.assign')}
                  </Button>
                </li>
              )
            })}
          </ul>
        </DialogContent>
      </Dialog>

      <AlertDialog
        open={Boolean(pendingAssignUser)}
        onOpenChange={(open) => {
          if (!open) setPendingAssignUser(null)
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t('admin.hierarchy.addToDepartmentConfirmTitle')}</AlertDialogTitle>
            <AlertDialogDescription>
              {t('admin.hierarchy.addToDepartmentConfirm', {
                user: pendingAssignUser?.displayName ?? '',
                department: assignDepartmentName,
              })}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t('admin.cancel')}</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                if (!pendingAssignUser) return
                assignMutation.mutate({
                  userId: pendingAssignUser.id,
                  addToDepartment: true,
                })
              }}
            >
              {t('admin.hierarchy.addToDepartmentAndAssign')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <Dialog
        open={departmentFormOpen}
        onOpenChange={(open) => {
          setDepartmentFormOpen(open)
          if (!open) {
            setEditingDepartment(null)
            setDepartmentFormError(null)
          }
        }}
      >
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>
              {editingDepartment
                ? t('admin.hierarchy.editOrganizationalUnit')
                : t('admin.hierarchy.addOrganizationalUnit')}
            </DialogTitle>
          </DialogHeader>
          <div className="grid gap-3">
            <div className="grid gap-3 sm:grid-cols-2">
              <div className="space-y-1.5">
                <Label>{t('admin.hierarchy.fields.nameEn')} *</Label>
                <Input
                  value={departmentForm.nameEn}
                  onChange={(e) =>
                    setDepartmentForm({ ...departmentForm, nameEn: e.target.value })
                  }
                />
              </div>
              <div className="space-y-1.5">
                <Label>{t('admin.hierarchy.fields.nameAr')}</Label>
                <Input
                  value={departmentForm.nameAr}
                  onChange={(e) =>
                    setDepartmentForm({ ...departmentForm, nameAr: e.target.value })
                  }
                  dir="rtl"
                />
              </div>
            </div>
            <div className="grid gap-3 sm:grid-cols-2">
              <div className="space-y-1.5">
                <Label>{t('admin.hierarchy.fields.code')} *</Label>
                <Input
                  value={departmentForm.code}
                  onChange={(e) => setDepartmentForm({ ...departmentForm, code: e.target.value })}
                />
              </div>
              <div className="space-y-1.5">
                <Label>{t('admin.hierarchy.fields.unitType')} *</Label>
                <Select
                  value={departmentForm.unitType}
                  onValueChange={(value) =>
                    setDepartmentForm({
                      ...departmentForm,
                      unitType: value as OrganizationUnitType,
                    })
                  }
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {UNIT_TYPE_OPTIONS.map((unitType) => (
                      <SelectItem key={unitType} value={unitType}>
                        {t(`admin.hierarchy.unitTypes.${unitType}`)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div className="space-y-1.5">
              <Label>{t('admin.hierarchy.fields.descriptionEn')}</Label>
              <Textarea
                value={departmentForm.descriptionEn}
                onChange={(e) =>
                  setDepartmentForm({ ...departmentForm, descriptionEn: e.target.value })
                }
              />
            </div>
            <div className="space-y-1.5">
              <Label>{t('admin.hierarchy.fields.descriptionAr')}</Label>
              <Textarea
                value={departmentForm.descriptionAr}
                onChange={(e) =>
                  setDepartmentForm({ ...departmentForm, descriptionAr: e.target.value })
                }
                dir="rtl"
              />
            </div>
            <div className="space-y-1.5">
              <Label>{t('admin.hierarchy.fields.parentDepartment')}</Label>
              <Select
                value={departmentForm.parentDepartmentId || '__none__'}
                onValueChange={(value) =>
                  setDepartmentForm({
                    ...departmentForm,
                    parentDepartmentId: value === '__none__' ? '' : value,
                  })
                }
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__none__">
                    {t('admin.hierarchy.noParentDepartment')}
                  </SelectItem>
                  {departmentParentChoices.map((dept) => (
                    <SelectItem key={dept.id} value={dept.id}>
                      {departmentLabel(language, dept)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5">
              <Label>{t('admin.hierarchy.fields.sortOrder')}</Label>
              <Input
                type="number"
                value={departmentForm.sortOrder}
                onChange={(e) =>
                  setDepartmentForm({ ...departmentForm, sortOrder: e.target.value })
                }
              />
            </div>
            {editingDepartment ? (
              <label className="flex items-center gap-2 text-sm">
                <Checkbox
                  checked={departmentForm.isActive}
                  onCheckedChange={(checked) =>
                    setDepartmentForm({ ...departmentForm, isActive: checked === true })
                  }
                />
                {t('admin.hierarchy.active')}
              </label>
            ) : null}
          </div>
          {departmentFormError ? (
            <p className="text-sm text-destructive">{departmentFormError}</p>
          ) : null}
          <DialogFooter>
            <Button variant="outline" onClick={() => setDepartmentFormOpen(false)}>
              {t('admin.cancel')}
            </Button>
            <Button
              disabled={
                createDepartmentMutation.isPending || updateDepartmentMutation.isPending
              }
              onClick={() =>
                editingDepartment
                  ? updateDepartmentMutation.mutate()
                  : createDepartmentMutation.mutate()
              }
            >
              {t('admin.save')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={addMemberOpen}
        onOpenChange={(open) => {
          setAddMemberOpen(open)
          if (!open) {
            setSelectedMemberIds([])
            setAddMemberAsPrimary(false)
          }
        }}
      >
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>{t('admin.hierarchy.addEmployees')}</DialogTitle>
            <DialogDescription>{t('admin.hierarchy.addEmployeesHint')}</DialogDescription>
          </DialogHeader>
          <Input
            value={memberSearch}
            onChange={(e) => setMemberSearch(e.target.value)}
            placeholder={t('admin.hierarchy.searchUsers')}
          />
          <label className="flex items-center gap-2 text-sm">
            <Checkbox
              checked={addMemberAsPrimary}
              onCheckedChange={(checked) => setAddMemberAsPrimary(checked === true)}
            />
            {t('admin.hierarchy.setAsPrimaryUnit')}
          </label>
          {memberActionError ? (
            <p className="text-sm text-destructive">{memberActionError}</p>
          ) : null}
          <ul className="max-h-72 space-y-2 overflow-y-auto">
            {(addMemberUsersQuery.data ?? []).map((user) => {
              const already = (membersQuery.data ?? []).some((m) => m.userId === user.id)
              const selected = selectedMemberIds.includes(user.id)
              return (
                <li
                  key={user.id}
                  className="flex items-center justify-between gap-3 rounded-md border border-border px-3 py-2"
                >
                  <label className="flex min-w-0 flex-1 items-center gap-3">
                    <Checkbox
                      checked={selected || already}
                      disabled={already}
                      onCheckedChange={(checked) => {
                        if (already) return
                        setSelectedMemberIds((prev) =>
                          checked === true
                            ? [...prev, user.id]
                            : prev.filter((id) => id !== user.id),
                        )
                      }}
                    />
                    <UserAvatar displayName={user.displayName} size="sm" />
                    <span className="min-w-0">
                      <span className="block truncate text-sm font-medium">{user.displayName}</span>
                      <span className="block truncate text-xs text-muted-foreground">
                        {user.upn}
                      </span>
                    </span>
                  </label>
                  {already ? (
                    <Badge variant="secondary">{t('admin.hierarchy.alreadyMember')}</Badge>
                  ) : null}
                </li>
              )
            })}
          </ul>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAddMemberOpen(false)}>
              {t('admin.cancel')}
            </Button>
            <Button
              disabled={selectedMemberIds.length === 0 || addMemberMutation.isPending}
              onClick={() => addMemberMutation.mutate()}
            >
              {t('admin.hierarchy.addSelectedEmployees', { count: selectedMemberIds.length })}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(moveDepartmentId)}
        onOpenChange={(open) => {
          if (!open) {
            setMoveDepartmentId(null)
            setMoveError(null)
          }
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('admin.hierarchy.moveOrganizationalUnit')}</DialogTitle>
            <DialogDescription>{t('admin.hierarchy.moveOrganizationalUnitHint')}</DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label>{t('admin.hierarchy.fields.parentDepartment')}</Label>
            <Select
              value={moveParentId || '__none__'}
              onValueChange={(value) => setMoveParentId(value === '__none__' ? '' : value)}
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="__none__">{t('admin.hierarchy.noParentDepartment')}</SelectItem>
                {moveParentChoices.map((dept) => (
                  <SelectItem key={dept.id} value={dept.id}>
                    {departmentLabel(language, dept)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          {moveError ? <p className="text-sm text-destructive">{moveError}</p> : null}
          <DialogFooter>
            <Button variant="outline" onClick={() => setMoveDepartmentId(null)}>
              {t('admin.cancel')}
            </Button>
            <Button
              disabled={moveDepartmentMutation.isPending}
              onClick={() => moveDepartmentMutation.mutate()}
            >
              {t('admin.save')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={assignMemberPositionOpen}
        onOpenChange={(open) => {
          setAssignMemberPositionOpen(open)
          if (!open) {
            setAssignMemberUserId(null)
            setAssignMemberPositionId('')
          }
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('admin.hierarchy.assignPosition')}</DialogTitle>
          </DialogHeader>
          <div className="space-y-2">
            <Label>{t('admin.hierarchy.position')}</Label>
            <Select
              value={assignMemberPositionId || '__none__'}
              onValueChange={(value) =>
                setAssignMemberPositionId(value === '__none__' ? '' : value)
              }
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="__none__">{t('admin.hierarchy.none')}</SelectItem>
                {(memberPositionsQuery.data ?? []).map((position) => (
                  <SelectItem key={position.id} value={position.id}>
                    {localizedName(language, position.nameEn, position.nameAr)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          {memberActionError ? (
            <p className="text-sm text-destructive">{memberActionError}</p>
          ) : null}
          <DialogFooter>
            <Button variant="outline" onClick={() => setAssignMemberPositionOpen(false)}>
              {t('admin.cancel')}
            </Button>
            <Button
              disabled={!assignMemberPositionId || assignMemberPositionMutation.isPending}
              onClick={() => assignMemberPositionMutation.mutate()}
            >
              {t('admin.hierarchy.assign')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}

function CompanyViewTab({
  language,
  isLoading,
  cards,
  selectedId,
  canManage,
  onSelect,
  onEdit,
  onMove,
  onDeactivate,
  onReactivate,
  onManageEmployees,
}: {
  language: string
  isLoading: boolean
  cards: OrganizationCompanyDepartmentCard[]
  selectedId?: string | null
  canManage: boolean
  onSelect: (departmentId: string) => void
  onEdit: (departmentId: string) => void
  onMove: (departmentId: string) => void
  onDeactivate: (departmentId: string) => void
  onReactivate: (departmentId: string) => void
  onManageEmployees: (departmentId: string) => void
}) {
  const { t } = useTranslation()
  const roots = useMemo(() => buildOrganizationDepartmentForest(cards), [cards])

  if (isLoading) {
    return (
      <div className="space-y-3">
        <Skeleton className="mx-auto h-8 w-24" />
        <Skeleton className="mx-auto h-28 w-full max-w-sm" />
        <Skeleton className="h-28 w-full max-w-sm" />
      </div>
    )
  }
  if (cards.length === 0) {
    return (
      <p className="rounded-md border border-dashed border-border p-8 text-center text-sm text-muted-foreground">
        {t('admin.hierarchy.noOrganizationalUnits')}
      </p>
    )
  }
  return (
    <OrganizationDepartmentTree
      language={language}
      roots={roots}
      selectedId={selectedId}
      actions={{
        canManage,
        onSelect,
        onEdit,
        onMove,
        onDeactivate,
        onReactivate,
        onManageEmployees,
      }}
    />
  )
}

function ProjectManagementScopePanel({
  language,
  descriptionEn,
  descriptionAr,
}: {
  language: string
  descriptionEn: string | null
  descriptionAr: string | null
}) {
  const { t } = useTranslation()
  const description = language.startsWith('ar')
    ? descriptionAr || descriptionEn
    : descriptionEn || descriptionAr
  const steps = [
    t('admin.hierarchy.pmScope.stepOpportunity'),
    t('admin.hierarchy.pmScope.stepDelivery'),
    t('admin.hierarchy.pmScope.stepStaffing'),
    t('admin.hierarchy.pmScope.stepEndorse'),
  ]

  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-base">{t('admin.hierarchy.pmScope.title')}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-3 text-sm">
        {description ? <p className="text-muted-foreground">{description}</p> : null}
        <ol className="space-y-1.5">
          {steps.map((step, index) => (
            <li key={step} className="flex items-start gap-2">
              <span className="mt-0.5 text-xs font-semibold text-muted-foreground">
                {index + 1}.
              </span>
              <span>
                {step}
                {index < steps.length - 1 ? (
                  <span className="ms-1 text-muted-foreground" aria-hidden>
                    →
                  </span>
                ) : null}
              </span>
            </li>
          ))}
        </ol>
        <p className="rounded-md border border-border bg-muted/40 px-3 py-2 text-xs text-muted-foreground">
          {t('admin.hierarchy.pmScope.hrNote')}
        </p>
      </CardContent>
    </Card>
  )
}

function PeopleTab({
  language,
  search,
  onSearchChange,
  departmentId,
  onDepartmentChange,
  status,
  onStatusChange,
  departments,
  isLoading,
  canManage,
  onManageOrganization,
  rows,
  onOpenProfile,
}: {
  language: string
  search: string
  onSearchChange: (value: string) => void
  departmentId: string
  onDepartmentChange: (value: string) => void
  status: PeopleStatusFilter
  onStatusChange: (value: PeopleStatusFilter) => void
  departments: OrganizationDepartmentSummary[]
  isLoading: boolean
  canManage: boolean
  onManageOrganization: () => void
  rows: Array<{
    userId: string
    displayName: string
    upn: string
    avatarUrl: string | null
    isActive: boolean
    primaryDepartmentName: string | null
    additionalDepartmentCount: number
    primaryPositionName?: string | null
    additionalPositionCount?: number
    positionNames: string[]
  }>
  onOpenProfile: (userId: string) => void
}) {
  const { t } = useTranslation()
  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-sm text-muted-foreground">{t('admin.hierarchy.peopleHint')}</p>
        {canManage ? (
          <Button variant="outline" size="sm" onClick={onManageOrganization}>
            {t('admin.hierarchy.manageOrganization')}
          </Button>
        ) : null}
      </div>
      <div className="grid gap-3 sm:grid-cols-3">
        <div className="space-y-1.5">
          <Label>{t('admin.hierarchy.searchPeople')}</Label>
          <Input
            value={search}
            onChange={(e) => onSearchChange(e.target.value)}
            placeholder={t('admin.hierarchy.searchPeoplePlaceholder')}
          />
        </div>
        <div className="space-y-1.5">
          <Label>{t('admin.hierarchy.organizationalUnit')}</Label>
          <Select value={departmentId} onValueChange={onDepartmentChange}>
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="__all__">{t('admin.hierarchy.allOrganizationalUnits')}</SelectItem>
              {departments.map((dept) => (
                <SelectItem key={dept.id} value={dept.id}>
                  {departmentLabel(language, dept)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-1.5">
          <Label>{t('admin.hierarchy.status')}</Label>
          <Select
            value={status}
            onValueChange={(value) => onStatusChange(value as PeopleStatusFilter)}
          >
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">{t('admin.hierarchy.statusAll')}</SelectItem>
              <SelectItem value="active">{t('admin.hierarchy.active')}</SelectItem>
              <SelectItem value="inactive">{t('admin.hierarchy.inactive')}</SelectItem>
            </SelectContent>
          </Select>
        </div>
      </div>

      {isLoading ? (
        <div className="space-y-2">
          <Skeleton className="h-12 w-full" />
          <Skeleton className="h-12 w-full" />
        </div>
      ) : rows.length === 0 ? (
        <p className="rounded-md border border-dashed border-border p-8 text-center text-sm text-muted-foreground">
          {t('admin.hierarchy.noPeople')}
        </p>
      ) : (
        <div className="overflow-x-auto rounded-md border border-border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('admin.hierarchy.person')}</TableHead>
                <TableHead>{t('admin.hierarchy.primaryUnit')}</TableHead>
                <TableHead>{t('admin.hierarchy.otherUnits')}</TableHead>
                <TableHead>{t('admin.hierarchy.primaryPosition')}</TableHead>
                <TableHead>{t('admin.hierarchy.otherPositions')}</TableHead>
                <TableHead>{t('admin.hierarchy.status')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((row) => (
                <TableRow
                  key={row.userId}
                  className="cursor-pointer"
                  onClick={() => onOpenProfile(row.userId)}
                >
                  <TableCell>
                    <div className="flex items-center gap-3">
                      <UserAvatar
                        displayName={row.displayName}
                        profileImageUrl={row.avatarUrl}
                      />
                      <div className="min-w-0">
                        <p className="truncate font-medium">{row.displayName}</p>
                        <p className="truncate text-xs text-muted-foreground">{row.upn}</p>
                      </div>
                    </div>
                  </TableCell>
                  <TableCell>
                    {row.primaryDepartmentName ?? t('admin.hierarchy.none')}
                  </TableCell>
                  <TableCell>
                    {row.additionalDepartmentCount > 0
                      ? t('admin.hierarchy.additionalCount', {
                          count: row.additionalDepartmentCount,
                        })
                      : t('admin.hierarchy.none')}
                  </TableCell>
                  <TableCell>
                    {row.primaryPositionName
                      ?? (row.positionNames[0] || t('admin.hierarchy.none'))}
                  </TableCell>
                  <TableCell>
                    {(row.additionalPositionCount ?? Math.max(0, row.positionNames.length - 1)) > 0
                      ? t('admin.hierarchy.additionalCount', {
                          count:
                            row.additionalPositionCount
                            ?? Math.max(0, row.positionNames.length - 1),
                        })
                      : t('admin.hierarchy.none')}
                  </TableCell>
                  <TableCell>
                    <Badge variant={row.isActive ? 'secondary' : 'outline'}>
                      {row.isActive
                        ? t('admin.hierarchy.active')
                        : t('admin.hierarchy.inactive')}
                    </Badge>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </div>
  )
}

function PositionsFlatList({
  language,
  positions,
  onSelect,
}: {
  language: string
  positions: OrganizationPosition[]
  onSelect: (id: string) => void
}) {
  const { t } = useTranslation()
  return (
    <div className="overflow-x-auto rounded-md border border-border">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>{t('admin.hierarchy.position')}</TableHead>
            <TableHead>{t('admin.hierarchy.fields.key')}</TableHead>
            <TableHead>{t('admin.hierarchy.status')}</TableHead>
            <TableHead>{t('admin.hierarchy.occupants')}</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {positions.map((position) => (
            <TableRow
              key={position.id}
              className="cursor-pointer"
              onClick={() => onSelect(position.id)}
            >
              <TableCell className="font-medium">
                {localizedName(language, position.nameEn, position.nameAr)}
                {position.isManagerial ? (
                  <Badge variant="secondary" className="ms-2">
                    {t('admin.hierarchy.managerial')}
                  </Badge>
                ) : null}
              </TableCell>
              <TableCell className="text-muted-foreground">{position.key}</TableCell>
              <TableCell>
                <Badge variant={position.isActive ? 'secondary' : 'outline'}>
                  {position.isActive
                    ? t('admin.hierarchy.active')
                    : t('admin.hierarchy.inactive')}
                </Badge>
              </TableCell>
              <TableCell>{position.occupantCount}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  )
}

function HierarchyChart({
  isLoading,
  roots,
  language,
  onSelectPosition,
  onSelectUser,
}: {
  isLoading: boolean
  roots: OrganizationPositionNode[]
  language: string
  onSelectPosition: (id: string) => void
  onSelectUser: (occupant: OrganizationPositionOccupant) => void
}) {
  const { t } = useTranslation()
  if (isLoading) {
    return (
      <div className="space-y-3">
        <Skeleton className="h-28 w-full max-w-sm" />
        <Skeleton className="h-28 w-full max-w-sm" />
      </div>
    )
  }
  if (roots.length === 0) {
    return (
      <p className="rounded-md border border-dashed border-border p-8 text-center text-sm text-muted-foreground">
        {t('admin.hierarchy.empty')}
      </p>
    )
  }
  return (
    <div className="overflow-x-auto pb-4">
      <div className="inline-flex min-w-full flex-col items-stretch gap-0 md:items-center">
        {roots.map((root) => (
          <PositionTree
            key={root.id}
            node={root}
            language={language}
            onSelectPosition={onSelectPosition}
            onSelectUser={onSelectUser}
          />
        ))}
      </div>
    </div>
  )
}

function MembersPanel({
  departmentName,
  breadcrumb,
  members,
  isLoading,
  canManage,
  actionError,
  onBack,
  onAdd,
  onRemove,
  onSetPrimary,
  onAssignPosition,
  onOpenProfile,
}: {
  departmentName: string
  breadcrumb: string[]
  members: OrganizationDepartmentMember[]
  isLoading: boolean
  canManage: boolean
  actionError: string | null
  onBack: () => void
  onAdd: () => void
  onRemove: (userId: string) => void
  onSetPrimary: (userId: string) => void
  onAssignPosition: (userId: string) => void
  onOpenProfile: (userId: string) => void
}) {
  const { t } = useTranslation()
  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-2">
        <div>
          <Button variant="ghost" size="sm" onClick={onBack} className="-ms-2">
            {t('admin.hierarchy.backToOrganizationalUnits')}
          </Button>
          {breadcrumb.length > 0 ? (
            <p className="text-xs text-muted-foreground">{breadcrumb.join(' / ')}</p>
          ) : null}
          <h4 className="font-medium">
            {t('admin.hierarchy.membersOf', { department: departmentName })}
          </h4>
        </div>
        {canManage ? (
          <Button size="sm" onClick={onAdd}>
            <Plus className="me-1 h-3.5 w-3.5" />
            {t('admin.hierarchy.addEmployees')}
          </Button>
        ) : null}
      </div>
      {actionError ? <p className="text-sm text-destructive">{actionError}</p> : null}
      {isLoading ? (
        <Skeleton className="h-20 w-full" />
      ) : members.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t('admin.hierarchy.noMembers')}</p>
      ) : (
        <ul className="space-y-2">
          {members.map((member) => (
            <li
              key={member.userId}
              className="flex items-center justify-between gap-3 rounded-md border border-border px-3 py-2"
            >
              <button
                type="button"
                className="flex min-w-0 flex-1 items-center gap-3 text-start"
                onClick={() => onOpenProfile(member.userId)}
              >
                <UserAvatar
                  displayName={member.displayName}
                  profileImageUrl={member.avatarUrl}
                />
                <span className="min-w-0">
                  <span className="block truncate text-sm font-medium">
                    {member.displayName}
                    {member.isPrimary ? (
                      <Badge className="ms-2" variant="secondary">
                        {t('admin.hierarchy.primaryUnitBadge')}
                      </Badge>
                    ) : null}
                  </span>
                  <span className="block truncate text-xs text-muted-foreground">
                    {member.upn}
                  </span>
                </span>
              </button>
              {canManage ? (
                <div className="flex flex-col gap-1">
                  {!member.isPrimary ? (
                    <Button size="sm" variant="outline" onClick={() => onSetPrimary(member.userId)}>
                      {t('admin.hierarchy.setPrimaryUnit')}
                    </Button>
                  ) : null}
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() => onAssignPosition(member.userId)}
                  >
                    {t('admin.hierarchy.assignPosition')}
                  </Button>
                  <Button size="sm" variant="outline" onClick={() => onRemove(member.userId)}>
                    {t('admin.hierarchy.removeMember')}
                  </Button>
                </div>
              ) : null}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

function ProfileSummarySheet({
  language,
  profile,
  canManage,
  canOpenUsersAdmin,
  onSetPrimary,
  onRemove,
}: {
  language: string
  profile: {
    displayName: string
    upn: string
    avatarUrl: string | null
    isActive: boolean
    primaryDepartmentName: string | null
    departments: Array<{
      departmentId: string
      nameEn: string
      nameAr: string | null
      code: string
      isPrimary: boolean
    }>
    positions: OrganizationUserPosition[]
  }
  canManage: boolean
  canOpenUsersAdmin: boolean
  onSetPrimary: (positionId: string, assignmentId: string) => void
  onRemove: (positionId: string, assignmentId: string) => void
}) {
  const { t } = useTranslation()
  const primaryDept = profile.departments.find((d) => d.isPrimary)
  const additionalDepts = profile.departments.filter((d) => !d.isPrimary)

  return (
    <div className="space-y-5 pe-6">
      <div className="flex flex-col items-start gap-3">
        <UserAvatar
          displayName={profile.displayName}
          profileImageUrl={profile.avatarUrl}
          size="xl"
        />
        <div>
          <h3 className="text-xl font-semibold">{profile.displayName}</h3>
          <p className="text-sm text-muted-foreground">{profile.upn}</p>
          <Badge variant={profile.isActive ? 'secondary' : 'outline'} className="mt-2">
            {profile.isActive ? t('admin.hierarchy.active') : t('admin.hierarchy.inactive')}
          </Badge>
        </div>
      </div>

      <div className="space-y-1 text-sm">
        <p className="font-medium text-muted-foreground">
          {t('admin.hierarchy.primaryDepartment')}
        </p>
        <p>
          {primaryDept
            ? localizedName(language, primaryDept.nameEn, primaryDept.nameAr)
            : profile.primaryDepartmentName ?? t('admin.hierarchy.none')}
        </p>
      </div>

      <div className="space-y-1 text-sm">
        <p className="font-medium text-muted-foreground">
          {t('admin.hierarchy.additionalDepartments')}
        </p>
        {additionalDepts.length === 0 ? (
          <p>{t('admin.hierarchy.none')}</p>
        ) : (
          <ul className="space-y-1">
            {additionalDepts.map((dept) => (
              <li key={dept.departmentId}>
                {departmentLabel(language, dept)}
              </li>
            ))}
          </ul>
        )}
      </div>

      <div className="space-y-1 text-sm">
        <p className="font-medium text-muted-foreground">
          {t('admin.hierarchy.authentication')}
        </p>
        <p>{t('admin.hierarchy.authenticationGoogle')}</p>
      </div>

      {canOpenUsersAdmin ? (
        <Button asChild variant="outline" size="sm">
          <Link to="/it/admin/users">{t('admin.hierarchy.openUsersAdmin')}</Link>
        </Button>
      ) : null}

      <div className="space-y-3">
        <h4 className="font-medium">{t('admin.hierarchy.positions')}</h4>
        {profile.positions.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t('admin.hierarchy.noUserPositions')}</p>
        ) : (
          <UserPositionsList
            language={language}
            positions={profile.positions}
            canManage={canManage}
            onSetPrimary={onSetPrimary}
            onRemove={onRemove}
          />
        )}
      </div>
    </div>
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
              <div
                key={child.id}
                className="relative flex min-w-0 flex-1 flex-col items-stretch md:items-center"
              >
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
              <UserAvatar
                displayName={occupant.displayName}
                profileImageUrl={occupant.avatarUrl}
                size="sm"
              />
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
