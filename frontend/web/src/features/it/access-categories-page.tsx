import { useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'
import { ApiError, accessApi, type AccessCategory } from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { DataTable } from '@/components/shared/data-table'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { AccessNavTabs } from '@/features/it/access-nav'
import { isAppLanguage } from '@/i18n'

function categoryLabel(category: AccessCategory, language: string): string {
  return language === 'ar' ? category.nameAr || category.nameEn : category.nameEn || category.nameAr
}

export function AccessCategoriesPage() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const language = isAppLanguage(i18n.language) ? i18n.language : 'en'

  const [key, setKey] = useState('')
  const [nameEn, setNameEn] = useState('')
  const [nameAr, setNameAr] = useState('')
  const [descriptionEn, setDescriptionEn] = useState('')
  const [descriptionAr, setDescriptionAr] = useState('')
  const [preferSubject, setPreferSubject] = useState(true)
  const [isActive, setIsActive] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const listQuery = useQuery({
    queryKey: ['access', 'categories'],
    queryFn: () => accessApi.listCategories(),
  })

  const createMutation = useMutation({
    mutationFn: () =>
      accessApi.createCategory({
        key,
        nameEn,
        nameAr,
        descriptionEn: descriptionEn || null,
        descriptionAr: descriptionAr || null,
        preferSubjectEmployeeVerification: preferSubject,
        isActive,
      }),
    onSuccess: async (created) => {
      setError(null)
      await qc.invalidateQueries({ queryKey: ['access', 'categories'] })
      navigate(`/it/access/configuration/${created.id}`)
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t('access.error.generic')),
  })

  const columns = useMemo<ColumnDef<AccessCategory, unknown>[]>(
    () => [
      {
        id: 'name',
        header: t('access.columns.name'),
        cell: ({ row }) => categoryLabel(row.original, language),
      },
      { accessorKey: 'key', header: t('access.categories.key') },
      {
        id: 'active',
        header: t('access.columns.active'),
        cell: ({ row }) =>
          row.original.isActive ? (
            <Badge variant="success">{t('access.categories.active')}</Badge>
          ) : (
            <Badge variant="secondary">{t('access.categories.inactive')}</Badge>
          ),
      },
      {
        id: 'updated',
        header: t('access.columns.updated'),
        cell: ({ row }) => new Date(row.original.updatedAtUtc).toLocaleString(),
      },
    ],
    [language, t],
  )

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('access.categories.title')}
        description={t('access.categories.description')}
        actions={
          <Button asChild variant="secondary">
            <Link to="/it/access">{t('access.back')}</Link>
          </Button>
        }
      />
      <AccessNavTabs />

      <DataTable
        columns={columns}
        data={listQuery.data ?? []}
        emptyMessage={t('access.categories.empty')}
        isLoading={listQuery.isLoading}
        onRowClick={(row) => navigate(`/it/access/configuration/${row.id}`)}
        getRowId={(row) => row.id}
      />

      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t('access.categories.new')}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-1">
              <Label htmlFor="cat-key">{t('access.categories.key')}</Label>
              <Input id="cat-key" value={key} onChange={(e) => setKey(e.target.value)} placeholder="IT_ACCESS" />
            </div>
            <div className="space-y-1">
              <Label htmlFor="cat-name-en">{t('access.categories.nameEn')}</Label>
              <Input id="cat-name-en" value={nameEn} onChange={(e) => setNameEn(e.target.value)} />
            </div>
            <div className="space-y-1">
              <Label htmlFor="cat-name-ar">{t('access.categories.nameAr')}</Label>
              <Input id="cat-name-ar" value={nameAr} onChange={(e) => setNameAr(e.target.value)} dir="rtl" />
            </div>
            <div className="space-y-1">
              <Label htmlFor="cat-desc-en">{t('access.categories.descriptionEn')}</Label>
              <Input id="cat-desc-en" value={descriptionEn} onChange={(e) => setDescriptionEn(e.target.value)} />
            </div>
            <div className="space-y-1 sm:col-span-2">
              <Label htmlFor="cat-desc-ar">{t('access.categories.descriptionAr')}</Label>
              <Input
                id="cat-desc-ar"
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
          {error ? <p className="text-sm text-destructive">{error}</p> : null}
          <Button
            type="button"
            disabled={!key.trim() || !nameEn.trim() || !nameAr.trim() || createMutation.isPending}
            onClick={() => createMutation.mutate()}
          >
            {t('access.categories.create')}
          </Button>
        </CardContent>
      </Card>
    </div>
  )
}
