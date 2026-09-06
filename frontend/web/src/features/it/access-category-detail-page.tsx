import { useEffect, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  ApiError,
  accessApi,
  type AccessCategoryEntitlement,
  type AccessEntitlement,
} from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
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
import { AccessNavTabs } from '@/features/it/access-nav'
import { AccessStageDualList } from '@/features/it/access-stage-dual-list'
import { useAccessUsers } from '@/features/it/access-users'
import { isAppLanguage } from '@/i18n'
import { toast } from '@/components/ui/toast-store'

type DraftCategoryEntitlement = {
  accessEntitlementId: string
  entitlementKey: string
  nameEn: string
  nameAr: string
  defaultRevokeAction: string
  isPrivileged: boolean
  isDefaultForJoiner: boolean
  sortOrder: number
  isActive: boolean
}

function stageIds(participantsByStage: Record<string, string[]> | undefined, stage: string): string[] {
  return participantsByStage?.[stage] ?? []
}

function toDraft(item: AccessCategoryEntitlement): DraftCategoryEntitlement {
  return {
    accessEntitlementId: item.accessEntitlementId,
    entitlementKey: item.entitlementKey,
    nameEn: item.nameEn,
    nameAr: item.nameAr,
    defaultRevokeAction: item.defaultRevokeAction,
    isPrivileged: item.isPrivileged,
    isDefaultForJoiner: item.isDefaultForJoiner,
    sortOrder: item.sortOrder,
    isActive: item.isActive,
  }
}

function entitlementLabel(item: { nameEn: string; nameAr: string; entitlementKey?: string; key?: string }, language: string) {
  const name = language === 'ar' ? item.nameAr || item.nameEn : item.nameEn || item.nameAr
  return name || item.entitlementKey || item.key || '—'
}

export function AccessCategoryDetailPage() {
  const { id = '' } = useParams()
  const { t, i18n } = useTranslation()
  const { can } = useAuth()
  const qc = useQueryClient()
  const language = isAppLanguage(i18n.language) ? i18n.language : 'en'
  const canConfigure = can('access.configure')
  const { activeUsers, isDirectoryAvailable, isLoading: usersLoading } = useAccessUsers()

  const [nameEn, setNameEn] = useState('')
  const [nameAr, setNameAr] = useState('')
  const [descriptionEn, setDescriptionEn] = useState('')
  const [descriptionAr, setDescriptionAr] = useState('')
  const [preferSubject, setPreferSubject] = useState(true)
  const [isActive, setIsActive] = useState(true)
  const [requesters, setRequesters] = useState<string[]>([])
  const [approvers, setApprovers] = useState<string[]>([])
  const [fulfillers, setFulfillers] = useState<string[]>([])
  const [verifiers, setVerifiers] = useState<string[]>([])
  const [closers, setClosers] = useState<string[]>([])
  const [draftItems, setDraftItems] = useState<DraftCategoryEntitlement[]>([])
  const [itemsHydrated, setItemsHydrated] = useState(false)
  const [addSearch, setAddSearch] = useState('')
  const [selectedCatalogId, setSelectedCatalogId] = useState('')
  const [addAsDefaultJoiner, setAddAsDefaultJoiner] = useState(false)
  const [createOpen, setCreateOpen] = useState(false)
  const [newKey, setNewKey] = useState('')
  const [newNameEn, setNewNameEn] = useState('')
  const [newNameAr, setNewNameAr] = useState('')
  const [newRevoke, setNewRevoke] = useState('Remove')
  const [newPrivileged, setNewPrivileged] = useState(false)
  const [newDefaultJoiner, setNewDefaultJoiner] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const categoryQuery = useQuery({
    queryKey: ['access', 'categories', id],
    queryFn: () => accessApi.getCategory(id),
    enabled: !!id,
  })

  const entitlementsQuery = useQuery({
    queryKey: ['access', 'categories', id, 'entitlements'],
    queryFn: () => accessApi.listCategoryEntitlements(id),
    enabled: !!id,
  })

  const catalogQuery = useQuery({
    queryKey: ['access', 'entitlements', 'active', addSearch],
    queryFn: () => accessApi.listEntitlements({ activeOnly: true, search: addSearch || undefined }),
    enabled: canConfigure,
  })

  useEffect(() => {
    const category = categoryQuery.data
    if (!category) return
    setNameEn(category.nameEn)
    setNameAr(category.nameAr)
    setDescriptionEn(category.descriptionEn ?? '')
    setDescriptionAr(category.descriptionAr ?? '')
    setPreferSubject(category.preferSubjectEmployeeVerification)
    setIsActive(category.isActive)
    setRequesters(stageIds(category.participantsByStage, 'Requester'))
    setApprovers(stageIds(category.participantsByStage, 'Approver'))
    setFulfillers(stageIds(category.participantsByStage, 'Fulfiller'))
    setVerifiers(stageIds(category.participantsByStage, 'Verifier'))
    setClosers(stageIds(category.participantsByStage, 'Closer'))
  }, [categoryQuery.data])

  useEffect(() => {
    if (!entitlementsQuery.data) return
    setDraftItems(entitlementsQuery.data.map(toDraft))
    setItemsHydrated(true)
  }, [entitlementsQuery.data])

  const invalidate = async () => {
    await qc.invalidateQueries({ queryKey: ['access', 'categories', id] })
    await qc.invalidateQueries({ queryKey: ['access', 'categories'] })
    await qc.invalidateQueries({ queryKey: ['access', 'categories', id, 'entitlements'] })
  }

  const saveDetails = useMutation({
    mutationFn: () =>
      accessApi.updateCategory(id, {
        key: categoryQuery.data?.key,
        nameEn,
        nameAr,
        descriptionEn: descriptionEn || null,
        descriptionAr: descriptionAr || null,
        preferSubjectEmployeeVerification: preferSubject,
        isActive,
      }),
    onSuccess: async () => {
      setError(null)
      await invalidate()
      toast.success(t('access.categories.detailsSaved'))
    },
    onError: (err) => {
      const message = err instanceof ApiError ? err.message : t('access.error.generic')
      setError(message)
      toast.error(message)
    },
  })

  const saveRouting = useMutation({
    mutationFn: () =>
      accessApi.replaceCategoryRouting(id, {
        requesterUserIds: requesters,
        approverUserIds: approvers,
        fulfillerUserIds: fulfillers,
        verifierUserIds: verifiers,
        closerUserIds: closers,
      }),
    onSuccess: async () => {
      setError(null)
      await invalidate()
      toast.success(t('access.categories.routingSaved'))
    },
    onError: (err) => {
      const message = err instanceof ApiError ? err.message : t('access.categories.routingFailed')
      setError(message)
      toast.error(message)
    },
  })

  const saveItems = useMutation({
    mutationFn: () =>
      accessApi.replaceCategoryEntitlements(
        id,
        draftItems.map((item, index) => ({
          accessEntitlementId: item.accessEntitlementId,
          isDefaultForJoiner: item.isDefaultForJoiner,
          sortOrder: index,
          isActive: item.isActive,
        })),
      ),
    onSuccess: async () => {
      setError(null)
      await invalidate()
      toast.success(t('access.categories.itemsSaved'))
    },
    onError: (err) => {
      const message = err instanceof ApiError ? err.message : t('access.error.generic')
      setError(message)
      toast.error(message)
    },
  })

  const createCatalogItem = useMutation({
    mutationFn: async () => {
      const created = await accessApi.createEntitlement({
        key: newKey,
        nameEn: newNameEn,
        nameAr: newNameAr,
        defaultRevokeAction: newRevoke,
        isPrivileged: newPrivileged,
        isActive: true,
      })
      return created
    },
    onSuccess: async (created) => {
      setDraftItems((prev) => {
        if (prev.some((item) => item.accessEntitlementId === created.id)) return prev
        return [
          ...prev,
          {
            accessEntitlementId: created.id,
            entitlementKey: created.key,
            nameEn: created.nameEn,
            nameAr: created.nameAr,
            defaultRevokeAction: created.defaultRevokeAction,
            isPrivileged: created.isPrivileged,
            isDefaultForJoiner: newDefaultJoiner,
            sortOrder: prev.length,
            isActive: true,
          },
        ]
      })
      setCreateOpen(false)
      setNewKey('')
      setNewNameEn('')
      setNewNameAr('')
      setNewRevoke('Remove')
      setNewPrivileged(false)
      setNewDefaultJoiner(false)
      await qc.invalidateQueries({ queryKey: ['access', 'entitlements'] })
      toast.success(t('access.categories.catalogItemCreated'))
    },
    onError: (err) => {
      const message = err instanceof ApiError ? err.message : t('access.error.generic')
      setError(message)
      toast.error(message)
    },
  })

  const availableCatalog = useMemo(() => {
    const selected = new Set(draftItems.map((item) => item.accessEntitlementId))
    return (catalogQuery.data ?? []).filter((item) => !selected.has(item.id))
  }, [catalogQuery.data, draftItems])

  const addFromCatalog = (entitlement: AccessEntitlement) => {
    setDraftItems((prev) => {
      if (prev.some((item) => item.accessEntitlementId === entitlement.id)) return prev
      return [
        ...prev,
        {
          accessEntitlementId: entitlement.id,
          entitlementKey: entitlement.key,
          nameEn: entitlement.nameEn,
          nameAr: entitlement.nameAr,
          defaultRevokeAction: entitlement.defaultRevokeAction,
          isPrivileged: entitlement.isPrivileged,
          isDefaultForJoiner: addAsDefaultJoiner,
          sortOrder: prev.length,
          isActive: true,
        },
      ]
    })
    setSelectedCatalogId('')
    setAddAsDefaultJoiner(false)
  }

  if (categoryQuery.isLoading) return <p className="text-sm text-muted-foreground">{t('access.loading')}</p>
  if (!categoryQuery.data) return <p className="text-sm text-destructive">{t('access.categories.notFound')}</p>

  const category = categoryQuery.data

  return (
    <div className="space-y-6">
      <PageHeader
        title={language === 'ar' ? category.nameAr || category.nameEn : category.nameEn}
        description={category.key}
        actions={
          <Button asChild variant="secondary">
            <Link to="/it/access/configuration">{t('access.categories.back')}</Link>
          </Button>
        }
      />
      <AccessNavTabs />

      <div className="flex flex-wrap gap-2">
        {category.isActive ? (
          <Badge variant="success">{t('access.categories.active')}</Badge>
        ) : (
          <Badge variant="secondary">{t('access.categories.inactive')}</Badge>
        )}
        {category.preferSubjectEmployeeVerification ? (
          <Badge variant="outline">{t('access.categories.preferSubject')}</Badge>
        ) : null}
      </div>

      {error ? <p className="text-sm text-destructive">{error}</p> : null}

      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t('access.categories.details')}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-1">
              <Label htmlFor="detail-name-en">{t('access.categories.nameEn')}</Label>
              <Input
                id="detail-name-en"
                value={nameEn}
                onChange={(e) => setNameEn(e.target.value)}
                disabled={!canConfigure}
              />
            </div>
            <div className="space-y-1">
              <Label htmlFor="detail-name-ar">{t('access.categories.nameAr')}</Label>
              <Input
                id="detail-name-ar"
                value={nameAr}
                onChange={(e) => setNameAr(e.target.value)}
                dir="rtl"
                disabled={!canConfigure}
              />
            </div>
            <div className="space-y-1">
              <Label htmlFor="detail-desc-en">{t('access.categories.descriptionEn')}</Label>
              <Input
                id="detail-desc-en"
                value={descriptionEn}
                onChange={(e) => setDescriptionEn(e.target.value)}
                disabled={!canConfigure}
              />
            </div>
            <div className="space-y-1">
              <Label htmlFor="detail-desc-ar">{t('access.categories.descriptionAr')}</Label>
              <Input
                id="detail-desc-ar"
                value={descriptionAr}
                onChange={(e) => setDescriptionAr(e.target.value)}
                dir="rtl"
                disabled={!canConfigure}
              />
            </div>
          </div>
          <div className="flex flex-wrap gap-4">
            <label className="flex items-center gap-2 text-sm">
              <Checkbox
                checked={preferSubject}
                onCheckedChange={(v) => setPreferSubject(v === true)}
                disabled={!canConfigure}
              />
              {t('access.categories.preferSubject')}
            </label>
            <label className="flex items-center gap-2 text-sm">
              <Checkbox
                checked={isActive}
                onCheckedChange={(v) => setIsActive(v === true)}
                disabled={!canConfigure}
              />
              {t('access.categories.active')}
            </label>
          </div>
          {canConfigure ? (
            <Button
              type="button"
              disabled={!nameEn.trim() || !nameAr.trim() || saveDetails.isPending}
              onClick={() => saveDetails.mutate()}
            >
              {t('access.save')}
            </Button>
          ) : null}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t('access.categories.routing')}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {!isDirectoryAvailable && !usersLoading ? (
            <p className="text-sm text-muted-foreground">{t('access.directoryUnavailable')}</p>
          ) : null}
          <AccessStageDualList
            title={t('access.stages.requesters')}
            users={activeUsers}
            selectedIds={requesters}
            onChange={setRequesters}
            disabled={usersLoading || !canConfigure}
          />
          <AccessStageDualList
            title={t('access.stages.approvers')}
            users={activeUsers}
            selectedIds={approvers}
            onChange={setApprovers}
            disabled={usersLoading || !canConfigure}
          />
          <AccessStageDualList
            title={t('access.stages.fulfillers')}
            users={activeUsers}
            selectedIds={fulfillers}
            onChange={setFulfillers}
            disabled={usersLoading || !canConfigure}
          />
          <AccessStageDualList
            title={t('access.stages.verifiers')}
            users={activeUsers}
            selectedIds={verifiers}
            onChange={setVerifiers}
            disabled={usersLoading || !canConfigure}
          />
          <p className="text-xs text-muted-foreground">{t('access.categories.fallbackHint')}</p>
          <AccessStageDualList
            title={t('access.stages.closers')}
            users={activeUsers}
            selectedIds={closers}
            onChange={setClosers}
            disabled={usersLoading || !canConfigure}
          />
          {canConfigure ? (
            <Button type="button" disabled={saveRouting.isPending} onClick={() => saveRouting.mutate()}>
              {t('access.categories.saveRouting')}
            </Button>
          ) : null}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t('access.categories.accessItems')}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {!itemsHydrated && entitlementsQuery.isLoading ? (
            <p className="text-sm text-muted-foreground">{t('access.loading')}</p>
          ) : draftItems.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t('access.categories.accessItemsEmpty')}</p>
          ) : (
            <ul className="space-y-2">
              {draftItems.map((item) => (
                <li
                  key={item.accessEntitlementId}
                  className="flex flex-wrap items-center gap-2 rounded-md border p-3 text-sm"
                >
                  <span className="min-w-[140px] font-medium">
                    {entitlementLabel(item, language)}
                  </span>
                  <Badge variant="outline">{item.entitlementKey}</Badge>
                  <Badge variant="secondary">{item.defaultRevokeAction}</Badge>
                  {item.isPrivileged ? <Badge variant="outline">{t('access.privileged')}</Badge> : null}
                  {item.isDefaultForJoiner ? (
                    <Badge variant="success">{t('access.defaultForJoiner')}</Badge>
                  ) : null}
                  {canConfigure ? (
                    <>
                      <label className="ms-auto flex items-center gap-2 text-xs">
                        <Checkbox
                          checked={item.isDefaultForJoiner}
                          onCheckedChange={(v) =>
                            setDraftItems((prev) =>
                              prev.map((row) =>
                                row.accessEntitlementId === item.accessEntitlementId
                                  ? { ...row, isDefaultForJoiner: v === true }
                                  : row,
                              ),
                            )
                          }
                        />
                        {t('access.defaultForJoiner')}
                      </label>
                      <label className="flex items-center gap-2 text-xs">
                        <Checkbox
                          checked={item.isActive}
                          onCheckedChange={(v) =>
                            setDraftItems((prev) =>
                              prev.map((row) =>
                                row.accessEntitlementId === item.accessEntitlementId
                                  ? { ...row, isActive: v === true }
                                  : row,
                              ),
                            )
                          }
                        />
                        {t('access.categories.active')}
                      </label>
                      <Button
                        type="button"
                        size="sm"
                        variant="secondary"
                        onClick={() =>
                          setDraftItems((prev) =>
                            prev.filter((row) => row.accessEntitlementId !== item.accessEntitlementId),
                          )
                        }
                      >
                        {t('access.remove')}
                      </Button>
                    </>
                  ) : null}
                </li>
              ))}
            </ul>
          )}

          {canConfigure ? (
            <div className="space-y-3 border-t pt-4">
              <p className="text-sm font-medium">{t('access.addAccess')}</p>
              <div className="flex flex-wrap items-end gap-2">
                <div className="min-w-[180px] flex-1 space-y-1">
                  <Label>{t('access.searchCatalog')}</Label>
                  <Input
                    value={addSearch}
                    onChange={(e) => setAddSearch(e.target.value)}
                    placeholder={t('access.searchCatalogPlaceholder')}
                  />
                </div>
                <div className="min-w-[220px] flex-1 space-y-1">
                  <Label>{t('access.selectCatalogItem')}</Label>
                  <Select value={selectedCatalogId || undefined} onValueChange={setSelectedCatalogId}>
                    <SelectTrigger>
                      <SelectValue placeholder={t('access.selectCatalogItem')} />
                    </SelectTrigger>
                    <SelectContent>
                      {availableCatalog.map((item) => (
                        <SelectItem key={item.id} value={item.id}>
                          {entitlementLabel(item, language)} ({item.key})
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <label className="flex items-center gap-2 pb-2 text-sm">
                  <Checkbox
                    checked={addAsDefaultJoiner}
                    onCheckedChange={(v) => setAddAsDefaultJoiner(v === true)}
                  />
                  {t('access.defaultForJoiner')}
                </label>
                <Button
                  type="button"
                  disabled={!selectedCatalogId}
                  onClick={() => {
                    const found = availableCatalog.find((item) => item.id === selectedCatalogId)
                    if (found) addFromCatalog(found)
                  }}
                >
                  {t('access.add')}
                </Button>
                <Button type="button" variant="secondary" onClick={() => setCreateOpen(true)}>
                  {t('access.categories.createCatalogItem')}
                </Button>
              </div>
              <Button type="button" disabled={saveItems.isPending} onClick={() => saveItems.mutate()}>
                {t('access.categories.saveAccessItems')}
              </Button>
            </div>
          ) : null}
        </CardContent>
      </Card>

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('access.categories.createCatalogItem')}</DialogTitle>
          </DialogHeader>
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="space-y-1 sm:col-span-2">
              <Label htmlFor="new-key">{t('access.categories.key')}</Label>
              <Input id="new-key" value={newKey} onChange={(e) => setNewKey(e.target.value)} />
            </div>
            <div className="space-y-1">
              <Label htmlFor="new-name-en">{t('access.categories.nameEn')}</Label>
              <Input id="new-name-en" value={newNameEn} onChange={(e) => setNewNameEn(e.target.value)} />
            </div>
            <div className="space-y-1">
              <Label htmlFor="new-name-ar">{t('access.categories.nameAr')}</Label>
              <Input
                id="new-name-ar"
                value={newNameAr}
                onChange={(e) => setNewNameAr(e.target.value)}
                dir="rtl"
              />
            </div>
            <div className="space-y-1">
              <Label>{t('access.defaultRevokeAction')}</Label>
              <Select value={newRevoke} onValueChange={setNewRevoke}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Remove">{t('access.remove')}</SelectItem>
                  <SelectItem value="Disable">{t('access.disable')}</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="flex flex-col justify-end gap-2 pb-1">
              <label className="flex items-center gap-2 text-sm">
                <Checkbox checked={newPrivileged} onCheckedChange={(v) => setNewPrivileged(v === true)} />
                {t('access.privileged')}
              </label>
              <label className="flex items-center gap-2 text-sm">
                <Checkbox
                  checked={newDefaultJoiner}
                  onCheckedChange={(v) => setNewDefaultJoiner(v === true)}
                />
                {t('access.defaultForJoiner')}
              </label>
            </div>
          </div>
          <DialogFooter>
            <Button type="button" variant="secondary" onClick={() => setCreateOpen(false)}>
              {t('access.cancel')}
            </Button>
            <Button
              type="button"
              disabled={
                !newKey.trim() ||
                !newNameEn.trim() ||
                !newNameAr.trim() ||
                createCatalogItem.isPending
              }
              onClick={() => createCatalogItem.mutate()}
            >
              {t('access.add')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
