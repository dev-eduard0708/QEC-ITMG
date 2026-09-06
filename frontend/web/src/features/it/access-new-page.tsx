import { useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ApiError, accessApi } from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { UserPicker } from '@/components/shared/user-picker'
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
import { AccessNavTabs } from '@/features/it/access-nav'
import { useAccessUsers } from '@/features/it/access-users'
import { isAppLanguage } from '@/i18n'

const types = ['Joiner', 'Mover', 'Leaver', 'AccessRequest'] as const

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
  const [error, setError] = useState<string | null>(null)

  const categoriesQuery = useQuery({
    queryKey: ['access', 'categories', 'active'],
    queryFn: () => accessApi.listCategories({ activeOnly: true }),
  })

  const categoryOptions = useMemo(
    () =>
      (categoriesQuery.data ?? []).map((category) => ({
        id: category.id,
        label: language === 'ar' ? category.nameAr || category.nameEn : category.nameEn || category.nameAr,
      })),
    [categoriesQuery.data, language],
  )

  const createMutation = useMutation({
    mutationFn: () =>
      accessApi.createCase({
        type,
        reason,
        accessCategoryId: categoryId || null,
        subjectUserId: externalSubject ? null : subjectUserId,
        subjectName: externalSubject ? subjectName || null : null,
        subjectEmail: externalSubject ? subjectEmail || null : null,
      }),
    onSuccess: (created) => navigate(`/it/access/${created.id}`),
    onError: (err) => setError(err instanceof ApiError ? err.message : t('access.error.generic')),
  })

  const canSubmit =
    Boolean(reason.trim() && categoryId) &&
    (externalSubject ? Boolean(subjectName.trim()) : Boolean(subjectUserId)) &&
    !createMutation.isPending

  return (
    <div className="mx-auto max-w-xl space-y-6">
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
                  {item}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-1">
          <Label htmlFor="reason">{t('access.columns.reason')}</Label>
          <Input id="reason" value={reason} onChange={(e) => setReason(e.target.value)} />
        </div>

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
          </div>
        )}

        {error ? <p className="text-sm text-destructive">{error}</p> : null}
        <Button type="button" disabled={!canSubmit} onClick={() => createMutation.mutate()}>
          {t('access.create')}
        </Button>
      </div>
    </div>
  )
}
