import { useEffect, useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Plus } from 'lucide-react'
import {
  ApiError,
  accessApi,
  type AccessCaseItemCreate,
  type AccessCategoryEntitlement,
  type AccessEntitlement,
} from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Textarea } from '@/components/ui/textarea'
import { toast } from '@/components/ui/toast-store'
import { AccessNavTabs } from '@/features/it/access-nav'
import { AccessItemSelectionCard } from '@/features/it/access-ux'
import { useAccessUsers } from '@/features/it/access-users'
import { isAppLanguage } from '@/i18n'

type GrantDraft = {
  localId: string
  accessEntitlementId?: string | null
  nameEn: string
  nameAr: string
  customName?: string
  notes?: string | null
  selected: boolean
  isCustom: boolean
  isPrivileged?: boolean
  action: string
}

function newLocalId() {
  return crypto.randomUUID()
}

export function AccessEditPage() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const { t, i18n } = useTranslation()
  const language = isAppLanguage(i18n.language) ? i18n.language : 'en'
  const { nameFor } = useAccessUsers()
  const [reason, setReason] = useState('')
  const [grants, setGrants] = useState<GrantDraft[]>([])
  const [customName, setCustomName] = useState('')
  const [customAction, setCustomAction] = useState('Grant')
  const [error, setError] = useState<string | null>(null)
  const [hydrated, setHydrated] = useState(false)

  const caseQuery = useQuery({
    queryKey: ['access', 'case', id],
    queryFn: () => accessApi.getCase(id),
    enabled: !!id,
  })
  const itemsQuery = useQuery({
    queryKey: ['access', 'case', id, 'items'],
    queryFn: () => accessApi.listItems(id),
    enabled: !!id,
  })
  const categoryId = caseQuery.data?.accessCategoryId ?? null
  const categoryEntitlementsQuery = useQuery({
    queryKey: ['access', 'categories', categoryId, 'entitlements'],
    queryFn: () => accessApi.listCategoryEntitlements(categoryId!),
    enabled: Boolean(categoryId),
  })
  const catalogQuery = useQuery({
    queryKey: ['access', 'entitlements', 'active'],
    queryFn: () => accessApi.listEntitlements({ activeOnly: true }),
  })

  const accessCase = caseQuery.data
  const canEdit = Boolean(accessCase?.actions?.canEditRequest)
  const statusOk = accessCase?.status === 'Draft' || accessCase?.status === 'Rework'

  useEffect(() => {
    if (!accessCase || !itemsQuery.data || hydrated) return
    setReason(accessCase.reason)
    const mapped: GrantDraft[] = itemsQuery.data.map((item) => ({
      localId: item.id,
      accessEntitlementId: item.accessEntitlementId,
      nameEn: item.nameEn || item.entitlementKey,
      nameAr: item.nameAr || item.nameEn || item.entitlementKey,
      customName: item.isCustom ? item.nameEn || item.entitlementKey : undefined,
      notes: item.notes,
      selected: true,
      isCustom: Boolean(item.isCustom),
      isPrivileged: item.isPrivileged,
      action: item.action,
    }))
    setGrants(mapped)
    setHydrated(true)
  }, [accessCase, itemsQuery.data, hydrated])

  const categoryRows = categoryEntitlementsQuery.data ?? []
  const catalog = useMemo(() => catalogQuery.data ?? [], [catalogQuery.data])

  const availableCatalog = useMemo(() => {
    const selectedIds = new Set(
      grants.filter((g) => g.selected && g.accessEntitlementId).map((g) => g.accessEntitlementId!),
    )
    return catalog.filter((e) => !selectedIds.has(e.id))
  }, [catalog, grants])

  const toggleGrant = (localId: string, selected: boolean) => {
    setGrants((prev) => prev.map((g) => (g.localId === localId ? { ...g, selected } : g)))
  }

  const addFromCategory = (row: AccessCategoryEntitlement) => {
    setGrants((prev) => {
      if (prev.some((g) => g.accessEntitlementId === row.accessEntitlementId && g.selected)) return prev
      const existing = prev.find((g) => g.accessEntitlementId === row.accessEntitlementId)
      if (existing) {
        return prev.map((g) =>
          g.accessEntitlementId === row.accessEntitlementId ? { ...g, selected: true } : g,
        )
      }
      return [
        ...prev,
        {
          localId: newLocalId(),
          accessEntitlementId: row.accessEntitlementId,
          nameEn: row.nameEn,
          nameAr: row.nameAr,
          selected: true,
          isCustom: false,
          isPrivileged: row.isPrivileged,
          action: accessCase?.type === 'Leaver' ? row.defaultRevokeAction || 'Remove' : 'Grant',
        },
      ]
    })
  }

  const addFromCatalog = (entitlement: AccessEntitlement) => {
    setGrants((prev) => [
      ...prev,
      {
        localId: newLocalId(),
        accessEntitlementId: entitlement.id,
        nameEn: entitlement.nameEn,
        nameAr: entitlement.nameAr,
        selected: true,
        isCustom: false,
        isPrivileged: entitlement.isPrivileged,
        action: accessCase?.type === 'Leaver' ? entitlement.defaultRevokeAction || 'Remove' : 'Grant',
      },
    ])
  }

  const addCustom = () => {
    const name = customName.trim()
    if (!name) return
    setGrants((prev) => [
      ...prev,
      {
        localId: newLocalId(),
        customName: name,
        nameEn: name,
        nameAr: name,
        selected: true,
        isCustom: true,
        action: customAction,
      },
    ])
    setCustomName('')
  }

  const buildItems = (): AccessCaseItemCreate[] =>
    grants
      .filter((g) => g.selected)
      .map((g) => ({
        accessEntitlementId: g.isCustom ? null : g.accessEntitlementId ?? null,
        customName: g.isCustom ? g.customName ?? g.nameEn : null,
        action: g.action,
        notes: g.notes ?? null,
        isSelected: true,
      }))

  const saveMutation = useMutation({
    mutationFn: async (andResubmit: boolean) => {
      await accessApi.updateScope(id, { reason, items: buildItems() })
      if (andResubmit) await accessApi.resubmit(id)
    },
    onSuccess: (_data, andResubmit) => {
      toast.success(
        andResubmit ? t('access.success.resubmitted') : t('access.success.scopeSaved'),
      )
      navigate(`/it/access/${id}`)
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t('access.error.generic')),
  })

  if (caseQuery.isLoading || itemsQuery.isLoading) {
    return <p className="text-sm text-muted-foreground">{t('access.loading')}</p>
  }
  if (!accessCase) {
    return <p className="text-sm text-destructive">{t('access.notFound')}</p>
  }
  if (!statusOk || !canEdit) {
    return (
      <div className="space-y-3">
        <p className="text-sm text-destructive">{t('access.edit.notEditable')}</p>
        <Button asChild variant="secondary">
          <Link to={`/it/access/${id}`}>{t('access.back')}</Link>
        </Button>
      </div>
    )
  }

  const categoryLabel =
    accessCase.accessCategoryDisplayName ?? accessCase.accessCategoryNameSnapshot ?? '—'
  const employeeLabel =
    accessCase.subjectName?.trim() ||
    (accessCase.subjectUserId ? nameFor(accessCase.subjectUserId) : null) ||
    accessCase.subjectEmail ||
    '—'

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <PageHeader
        title={t('access.edit.title', { number: accessCase.caseNumber })}
        description={t('access.edit.subtitle')}
        actions={
          <Button asChild variant="secondary">
            <Link to={`/it/access/${id}`}>{t('access.back')}</Link>
          </Button>
        }
      />
      <AccessNavTabs />

      {error ? <p className="text-sm text-destructive">{error}</p> : null}

      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t('access.edit.readOnlyTitle')}</CardTitle>
          <CardDescription>{t('access.edit.readOnlyHint')}</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-2 text-sm sm:grid-cols-2">
          <p>
            <span className="text-muted-foreground">{t('access.fields.category')}: </span>
            {categoryLabel}
          </p>
          <p>
            <span className="text-muted-foreground">{t('access.fields.type')}: </span>
            {t(`access.types.${accessCase.type}`, { defaultValue: accessCase.type })}
          </p>
          <p className="sm:col-span-2">
            <span className="text-muted-foreground">{t('access.fields.subject')}: </span>
            {employeeLabel}
          </p>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t('access.fields.reason')}</CardTitle>
        </CardHeader>
        <CardContent>
          <Textarea value={reason} onChange={(e) => setReason(e.target.value)} rows={3} />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t('access.requestedAccess')}</CardTitle>
          <CardDescription>{t('access.edit.itemsHint')}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          {grants.filter((g) => g.selected).length === 0 ? (
            <p className="text-sm text-muted-foreground">{t('access.itemsEmpty')}</p>
          ) : (
            grants
              .filter((g) => g.selected)
              .map((g) => (
                <AccessItemSelectionCard
                  key={g.localId}
                  name={language === 'ar' ? g.nameAr || g.nameEn : g.nameEn || g.nameAr}
                  selected={g.selected}
                  onSelectedChange={(selected) => toggleGrant(g.localId, selected)}
                  privileged={g.isPrivileged}
                  secondaryLabel={g.action}
                />
              ))
          )}

          {categoryRows.length > 0 ? (
            <div className="space-y-2 border-t pt-3">
              <h3 className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                {t('access.edit.fromCategory')}
              </h3>
              <div className="flex flex-wrap gap-2">
                {categoryRows.map((row) => (
                  <Button
                    key={row.id}
                    type="button"
                    size="sm"
                    variant="secondary"
                    onClick={() => addFromCategory(row)}
                  >
                    <Plus className="me-1 h-3.5 w-3.5" />
                    {language === 'ar' ? row.nameAr || row.nameEn : row.nameEn || row.nameAr}
                  </Button>
                ))}
              </div>
            </div>
          ) : null}

          {availableCatalog.length > 0 ? (
            <div className="space-y-2 border-t pt-3">
              <h3 className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                {t('access.edit.fromCatalog')}
              </h3>
              <Select
                onValueChange={(value) => {
                  const entitlement = catalog.find((e) => e.id === value)
                  if (entitlement) addFromCatalog(entitlement)
                }}
              >
                <SelectTrigger>
                  <SelectValue placeholder={t('access.edit.addCatalog')} />
                </SelectTrigger>
                <SelectContent>
                  {availableCatalog.slice(0, 40).map((e) => (
                    <SelectItem key={e.id} value={e.id}>
                      {language === 'ar' ? e.nameAr || e.nameEn : e.nameEn || e.nameAr}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          ) : null}

          <div className="space-y-2 border-t pt-3">
            <h3 className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
              {t('access.advanced.manualItem')}
            </h3>
            <div className="flex flex-wrap items-end gap-2">
              <div className="space-y-1">
                <Label>{t('access.fields.entitlement')}</Label>
                <Input value={customName} onChange={(e) => setCustomName(e.target.value)} />
              </div>
              <div className="space-y-1">
                <Label>{t('access.fields.action')}</Label>
                <Select value={customAction} onValueChange={setCustomAction}>
                  <SelectTrigger className="w-[140px]">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {['Grant', 'Remove', 'Disable', 'Reassign'].map((item) => (
                      <SelectItem key={item} value={item}>
                        {item}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <Button type="button" disabled={!customName.trim()} onClick={addCustom}>
                {t('access.actions.addItem')}
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>

      <div className="flex flex-wrap gap-2">
        <Button
          type="button"
          variant="secondary"
          disabled={!reason.trim() || saveMutation.isPending}
          onClick={() => saveMutation.mutate(false)}
        >
          {t('access.actions.saveChanges')}
        </Button>
        {accessCase.status === 'Rework' && accessCase.actions?.canResubmit ? (
          <Button
            type="button"
            disabled={!reason.trim() || saveMutation.isPending || buildItems().length === 0}
            onClick={() => saveMutation.mutate(true)}
          >
            {t('access.actions.saveAndResubmit')}
          </Button>
        ) : null}
      </div>
    </div>
  )
}
