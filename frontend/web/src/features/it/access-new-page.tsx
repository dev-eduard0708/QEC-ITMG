import { useEffect, useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Plus } from 'lucide-react'
import {
  ApiError,
  accessApi,
  type AccessCaseItemCreate,
  type AccessCategoryEntitlement,
  type AccessEntitlement,
  type UserAccessEntitlement,
} from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { UserPicker } from '@/components/shared/user-picker'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Checkbox } from '@/components/ui/checkbox'
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
import { Separator } from '@/components/ui/separator'
import { Sheet, SheetContent } from '@/components/ui/sheet'
import { Textarea } from '@/components/ui/textarea'
import { toast } from '@/components/ui/toast-store'
import { AccessNavTabs } from '@/features/it/access-nav'
import {
  AccessIdentitySummary,
  AccessItemSelectionCard,
  AccessRequestSummary,
} from '@/features/it/access-ux'
import { useAccessUsers } from '@/features/it/access-users'
import { isAppLanguage } from '@/i18n'
import { cn } from '@/lib/utils'

const types = ['Joiner', 'Mover', 'Leaver', 'AccessRequest'] as const

type GrantDraft = {
  localId: string
  accessEntitlementId?: string
  nameEn: string
  nameAr: string
  descriptionEn?: string | null
  descriptionAr?: string | null
  key?: string
  customName?: string
  notes?: string
  selected: boolean
  isCustom: boolean
  isPrivileged?: boolean
  source: 'category' | 'catalog' | 'custom'
  isDefaultForJoiner?: boolean
}

type CurrentDraft = {
  localId: string
  accessEntitlementId?: string | null
  entitlementKey: string
  nameEn: string
  nameAr: string
  keep: boolean
  revokeAction: 'Remove' | 'Disable'
  isPrivileged: boolean
  isCustom: boolean
  selectedForRevoke: boolean
}

type RevokeExtraDraft = {
  localId: string
  accessEntitlementId?: string
  nameEn: string
  nameAr: string
  customName?: string
  notes?: string
  revokeAction: 'Remove' | 'Disable'
  isCustom: boolean
}

function labelOf(
  item: {
    nameEn?: string | null
    nameAr?: string | null
    entitlementKey?: string
    key?: string
    customName?: string
  },
  language: string,
) {
  if (item.customName?.trim()) return item.customName.trim()
  const name = language === 'ar' ? item.nameAr || item.nameEn : item.nameEn || item.nameAr
  return name || item.entitlementKey || item.key || '—'
}

function descriptionOf(
  item: { descriptionEn?: string | null; descriptionAr?: string | null },
  language: string,
) {
  return language === 'ar'
    ? item.descriptionAr || item.descriptionEn || null
    : item.descriptionEn || item.descriptionAr || null
}

function newLocalId() {
  return crypto.randomUUID()
}

function revokeFrom(value: string | null | undefined): 'Remove' | 'Disable' {
  return value?.toLowerCase() === 'disable' ? 'Disable' : 'Remove'
}

function fromCategory(
  items: AccessCategoryEntitlement[],
  opts: { defaultSelected: 'joiner' | 'none' },
): GrantDraft[] {
  return items
    .filter((item) => item.isActive)
    .map((item) => ({
      localId: item.accessEntitlementId,
      accessEntitlementId: item.accessEntitlementId,
      nameEn: item.nameEn,
      nameAr: item.nameAr,
      key: item.entitlementKey,
      selected: opts.defaultSelected === 'joiner' ? item.isDefaultForJoiner : false,
      isCustom: false,
      isPrivileged: item.isPrivileged,
      source: 'category' as const,
      isDefaultForJoiner: item.isDefaultForJoiner,
    }))
}

function useIsDesktop(breakpoint = 768) {
  const [isDesktop, setIsDesktop] = useState(
    () => typeof window !== 'undefined' && window.matchMedia(`(min-width: ${breakpoint}px)`).matches,
  )
  useEffect(() => {
    const mq = window.matchMedia(`(min-width: ${breakpoint}px)`)
    const update = () => setIsDesktop(mq.matches)
    update()
    mq.addEventListener('change', update)
    return () => mq.removeEventListener('change', update)
  }, [breakpoint])
  return isDesktop
}

export function AccessNewPage() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const language = isAppLanguage(i18n.language) ? i18n.language : 'en'
  const { activeUsers, byId, nameFor, isDirectoryAvailable } = useAccessUsers()
  const isDesktop = useIsDesktop()

  const [categoryId, setCategoryId] = useState('')
  const [type, setType] = useState<string>('Joiner')
  const [reason, setReason] = useState('')
  const [subjectUserId, setSubjectUserId] = useState<string | null>(null)
  const [externalSubject, setExternalSubject] = useState(false)
  const [subjectName, setSubjectName] = useState('')
  const [subjectEmail, setSubjectEmail] = useState('')
  const [grants, setGrants] = useState<GrantDraft[]>([])
  const [currentItems, setCurrentItems] = useState<CurrentDraft[]>([])
  const [extraRevokes, setExtraRevokes] = useState<RevokeExtraDraft[]>([])
  const [catalogSearch, setCatalogSearch] = useState('')
  const [customName, setCustomName] = useState('')
  const [customNotes, setCustomNotes] = useState('')
  const [customRevokeAction, setCustomRevokeAction] = useState<'Remove' | 'Disable'>('Disable')
  const [addMode, setAddMode] = useState<'grant' | 'revoke'>('grant')
  const [addOpen, setAddOpen] = useState(false)
  const [showCustomFields, setShowCustomFields] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const categoriesQuery = useQuery({
    queryKey: ['access', 'categories', 'active'],
    queryFn: () => accessApi.listCategories({ activeOnly: true }),
  })

  const categoryDetailQuery = useQuery({
    queryKey: ['access', 'categories', categoryId],
    queryFn: () => accessApi.getCategory(categoryId),
    enabled: Boolean(categoryId),
  })

  const categoryEntitlementsQuery = useQuery({
    queryKey: ['access', 'categories', categoryId, 'entitlements', 'active'],
    queryFn: () => accessApi.listCategoryEntitlements(categoryId, { activeOnly: true }),
    enabled: Boolean(categoryId),
  })

  const currentAccessQuery = useQuery({
    queryKey: ['access', 'users', subjectUserId, 'current-access'],
    queryFn: () => accessApi.getCurrentAccess(subjectUserId!, { activeOnly: true }),
    enabled: Boolean(subjectUserId) && (type === 'Mover' || type === 'Leaver') && !externalSubject,
  })

  const catalogQuery = useQuery({
    queryKey: ['access', 'entitlements', 'picker', catalogSearch],
    queryFn: () => accessApi.listEntitlements({ activeOnly: true, search: catalogSearch || undefined }),
    enabled: addOpen,
  })

  const categoryOptions = useMemo(
    () =>
      (categoriesQuery.data ?? []).map((category) => ({
        id: category.id,
        label: language === 'ar' ? category.nameAr || category.nameEn : category.nameEn || category.nameAr,
      })),
    [categoriesQuery.data, language],
  )

  const activeEntitlementIds = useMemo(() => {
    const ids = new Set<string>()
    for (const row of currentAccessQuery.data ?? []) {
      if (row.accessEntitlementId) ids.add(row.accessEntitlementId)
    }
    return ids
  }, [currentAccessQuery.data])

  const activeIdsKey = useMemo(() => [...activeEntitlementIds].sort().join(','), [activeEntitlementIds])

  useEffect(() => {
    if (!categoryEntitlementsQuery.data) {
      if (!categoryId) setGrants([])
      return
    }
    if (type === 'Joiner') {
      setGrants(fromCategory(categoryEntitlementsQuery.data, { defaultSelected: 'joiner' }))
    } else if (type === 'AccessRequest') {
      setGrants(fromCategory(categoryEntitlementsQuery.data, { defaultSelected: 'none' }))
    } else if (type === 'Mover') {
      setGrants(
        fromCategory(categoryEntitlementsQuery.data, { defaultSelected: 'none' }).filter(
          (item) => !item.accessEntitlementId || !activeEntitlementIds.has(item.accessEntitlementId),
        ),
      )
    } else {
      setGrants([])
    }
    setExtraRevokes([])
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [categoryEntitlementsQuery.data, type, categoryId])

  useEffect(() => {
    if (type !== 'Mover' || !activeIdsKey) return
    setGrants((prev) =>
      prev.filter((item) => !item.accessEntitlementId || !activeEntitlementIds.has(item.accessEntitlementId)),
    )
  }, [type, activeIdsKey, activeEntitlementIds])

  useEffect(() => {
    const rows = currentAccessQuery.data
    if (!rows || (type !== 'Mover' && type !== 'Leaver')) {
      if (type !== 'Mover' && type !== 'Leaver') setCurrentItems([])
      return
    }
    setCurrentItems(
      rows.map((row: UserAccessEntitlement) => ({
        localId: row.id,
        accessEntitlementId: row.accessEntitlementId,
        entitlementKey: row.entitlementKey,
        nameEn: row.nameEn,
        nameAr: row.nameAr,
        keep: type === 'Mover',
        revokeAction: revokeFrom(row.defaultRevokeAction),
        isPrivileged: row.isPrivileged,
        isCustom: row.isCustom,
        selectedForRevoke: type === 'Leaver',
      })),
    )
  }, [currentAccessQuery.data, type])

  useEffect(() => {
    if (type === 'Mover' || type === 'Leaver') {
      setExternalSubject(false)
      setAddMode(type === 'Leaver' ? 'revoke' : 'grant')
    } else {
      setAddMode('grant')
    }
  }, [type])

  const usedEntitlementIds = useMemo(() => {
    const used = new Set<string>()
    for (const g of grants) {
      if (g.accessEntitlementId) used.add(g.accessEntitlementId)
    }
    for (const c of currentItems) {
      if (c.accessEntitlementId) used.add(c.accessEntitlementId)
    }
    for (const r of extraRevokes) {
      if (r.accessEntitlementId) used.add(r.accessEntitlementId)
    }
    return used
  }, [grants, currentItems, extraRevokes])

  const catalogRows = catalogQuery.data ?? []

  const addCatalogGrant = (entitlement: AccessEntitlement) => {
    setGrants((prev) => {
      if (prev.some((g) => g.accessEntitlementId === entitlement.id)) {
        return prev.map((g) =>
          g.accessEntitlementId === entitlement.id ? { ...g, selected: true } : g,
        )
      }
      return [
        ...prev,
        {
          localId: newLocalId(),
          accessEntitlementId: entitlement.id,
          nameEn: entitlement.nameEn,
          nameAr: entitlement.nameAr,
          descriptionEn: entitlement.descriptionEn,
          descriptionAr: entitlement.descriptionAr,
          key: entitlement.key,
          selected: true,
          isCustom: false,
          isPrivileged: entitlement.isPrivileged,
          source: 'catalog',
        },
      ]
    })
  }

  const addCustomGrant = () => {
    const name = customName.trim()
    if (!name) return
    setGrants((prev) => [
      ...prev,
      {
        localId: newLocalId(),
        customName: name,
        nameEn: name,
        nameAr: name,
        notes: customNotes.trim() || undefined,
        selected: true,
        isCustom: true,
        source: 'custom',
      },
    ])
    setCustomName('')
    setCustomNotes('')
  }

  const addCatalogRevoke = (entitlement: AccessEntitlement) => {
    setExtraRevokes((prev) => {
      if (prev.some((r) => r.accessEntitlementId === entitlement.id)) return prev
      return [
        ...prev,
        {
          localId: newLocalId(),
          accessEntitlementId: entitlement.id,
          nameEn: entitlement.nameEn,
          nameAr: entitlement.nameAr,
          revokeAction: revokeFrom(entitlement.defaultRevokeAction),
          isCustom: false,
        },
      ]
    })
  }

  const addCustomRevoke = () => {
    const name = customName.trim()
    if (!name) return
    setExtraRevokes((prev) => [
      ...prev,
      {
        localId: newLocalId(),
        customName: name,
        nameEn: name,
        nameAr: name,
        notes: customNotes.trim() || undefined,
        revokeAction: customRevokeAction,
        isCustom: true,
      },
    ])
    setCustomName('')
    setCustomNotes('')
  }

  const closeAddPanel = () => {
    setAddOpen(false)
    setCatalogSearch('')
    setShowCustomFields(false)
    setCustomName('')
    setCustomNotes('')
  }

  const buildItems = (): AccessCaseItemCreate[] => {
    const items: AccessCaseItemCreate[] = []

    if (type === 'Joiner' || type === 'AccessRequest') {
      for (const g of grants.filter((x) => x.selected)) {
        items.push({
          accessEntitlementId: g.isCustom ? null : g.accessEntitlementId ?? null,
          customName: g.isCustom ? g.customName ?? g.nameEn : null,
          action: 'Grant',
          notes: g.notes ?? null,
          isSelected: true,
        })
      }
    }

    if (type === 'Mover') {
      for (const c of currentItems.filter((x) => !x.keep)) {
        items.push({
          accessEntitlementId: c.isCustom ? null : c.accessEntitlementId ?? null,
          customName: c.isCustom ? labelOf(c, language) : null,
          action: c.revokeAction,
          isSelected: true,
        })
      }
      for (const g of grants.filter((x) => x.selected)) {
        items.push({
          accessEntitlementId: g.isCustom ? null : g.accessEntitlementId ?? null,
          customName: g.isCustom ? g.customName ?? g.nameEn : null,
          action: 'Grant',
          notes: g.notes ?? null,
          isSelected: true,
        })
      }
      for (const r of extraRevokes) {
        items.push({
          accessEntitlementId: r.isCustom ? null : r.accessEntitlementId ?? null,
          customName: r.isCustom ? r.customName ?? r.nameEn : null,
          action: r.revokeAction,
          notes: r.notes ?? null,
          isSelected: true,
        })
      }
    }

    if (type === 'Leaver') {
      for (const c of currentItems.filter((x) => x.selectedForRevoke)) {
        items.push({
          accessEntitlementId: c.isCustom ? null : c.accessEntitlementId ?? null,
          customName: c.isCustom ? labelOf(c, language) : null,
          action: c.revokeAction,
          isSelected: true,
        })
      }
      for (const r of extraRevokes) {
        items.push({
          accessEntitlementId: r.isCustom ? null : r.accessEntitlementId ?? null,
          customName: r.isCustom ? r.customName ?? r.nameEn : null,
          action: r.revokeAction,
          notes: r.notes ?? null,
          isSelected: true,
        })
      }
    }

    return items
  }

  const standardGrants = grants.filter((g) => g.source === 'category')
  const additionalGrants = grants.filter((g) => g.source !== 'category' && g.selected)
  const selectedStandardCount = standardGrants.filter((g) => g.selected).length

  const summary = useMemo(() => {
    let grantsCount = 0
    let removes = 0
    let disables = 0
    if (type === 'Joiner' || type === 'AccessRequest') {
      grantsCount = grants.filter((x) => x.selected).length
    } else if (type === 'Mover') {
      for (const c of currentItems.filter((x) => !x.keep)) {
        if (c.revokeAction === 'Disable') disables += 1
        else removes += 1
      }
      grantsCount = grants.filter((x) => x.selected).length
      for (const r of extraRevokes) {
        if (r.revokeAction === 'Disable') disables += 1
        else removes += 1
      }
    } else if (type === 'Leaver') {
      for (const c of currentItems.filter((x) => x.selectedForRevoke)) {
        if (c.revokeAction === 'Disable') disables += 1
        else removes += 1
      }
      for (const r of extraRevokes) {
        if (r.revokeAction === 'Disable') disables += 1
        else removes += 1
      }
    }
    return {
      grants: grantsCount,
      removes,
      disables,
      total: grantsCount + removes + disables,
    }
  }, [grants, currentItems, extraRevokes, type])

  const createMutation = useMutation({
    mutationFn: (submitForApproval: boolean) =>
      accessApi.createCase({
        type,
        reason,
        accessCategoryId: categoryId || null,
        subjectUserId: externalSubject ? null : subjectUserId,
        subjectName: externalSubject ? subjectName || null : null,
        subjectEmail: externalSubject ? subjectEmail || null : null,
        items: buildItems(),
        submitForApproval,
      }),
    onSuccess: (created, submitForApproval) => {
      toast.success(
        submitForApproval
          ? t('access.success.submittedForApproval')
          : t('access.success.draftSaved'),
      )
      navigate(`/it/access/${created.id}`)
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t('access.error.generic')),
  })

  const subjectOk = externalSubject ? Boolean(subjectName.trim()) : Boolean(subjectUserId)
  const moverLeaverNeedsUser = (type === 'Mover' || type === 'Leaver') && !subjectUserId
  const canSubmit =
    Boolean(reason.trim() && categoryId) &&
    subjectOk &&
    !moverLeaverNeedsUser &&
    !createMutation.isPending

  const showGrantSection = type === 'Joiner' || type === 'AccessRequest' || type === 'Mover'
  const showCurrentSection = (type === 'Mover' || type === 'Leaver') && !externalSubject

  const selectedUser = subjectUserId ? byId.get(subjectUserId) : undefined
  const employeeSummaryLabel = externalSubject
    ? subjectName.trim() || '—'
    : selectedUser?.displayName || (subjectUserId ? nameFor(subjectUserId) : '—')

  const categoryLabel = categoryOptions.find((c) => c.id === categoryId)?.label ?? '—'

  const routingPreview = useMemo(() => {
    const participants = categoryDetailQuery.data?.participantsByStage
    if (!participants) return null
    const firstName = (stage: string) => {
      const ids = participants[stage] ?? []
      if (ids.length === 0) return '—'
      const names = ids.map((id) => nameFor(id))
      return names.length > 1 ? `${names[0]} (+${names.length - 1})` : names[0] ?? '—'
    }
    return {
      approver: firstName('Approver'),
      fulfiller: firstName('Fulfiller'),
      verifier: firstName('Verifier'),
      closer: firstName('Closer'),
    }
  }, [categoryDetailQuery.data, nameFor])

  const standardCount =
    type === 'Joiner' || type === 'AccessRequest' || type === 'Mover'
      ? selectedStandardCount
      : type === 'Leaver'
        ? currentItems.filter((c) => c.selectedForRevoke).length
        : 0
  const additionalCount =
    type === 'Leaver'
      ? extraRevokes.length
      : type === 'Mover'
        ? extraRevokes.length + additionalGrants.length
        : additionalGrants.length

  const grantSectionTitle =
    type === 'Mover'
      ? t('access.newAccessToGrant')
      : type === 'Joiner'
        ? t('access.standardAccess')
        : t('access.requestedAccess')

  const showAddModeToggle = type === 'Mover'

  const addPanelBody = (
    <div className="space-y-4">
      {showAddModeToggle ? (
        <div className="flex flex-wrap gap-2">
          <Button
            type="button"
            size="sm"
            variant={addMode === 'grant' ? 'default' : 'secondary'}
            onClick={() => setAddMode('grant')}
          >
            {t('access.addAccess')}
          </Button>
          <Button
            type="button"
            size="sm"
            variant={addMode === 'revoke' ? 'default' : 'secondary'}
            onClick={() => setAddMode('revoke')}
          >
            {t('access.addOtherAccess')}
          </Button>
        </div>
      ) : null}

      <div className="space-y-1">
        <Label>{t('access.searchCatalog')}</Label>
        <Input
          value={catalogSearch}
          onChange={(e) => setCatalogSearch(e.target.value)}
          placeholder={t('access.searchCatalogPlaceholder')}
        />
      </div>

      <ul className="max-h-56 space-y-1 overflow-y-auto">
        {catalogQuery.isLoading ? (
          <li className="px-2 py-4 text-center text-sm text-muted-foreground">{t('access.loading')}</li>
        ) : catalogRows.length === 0 ? (
          <li className="px-2 py-4 text-center text-sm text-muted-foreground">{t('access.dual.empty')}</li>
        ) : (
          catalogRows.map((item) => {
            const already = usedEntitlementIds.has(item.id)
            return (
              <li key={item.id}>
                <button
                  type="button"
                  className={cn(
                    'flex w-full items-center justify-between gap-2 rounded-md border px-3 py-2 text-start text-sm hover:bg-muted/50',
                    already && 'bg-muted/30',
                  )}
                  onClick={() => {
                    if (already) return
                    if (addMode === 'revoke') addCatalogRevoke(item)
                    else addCatalogGrant(item)
                    closeAddPanel()
                  }}
                >
                  <span className="min-w-0 truncate font-medium">{labelOf(item, language)}</span>
                  {already ? (
                    <Badge variant="secondary" className="shrink-0 text-[10px]">
                      {t('access.alreadySelected')}
                    </Badge>
                  ) : null}
                </button>
              </li>
            )
          })
        )}
      </ul>

      <Separator />
      <div className="space-y-3">
        <button
          type="button"
          className="text-sm font-medium text-primary hover:underline"
          onClick={() => setShowCustomFields((v) => !v)}
        >
          {t('access.cantFindIt')}
        </button>
        {showCustomFields ? (
          <div className="space-y-3 rounded-md border bg-muted/20 p-3">
            <div className="space-y-1">
              <Label>{t('access.customRequest')}</Label>
              <Input
                value={customName}
                onChange={(e) => setCustomName(e.target.value)}
                placeholder={t('access.customNamePlaceholder')}
              />
            </div>
            <div className="space-y-1">
              <Label>{t('access.notes')}</Label>
              <Input value={customNotes} onChange={(e) => setCustomNotes(e.target.value)} />
            </div>
            {addMode === 'revoke' ? (
              <div className="space-y-1">
                <Label>{t('access.defaultRevokeAction')}</Label>
                <Select
                  value={customRevokeAction}
                  onValueChange={(v) => setCustomRevokeAction(v as 'Remove' | 'Disable')}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Remove">{t('access.remove')}</SelectItem>
                    <SelectItem value="Disable">{t('access.disable')}</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            ) : null}
            <Button
              type="button"
              disabled={!customName.trim()}
              onClick={() => {
                if (addMode === 'revoke') addCustomRevoke()
                else addCustomGrant()
                closeAddPanel()
              }}
            >
              {t('access.addCustom')}
            </Button>
          </div>
        ) : null}
      </div>
    </div>
  )

  const summaryCard = (
    <AccessRequestSummary
      sticky
      category={categoryId ? categoryLabel : null}
      typeLabel={t(`access.types.${type}`)}
      employee={employeeSummaryLabel}
      accessRequestedCount={summary.total}
      standardCount={standardCount}
      additionalCount={additionalCount}
      routing={categoryId ? routingPreview : null}
    />
  )

  const actionBar = (
    <div className="sticky bottom-0 z-10 -mx-1 border-t bg-background/95 px-1 py-3 backdrop-blur supports-[backdrop-filter]:bg-background/80">
      {error ? <p className="mb-2 text-sm text-destructive">{error}</p> : null}
      <p className="mb-2 text-xs text-muted-foreground">
        {t('access.submitHelper', {
          approver: routingPreview?.approver || t('access.workflow.approver'),
        })}
      </p>
      <div className="flex flex-wrap gap-2">
        <Button
          type="button"
          variant="secondary"
          disabled={!canSubmit}
          onClick={() => createMutation.mutate(false)}
        >
          {t('access.saveAsDraft')}
        </Button>
        <Button type="button" disabled={!canSubmit} onClick={() => createMutation.mutate(true)}>
          {t('access.createAndSubmit')}
        </Button>
      </div>
    </div>
  )

  return (
    <div className="mx-auto max-w-6xl space-y-6">
      <PageHeader
        title={t('access.newTitle')}
        description={t('access.newDescription')}
        actions={
          <Button asChild variant="secondary">
            <Link to="/it/access">{t('access.back')}</Link>
          </Button>
        }
      />
      <AccessNavTabs />

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1.85fr)_minmax(260px,1fr)]">
        <div className="space-y-4">
          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-base">{t('access.sections.requestDetails')}</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="space-y-1">
                <Label>{t('access.fields.category')}</Label>
                <Select value={categoryId || undefined} onValueChange={setCategoryId}>
                  <SelectTrigger>
                    <SelectValue placeholder={t('access.fields.categoryPlaceholder')} />
                  </SelectTrigger>
                  <SelectContent>
                    {categoryOptions.map((item) => (
                      <SelectItem key={item.id} value={item.id}>
                        {item.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-2">
                <Label>{t('access.columns.type')}</Label>
                <div className="grid gap-2 sm:grid-cols-2">
                  {types.map((item) => {
                    const selected = type === item
                    return (
                      <button
                        key={item}
                        type="button"
                        onClick={() => setType(item)}
                        className={cn(
                          'rounded-lg border p-3 text-start transition-colors',
                          selected ? 'border-primary/40 bg-primary/5' : 'hover:bg-muted/40',
                        )}
                      >
                        <p className="text-sm font-medium">{t(`access.types.${item}`)}</p>
                        <p className="mt-0.5 text-xs text-muted-foreground">
                          {t(`access.typeHelp.${item}`)}
                        </p>
                      </button>
                    )
                  })}
                </div>
              </div>

              <div className="space-y-1">
                <Label htmlFor="reason">{t('access.columns.reason')}</Label>
                <Textarea
                  id="reason"
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                  rows={3}
                  placeholder={t(`access.reasonHint.${type}`)}
                />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-base">{t('access.sections.employee')}</CardTitle>
              <CardDescription>{t('access.sections.employeeHint')}</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              {type !== 'Mover' && type !== 'Leaver' ? (
                <label className="flex items-center gap-2 text-sm text-muted-foreground">
                  <Checkbox
                    checked={externalSubject}
                    onCheckedChange={(value) => {
                      const next = value === true
                      setExternalSubject(next)
                      if (next) setSubjectUserId(null)
                      else {
                        setSubjectName('')
                        setSubjectEmail('')
                      }
                    }}
                  />
                  {t('access.fields.externalSubject')}
                </label>
              ) : null}

              {externalSubject ? (
                <div className="grid gap-3 sm:grid-cols-2">
                  <div className="space-y-1">
                    <Label htmlFor="subjectName">{t('access.fields.subjectName')}</Label>
                    <Input
                      id="subjectName"
                      value={subjectName}
                      onChange={(e) => setSubjectName(e.target.value)}
                    />
                  </div>
                  <div className="space-y-1">
                    <Label htmlFor="subjectEmail">{t('access.fields.subjectEmail')}</Label>
                    <Input
                      id="subjectEmail"
                      value={subjectEmail}
                      onChange={(e) => setSubjectEmail(e.target.value)}
                    />
                  </div>
                </div>
              ) : subjectUserId && selectedUser ? (
                <AccessIdentitySummary
                  name={selectedUser.displayName}
                  email={selectedUser.upn}
                  onChange={() => setSubjectUserId(null)}
                />
              ) : (
                <div className="space-y-1">
                  <Label>{t('access.fields.subjectUser')}</Label>
                  <UserPicker
                    users={activeUsers}
                    value={subjectUserId}
                    onChange={setSubjectUserId}
                    placeholder={t('access.fields.subjectUserPlaceholder')}
                    allowClear
                  />
                  {!isDirectoryAvailable ? (
                    <p className="text-xs text-muted-foreground">{t('access.directoryUnavailable')}</p>
                  ) : null}
                </div>
              )}
              {moverLeaverNeedsUser ? (
                <p className="text-xs text-destructive">{t('access.subjectRequiredForType')}</p>
              ) : null}
            </CardContent>
          </Card>

          {showCurrentSection ? (
            <Card>
              <CardHeader className="pb-3">
                <CardTitle className="text-base">
                  {type === 'Leaver' ? t('access.accessToRevoke') : t('access.currentAccess')}
                </CardTitle>
                {type === 'Leaver' ? (
                  <CardDescription>{t('access.leaverWarning')}</CardDescription>
                ) : (
                  <CardDescription>
                    {t('access.moverCurrentSummary', {
                      keep: currentItems.filter((c) => c.keep).length,
                      change: currentItems.filter((c) => !c.keep).length,
                    })}
                  </CardDescription>
                )}
              </CardHeader>
              <CardContent className="space-y-2">
                {currentAccessQuery.isLoading ? (
                  <p className="text-sm text-muted-foreground">{t('access.loading')}</p>
                ) : currentItems.length === 0 ? (
                  <p className="text-sm text-muted-foreground">{t('access.currentAccessEmpty')}</p>
                ) : (
                  <ul className="space-y-1.5">
                    {currentItems.map((item) => (
                      <li
                        key={item.localId}
                        className="flex flex-wrap items-center gap-2 rounded-md border px-3 py-2 text-sm"
                      >
                        <span className="min-w-0 flex-1 font-medium">{labelOf(item, language)}</span>
                        {item.isPrivileged ? (
                          <Badge variant="outline" className="text-[10px]">
                            {t('access.privileged')}
                          </Badge>
                        ) : null}
                        {type === 'Mover' ? (
                          <div className="flex gap-1">
                            {(['keep', 'disable', 'remove'] as const).map((opt) => {
                              const isKeep = opt === 'keep'
                              const active = isKeep
                                ? item.keep
                                : !item.keep &&
                                  ((opt === 'disable' && item.revokeAction === 'Disable') ||
                                    (opt === 'remove' && item.revokeAction === 'Remove'))
                              return (
                                <Button
                                  key={opt}
                                  type="button"
                                  size="sm"
                                  variant={active ? 'default' : 'secondary'}
                                  className="h-7 px-2 text-xs"
                                  onClick={() =>
                                    setCurrentItems((prev) =>
                                      prev.map((row) =>
                                        row.localId === item.localId
                                          ? {
                                              ...row,
                                              keep: isKeep,
                                              revokeAction:
                                                opt === 'disable'
                                                  ? 'Disable'
                                                  : opt === 'remove'
                                                    ? 'Remove'
                                                    : row.revokeAction,
                                            }
                                          : row,
                                      ),
                                    )
                                  }
                                >
                                  {opt === 'keep'
                                    ? t('access.keep')
                                    : opt === 'disable'
                                      ? t('access.disable')
                                      : t('access.remove')}
                                </Button>
                              )
                            })}
                          </div>
                        ) : (
                          <label className="ms-auto flex items-center gap-2 text-xs">
                            <Checkbox
                              checked={item.selectedForRevoke}
                              onCheckedChange={(v) =>
                                setCurrentItems((prev) =>
                                  prev.map((row) =>
                                    row.localId === item.localId
                                      ? { ...row, selectedForRevoke: v === true }
                                      : row,
                                  ),
                                )
                              }
                            />
                            <span>
                              {item.revokeAction === 'Disable'
                                ? t('access.disable')
                                : t('access.remove')}
                            </span>
                          </label>
                        )}
                      </li>
                    ))}
                  </ul>
                )}
              </CardContent>
            </Card>
          ) : null}

          {showGrantSection && categoryId ? (
            <Card>
              <CardHeader className="pb-3">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <CardTitle className="text-base">{grantSectionTitle}</CardTitle>
                  {type === 'Joiner' ? (
                    <span className="text-xs text-muted-foreground">
                      {t('access.selectedCount', { count: selectedStandardCount })}
                    </span>
                  ) : null}
                </div>
              </CardHeader>
              <CardContent className="space-y-3">
                {categoryEntitlementsQuery.isLoading ? (
                  <p className="text-sm text-muted-foreground">{t('access.loading')}</p>
                ) : standardGrants.length === 0 ? (
                  <p className="text-sm text-muted-foreground">{t('access.noCategoryAccess')}</p>
                ) : (
                  <div className="grid gap-2 sm:grid-cols-2">
                    {standardGrants.map((item) => (
                      <AccessItemSelectionCard
                        key={item.localId}
                        name={labelOf(item, language)}
                        description={descriptionOf(item, language)}
                        selected={item.selected}
                        privileged={item.isPrivileged}
                        secondaryLabel={
                          type === 'Joiner'
                            ? item.isDefaultForJoiner
                              ? t('access.default')
                              : t('access.optional')
                            : null
                        }
                        onSelectedChange={(selected) =>
                          setGrants((prev) =>
                            prev.map((row) =>
                              row.localId === item.localId ? { ...row, selected } : row,
                            ),
                          )
                        }
                      />
                    ))}
                  </div>
                )}
                {type === 'Joiner' && selectedStandardCount > 0 ? (
                  <Button
                    type="button"
                    size="sm"
                    variant="secondary"
                    onClick={() =>
                      setGrants((prev) =>
                        prev.map((row) =>
                          row.source === 'category' ? { ...row, selected: false } : row,
                        ),
                      )
                    }
                  >
                    {t('access.clearSelection')}
                  </Button>
                ) : null}
              </CardContent>
            </Card>
          ) : null}

          {categoryId ? (
            <Card>
              <CardHeader className="pb-3">
                <CardTitle className="text-base">{t('access.additionalAccess')}</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                {(additionalGrants.length > 0 || extraRevokes.length > 0) && (
                  <ul className="space-y-1.5">
                    {additionalGrants.map((item) => (
                      <li
                        key={item.localId}
                        className="flex flex-wrap items-center gap-2 rounded-md border px-3 py-2 text-sm"
                      >
                        <span className="min-w-0 flex-1 font-medium">{labelOf(item, language)}</span>
                        {item.isCustom ? <Badge variant="secondary">{t('access.custom')}</Badge> : null}
                        <Button
                          type="button"
                          size="sm"
                          variant="secondary"
                          onClick={() =>
                            setGrants((prev) => prev.filter((row) => row.localId !== item.localId))
                          }
                        >
                          {t('access.remove')}
                        </Button>
                      </li>
                    ))}
                    {extraRevokes.map((item) => (
                      <li
                        key={item.localId}
                        className="flex flex-wrap items-center gap-2 rounded-md border px-3 py-2 text-sm"
                      >
                        <span className="min-w-0 flex-1 font-medium">{labelOf(item, language)}</span>
                        <Badge variant="outline">
                          {item.revokeAction === 'Disable' ? t('access.disable') : t('access.remove')}
                        </Badge>
                        <Button
                          type="button"
                          size="sm"
                          variant="secondary"
                          onClick={() =>
                            setExtraRevokes((prev) =>
                              prev.filter((row) => row.localId !== item.localId),
                            )
                          }
                        >
                          {t('access.remove')}
                        </Button>
                      </li>
                    ))}
                  </ul>
                )}
                <Button
                  type="button"
                  variant="outline"
                  className="w-full border-dashed"
                  onClick={() => {
                    setAddMode(type === 'Leaver' ? 'revoke' : 'grant')
                    setAddOpen(true)
                  }}
                >
                  <Plus className="me-1.5 h-4 w-4" />
                  {t('access.addAnotherAccess')}
                </Button>
              </CardContent>
            </Card>
          ) : null}

          <div className="lg:hidden">{summaryCard}</div>
          {actionBar}
        </div>

        <aside className="hidden lg:block">{summaryCard}</aside>
      </div>

      {isDesktop ? (
        <Dialog
          open={addOpen}
          onOpenChange={(open) => {
            if (!open) closeAddPanel()
            else setAddOpen(true)
          }}
        >
          <DialogContent className="max-w-lg">
            <DialogHeader>
              <DialogTitle>{t('access.addAnotherAccess')}</DialogTitle>
            </DialogHeader>
            {addPanelBody}
            <DialogFooter>
              <Button type="button" variant="secondary" onClick={closeAddPanel}>
                {t('access.cancel')}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      ) : (
        <Sheet
          open={addOpen}
          onOpenChange={(open) => {
            if (!open) closeAddPanel()
            else setAddOpen(true)
          }}
        >
          <SheetContent className="inset-y-auto bottom-0 start-0 end-0 top-auto h-[85vh] w-full max-w-none rounded-t-xl border-t bg-card p-6 text-card-foreground">
            <h2 className="mb-4 pe-8 text-lg font-semibold">{t('access.addAnotherAccess')}</h2>
            {addPanelBody}
          </SheetContent>
        </Sheet>
      )}
    </div>
  )
}
