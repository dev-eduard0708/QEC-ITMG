import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ApiError, accessApi } from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { AccessNavTabs } from '@/features/it/access-nav'
import { AccessStageDualList } from '@/features/it/access-stage-dual-list'
import { useAccessUsers } from '@/features/it/access-users'

function stageIds(participantsByStage: Record<string, string[]> | undefined, stage: string): string[] {
  return participantsByStage?.[stage] ?? []
}

export function AccessCategoryDetailPage() {
  const { id = '' } = useParams()
  const { t } = useTranslation()
  const qc = useQueryClient()
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
  const [error, setError] = useState<string | null>(null)

  const categoryQuery = useQuery({
    queryKey: ['access', 'categories', id],
    queryFn: () => accessApi.getCategory(id),
    enabled: !!id,
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

  const invalidate = async () => {
    await qc.invalidateQueries({ queryKey: ['access', 'categories', id] })
    await qc.invalidateQueries({ queryKey: ['access', 'categories'] })
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
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t('access.error.generic')),
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
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t('access.error.generic')),
  })

  if (categoryQuery.isLoading) return <p className="text-sm text-muted-foreground">{t('access.loading')}</p>
  if (!categoryQuery.data) return <p className="text-sm text-destructive">{t('access.categories.notFound')}</p>

  const category = categoryQuery.data

  return (
    <div className="space-y-6">
      <PageHeader
        title={category.nameEn}
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
              <Input id="detail-name-en" value={nameEn} onChange={(e) => setNameEn(e.target.value)} />
            </div>
            <div className="space-y-1">
              <Label htmlFor="detail-name-ar">{t('access.categories.nameAr')}</Label>
              <Input id="detail-name-ar" value={nameAr} onChange={(e) => setNameAr(e.target.value)} dir="rtl" />
            </div>
            <div className="space-y-1">
              <Label htmlFor="detail-desc-en">{t('access.categories.descriptionEn')}</Label>
              <Input id="detail-desc-en" value={descriptionEn} onChange={(e) => setDescriptionEn(e.target.value)} />
            </div>
            <div className="space-y-1">
              <Label htmlFor="detail-desc-ar">{t('access.categories.descriptionAr')}</Label>
              <Input
                id="detail-desc-ar"
                value={descriptionAr}
                onChange={(e) => setDescriptionAr(e.target.value)}
                dir="rtl"
              />
            </div>
          </div>
          <div className="flex flex-wrap gap-4">
            <label className="flex items-center gap-2 text-sm">
              <Checkbox checked={preferSubject} onCheckedChange={(v) => setPreferSubject(v === true)} />
              {t('access.categories.preferSubject')}
            </label>
            <label className="flex items-center gap-2 text-sm">
              <Checkbox checked={isActive} onCheckedChange={(v) => setIsActive(v === true)} />
              {t('access.categories.active')}
            </label>
          </div>
          <Button
            type="button"
            disabled={!nameEn.trim() || !nameAr.trim() || saveDetails.isPending}
            onClick={() => saveDetails.mutate()}
          >
            {t('access.save')}
          </Button>
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
            disabled={usersLoading}
          />
          <AccessStageDualList
            title={t('access.stages.approvers')}
            users={activeUsers}
            selectedIds={approvers}
            onChange={setApprovers}
            disabled={usersLoading}
          />
          <AccessStageDualList
            title={t('access.stages.fulfillers')}
            users={activeUsers}
            selectedIds={fulfillers}
            onChange={setFulfillers}
            disabled={usersLoading}
          />
          <AccessStageDualList
            title={t('access.stages.verifiers')}
            users={activeUsers}
            selectedIds={verifiers}
            onChange={setVerifiers}
            disabled={usersLoading}
          />
          <p className="text-xs text-muted-foreground">{t('access.categories.fallbackHint')}</p>
          <AccessStageDualList
            title={t('access.stages.closers')}
            users={activeUsers}
            selectedIds={closers}
            onChange={setClosers}
            disabled={usersLoading}
          />
          <Button type="button" disabled={saveRouting.isPending} onClick={() => saveRouting.mutate()}>
            {t('access.categories.saveRouting')}
          </Button>
        </CardContent>
      </Card>
    </div>
  )
}
