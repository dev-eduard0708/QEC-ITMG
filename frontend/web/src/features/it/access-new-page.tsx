import { useEffect, useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
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
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { toast } from '@/components/ui/toast-store'
import { AccessNavTabs } from '@/features/it/access-nav'
import { useAccessUsers } from '@/features/it/access-users'
import { isAppLanguage } from '@/i18n'

const types = ['Joiner', 'Mover', 'Leaver', 'AccessRequest'] as const

type GrantDraft = {
  localId: string
  accessEntitlementId?: string
  nameEn: string
  nameAr: string
  key?: string
  customName?: string
  notes?: string
  selected: boolean
  isCustom: boolean
  isPrivileged?: boolean
  source: 'category' | 'catalog' | 'custom'
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
  item: { nameEn?: string | null; nameAr?: string | null; entitlementKey?: string; key?: string; customName?: string },
  language: string,
) {
  if (item.customName?.trim()) return item.customName.trim()
  const name = language === 'ar' ? item.nameAr || item.nameEn : item.nameEn || item.nameAr
  return name || item.entitlementKey || item.key || '—'
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
      selected:
        opts.defaultSelected === 'joiner' ? item.isDefaultForJoiner : false,
      isCustom: false,
      isPrivileged: item.isPrivileged,
      source: 'category' as const,
    }))
}

export function AccessNewPage() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const language = isAppLanguage(i18n.language) ? i18n.language : 'en'
  const { activeUsers, isDirectoryAvailable } = useAccessUsers()

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
  const [selectedCatalogId, setSelectedCatalogId] = useState('')
  const [customName, setCustomName] = useState('')
  const [customNotes, setCustomNotes] = useState('')
  const [customRevokeAction, setCustomRevokeAction] = useState<'Remove' | 'Disable'>('Disable')
  const [addMode, setAddMode] = useState<'grant' | 'revoke'>('grant')
  const [error, setError] = useState<string | null>(null)

  const categoriesQuery = useQuery({
    queryKey: ['access', 'categories', 'active'],
    queryFn: () => accessApi.listCategories({ activeOnly: true }),
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

  const activeIdsKey = useMemo(
    () =>
      [...activeEntitlementIds].sort().join(','),
    [activeEntitlementIds],
  )

  // Reset grants when category/type changes
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
    // activeEntitlementIds read intentionally when category/type seeds; later arrivals handled below
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [categoryEntitlementsQuery.data, type, categoryId])

  // When Mover current access arrives, drop already-active category/catalog grants
  useEffect(() => {
    if (type !== 'Mover' || !activeIdsKey) return
    setGrants((prev) =>
      prev.filter((item) => !item.accessEntitlementId || !activeEntitlementIds.has(item.accessEntitlementId)),
    )
  }, [type, activeIdsKey, activeEntitlementIds])

  // Hydrate current access for Mover/Leaver
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

  const availableCatalog = useMemo(() => {
    const used = new Set<string>()
    for (const g of grants) {
      if (g.accessEntitlementId) used.add(g.accessEntitlementId)
    }
    for (const c of currentItems) {
      if (type === 'Mover' && c.accessEntitlementId) used.add(c.accessEntitlementId)
    }
    for (const r of extraRevokes) {
      if (r.accessEntitlementId) used.add(r.accessEntitlementId)
    }
    return (catalogQuery.data ?? []).filter((item) => !used.has(item.id))
  }, [catalogQuery.data, grants, currentItems, extraRevokes, type])

  const addCatalogGrant = (entitlement: AccessEntitlement) => {
    setGrants((prev) => [
      ...prev,
      {
        localId: newLocalId(),
        accessEntitlementId: entitlement.id,
        nameEn: entitlement.nameEn,
        nameAr: entitlement.nameAr,
        key: entitlement.key,
        selected: true,
        isCustom: false,
        isPrivileged: entitlement.isPrivileged,
        source: 'catalog',
      },
    ])
    setSelectedCatalogId('')
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
    setExtraRevokes((prev) => [
      ...prev,
      {
        localId: newLocalId(),
        accessEntitlementId: entitlement.id,
        nameEn: entitlement.nameEn,
        nameAr: entitlement.nameAr,
        revokeAction: revokeFrom(entitlement.defaultRevokeAction),
        isCustom: false,
      },
    ])
    setSelectedCatalogId('')
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

  const subjectOk = externalSubject
    ? Boolean(subjectName.trim())
    : Boolean(subjectUserId)
  const moverLeaverNeedsUser = (type === 'Mover' || type === 'Leaver') && !subjectUserId
  const canSubmit =
    Boolean(reason.trim() && categoryId) &&
    subjectOk &&
    !moverLeaverNeedsUser &&
    !createMutation.isPending &&
    (type === 'Leaver' || type === 'Mover' || type === 'Joiner' || type === 'AccessRequest')

  const showGrantSection = type === 'Joiner' || type === 'AccessRequest' || type === 'Mover'
  const showCurrentSection = (type === 'Mover' || type === 'Leaver') && !externalSubject

  return (
    <div className="mx-auto max-w-2xl space-y-6">
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
      <div className="space-y-4">
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
        <div className="space-y-1">
          <Label>{t('access.columns.type')}</Label>
          <Select value={type} onValueChange={setType}>
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {types.map((item) => (
                <SelectItem key={item} value={item}>
                  {t(`access.types.${item}`)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-1">
          <Label htmlFor="reason">{t('access.columns.reason')}</Label>
          <Input id="reason" value={reason} onChange={(e) => setReason(e.target.value)} />
        </div>

        {type !== 'Mover' && type !== 'Leaver' ? (
          <label className="flex items-center gap-2 text-sm">
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
          <>
            <div className="space-y-1">
              <Label htmlFor="subjectName">{t('access.fields.subjectName')}</Label>
              <Input id="subjectName" value={subjectName} onChange={(e) => setSubjectName(e.target.value)} />
            </div>
            <div className="space-y-1">
              <Label htmlFor="subjectEmail">{t('access.fields.subjectEmail')}</Label>
              <Input id="subjectEmail" value={subjectEmail} onChange={(e) => setSubjectEmail(e.target.value)} />
            </div>
          </>
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
            {moverLeaverNeedsUser ? (
              <p className="text-xs text-destructive">{t('access.subjectRequiredForType')}</p>
            ) : null}
          </div>
        )}

        {showCurrentSection ? (
          <section className="space-y-3 rounded-md border p-4">
            <div className="flex items-center justify-between gap-2">
              <h2 className="text-sm font-medium">
                {type === 'Leaver' ? t('access.accessToRevoke') : t('access.currentAccess')}
              </h2>
              {currentAccessQuery.isLoading ? (
                <span className="text-xs text-muted-foreground">{t('access.loading')}</span>
              ) : null}
            </div>
            {currentItems.length === 0 && !currentAccessQuery.isLoading ? (
              <p className="text-sm text-muted-foreground">{t('access.currentAccessEmpty')}</p>
            ) : (
              <ul className="space-y-2">
                {currentItems.map((item) => (
                  <li key={item.localId} className="flex flex-wrap items-center gap-2 text-sm">
                    <span className="min-w-[140px] font-medium">{labelOf(item, language)}</span>
                    {item.isPrivileged ? <Badge variant="outline">{t('access.privileged')}</Badge> : null}
                    {item.isCustom ? <Badge variant="secondary">{t('access.custom')}</Badge> : (
                      <Badge variant="outline">{t('access.catalog')}</Badge>
                    )}
                    {type === 'Mover' ? (
                      <div className="ms-auto flex gap-2">
                        <Button
                          type="button"
                          size="sm"
                          variant={item.keep ? 'default' : 'secondary'}
                          onClick={() =>
                            setCurrentItems((prev) =>
                              prev.map((row) =>
                                row.localId === item.localId ? { ...row, keep: true } : row,
                              ),
                            )
                          }
                        >
                          {t('access.keep')}
                        </Button>
                        <Button
                          type="button"
                          size="sm"
                          variant={!item.keep ? 'default' : 'secondary'}
                          onClick={() =>
                            setCurrentItems((prev) =>
                              prev.map((row) =>
                                row.localId === item.localId ? { ...row, keep: false } : row,
                              ),
                            )
                          }
                        >
                          {item.revokeAction === 'Disable' ? t('access.disable') : t('access.remove')}
                        </Button>
                      </div>
                    ) : (
                      <label className="ms-auto flex items-center gap-2">
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
                          {item.revokeAction === 'Disable' ? t('access.disable') : t('access.remove')}
                        </span>
                      </label>
                    )}
                  </li>
                ))}
              </ul>
            )}
          </section>
        ) : null}

        {showGrantSection && categoryId ? (
          <section className="space-y-3 rounded-md border p-4">
            <h2 className="text-sm font-medium">
              {type === 'Mover'
                ? t('access.newAccessToGrant')
                : type === 'Joiner'
                  ? t('access.standardAccess')
                  : t('access.requestedAccess')}
            </h2>
            {categoryEntitlementsQuery.isLoading ? (
              <p className="text-sm text-muted-foreground">{t('access.loading')}</p>
            ) : grants.length === 0 ? (
              <p className="text-sm text-muted-foreground">{t('access.noCategoryAccess')}</p>
            ) : (
              <ul className="space-y-2">
                {grants.map((item) => (
                  <li key={item.localId} className="flex flex-wrap items-center gap-2 text-sm">
                    <Checkbox
                      checked={item.selected}
                      onCheckedChange={(v) =>
                        setGrants((prev) =>
                          prev.map((row) =>
                            row.localId === item.localId ? { ...row, selected: v === true } : row,
                          ),
                        )
                      }
                    />
                    <span className="font-medium">{labelOf(item, language)}</span>
                    {item.source === 'category' && type === 'Joiner' ? (
                      categoryEntitlementsQuery.data?.find(
                        (x) => x.accessEntitlementId === item.accessEntitlementId,
                      )?.isDefaultForJoiner ? (
                        <Badge variant="success">{t('access.defaultForJoiner')}</Badge>
                      ) : (
                        <Badge variant="secondary">{t('access.optional')}</Badge>
                      )
                    ) : null}
                    {item.isCustom ? <Badge variant="secondary">{t('access.custom')}</Badge> : null}
                    {item.isPrivileged ? <Badge variant="outline">{t('access.privileged')}</Badge> : null}
                    {item.source !== 'category' ? (
                      <Button
                        type="button"
                        size="sm"
                        variant="secondary"
                        className="ms-auto"
                        onClick={() => setGrants((prev) => prev.filter((row) => row.localId !== item.localId))}
                      >
                        {t('access.remove')}
                      </Button>
                    ) : null}
                  </li>
                ))}
              </ul>
            )}
          </section>
        ) : null}

        {(type === 'Leaver' || type === 'Mover') && extraRevokes.length > 0 ? (
          <section className="space-y-2 rounded-md border p-4">
            <h2 className="text-sm font-medium">{t('access.additionalRevokes')}</h2>
            <ul className="space-y-2">
              {extraRevokes.map((item) => (
                <li key={item.localId} className="flex flex-wrap items-center gap-2 text-sm">
                  <span className="font-medium">{labelOf(item, language)}</span>
                  <Badge variant="outline">
                    {item.revokeAction === 'Disable' ? t('access.disable') : t('access.remove')}
                  </Badge>
                  {item.isCustom ? <Badge variant="secondary">{t('access.custom')}</Badge> : null}
                  <Button
                    type="button"
                    size="sm"
                    variant="secondary"
                    className="ms-auto"
                    onClick={() =>
                      setExtraRevokes((prev) => prev.filter((row) => row.localId !== item.localId))
                    }
                  >
                    {t('access.remove')}
                  </Button>
                </li>
              ))}
            </ul>
          </section>
        ) : null}

        {categoryId ? (
          <section className="space-y-3 rounded-md border p-4">
            <div className="flex flex-wrap gap-2">
              {(type === 'Joiner' || type === 'AccessRequest' || type === 'Mover') && (
                <Button
                  type="button"
                  size="sm"
                  variant={addMode === 'grant' ? 'default' : 'secondary'}
                  onClick={() => setAddMode('grant')}
                >
                  {t('access.addAccess')}
                </Button>
              )}
              {(type === 'Leaver' || type === 'Mover') && (
                <Button
                  type="button"
                  size="sm"
                  variant={addMode === 'revoke' ? 'default' : 'secondary'}
                  onClick={() => setAddMode('revoke')}
                >
                  {t('access.addOtherAccess')}
                </Button>
              )}
            </div>

            <div className="flex flex-wrap items-end gap-2">
              <div className="min-w-[160px] flex-1 space-y-1">
                <Label>{t('access.searchCatalog')}</Label>
                <Input
                  value={catalogSearch}
                  onChange={(e) => setCatalogSearch(e.target.value)}
                  placeholder={t('access.searchCatalogPlaceholder')}
                />
              </div>
              <div className="min-w-[200px] flex-1 space-y-1">
                <Label>{t('access.selectCatalogItem')}</Label>
                <Select value={selectedCatalogId || undefined} onValueChange={setSelectedCatalogId}>
                  <SelectTrigger>
                    <SelectValue placeholder={t('access.selectCatalogItem')} />
                  </SelectTrigger>
                  <SelectContent>
                    {availableCatalog.map((item) => (
                      <SelectItem key={item.id} value={item.id}>
                        {labelOf(item, language)} ({item.key})
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <Button
                type="button"
                disabled={!selectedCatalogId}
                onClick={() => {
                  const found = availableCatalog.find((item) => item.id === selectedCatalogId)
                  if (!found) return
                  if (addMode === 'revoke') addCatalogRevoke(found)
                  else addCatalogGrant(found)
                }}
              >
                {t('access.add')}
              </Button>
            </div>

            <div className="flex flex-wrap items-end gap-2 border-t pt-3">
              <div className="min-w-[160px] flex-1 space-y-1">
                <Label>{t('access.customRequest')}</Label>
                <Input
                  value={customName}
                  onChange={(e) => setCustomName(e.target.value)}
                  placeholder={t('access.customNamePlaceholder')}
                />
              </div>
              <div className="min-w-[140px] flex-1 space-y-1">
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
                    <SelectTrigger className="w-[140px]">
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
                variant="secondary"
                disabled={!customName.trim()}
                onClick={() => {
                  if (addMode === 'revoke') addCustomRevoke()
                  else addCustomGrant()
                }}
              >
                {t('access.addCustom')}
              </Button>
            </div>
          </section>
        ) : null}

        <div className="rounded-md border bg-muted/30 p-3 text-sm">
          <p className="font-medium">{t('access.summaryTitle')}</p>
          <p className="mt-1 text-muted-foreground">
            {t('access.summaryCounts', {
              grants: summary.grants,
              removes: summary.removes,
              disables: summary.disables,
              total: summary.total,
            })}
          </p>
        </div>

        {error ? <p className="text-sm text-destructive">{error}</p> : null}
        <div className="flex flex-wrap gap-2">
          <Button
            type="button"
            variant="secondary"
            disabled={!canSubmit}
            onClick={() => createMutation.mutate(false)}
          >
            {t('access.saveAsDraft')}
          </Button>
          <Button
            type="button"
            disabled={!canSubmit}
            onClick={() => createMutation.mutate(true)}
          >
            {t('access.createAndSubmit')}
          </Button>
        </div>
      </div>
    </div>
  )
}
