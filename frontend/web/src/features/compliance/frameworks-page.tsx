import { useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'
import { complianceApi, type ComplianceFramework, type FrameworkRequirement } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { DataTable } from '@/components/shared/data-table'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'

export function FrameworksPage() {
  const { t } = useTranslation()
  const { can } = useAuth()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const listQuery = useQuery({
    queryKey: ['compliance', 'frameworks'],
    queryFn: () => complianceApi.listFrameworks(),
  })
  const seedMutation = useMutation({
    mutationFn: () => complianceApi.seedStructure(),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['compliance', 'frameworks'] })
    },
  })

  const columns = useMemo<ColumnDef<ComplianceFramework, unknown>[]>(
    () => [
      { accessorKey: 'code', header: t('compliance.frameworks.columns.code') },
      { accessorKey: 'name', header: t('compliance.frameworks.columns.name') },
      { accessorKey: 'publisher', header: t('compliance.frameworks.columns.publisher') },
      {
        accessorKey: 'isActive',
        header: t('compliance.frameworks.columns.active'),
        cell: ({ row }) => (row.original.isActive ? 'Yes' : 'No'),
      },
    ],
    [t],
  )

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('compliance.frameworks.title')}
        description={t('compliance.frameworks.description')}
        actions={
          <div className="flex flex-wrap gap-2">
            <Button asChild variant="outline">
              <Link to="/it/compliance">{t('compliance.nav.back')}</Link>
            </Button>
            {can('framework.manage') ? (
              <Button type="button" variant="secondary" onClick={() => seedMutation.mutate()}>
                {t('compliance.frameworks.seed')}
              </Button>
            ) : null}
          </div>
        }
      />
      <DataTable
        columns={columns}
        data={listQuery.data ?? []}
        isLoading={listQuery.isLoading}
        onRowClick={(row) => navigate(`/it/compliance/frameworks/${row.id}`)}
      />
    </div>
  )
}

export function FrameworkDetailPage() {
  const { id = '' } = useParams()
  const { t } = useTranslation()
  const detailQuery = useQuery({
    queryKey: ['compliance', 'frameworks', id],
    queryFn: () => complianceApi.getFramework(id),
    enabled: !!id,
  })
  const versions = detailQuery.data?.versions ?? []
  const [versionId, setVersionId] = useState<string>('')
  const selectedVersion =
    versions.find((v) => v.id === versionId) ?? versions.find((v) => v.isCurrent) ?? versions[0]

  const reqQuery = useQuery({
    queryKey: ['compliance', 'requirements', selectedVersion?.id],
    queryFn: () => complianceApi.listRequirements(selectedVersion!.id),
    enabled: !!selectedVersion?.id,
  })
  const coverageQuery = useQuery({
    queryKey: ['compliance', 'coverage', selectedVersion?.id],
    queryFn: () => complianceApi.coverage(selectedVersion!.id),
    enabled: !!selectedVersion?.id,
  })

  const reqColumns = useMemo<ColumnDef<FrameworkRequirement, unknown>[]>(
    () => [
      { accessorKey: 'code', header: t('compliance.frameworks.columns.code') },
      { accessorKey: 'title', header: t('compliance.frameworks.columns.name') },
      {
        accessorKey: 'requirementType',
        header: t('compliance.frameworks.columns.type'),
        cell: ({ row }) => <Badge variant="outline">{row.original.requirementType}</Badge>,
      },
    ],
    [t],
  )

  const fw = detailQuery.data?.framework
  if (detailQuery.isLoading) return <p className="text-sm text-muted-foreground">{t('compliance.loading')}</p>
  if (!fw) return <p className="text-sm text-muted-foreground">{t('compliance.notFound')}</p>
  const cov = coverageQuery.data

  return (
    <div className="space-y-6">
      <PageHeader
        title={`${fw.code} · ${fw.name}`}
        description={fw.description ?? t('compliance.frameworks.description')}
        actions={
          <Button asChild variant="outline">
            <Link to="/it/compliance/frameworks">{t('compliance.nav.backFrameworks')}</Link>
          </Button>
        }
      />

      <div className="flex flex-wrap gap-2 items-center">
        <Select
          value={selectedVersion?.id ?? ''}
          onValueChange={setVersionId}
        >
          <SelectTrigger className="w-64">
            <SelectValue placeholder={t('compliance.frameworks.version')} />
          </SelectTrigger>
          <SelectContent>
            {versions.map((v) => (
              <SelectItem key={v.id} value={v.id}>
                {v.versionCode}
                {v.isCurrent ? ' (current)' : ''}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {cov ? (
        <Card>
          <CardHeader>
            <CardTitle>{t('compliance.overview.counts')}</CardTitle>
          </CardHeader>
          <CardContent className="text-sm space-y-1">
            <p>
              {t('compliance.coverage.mapped')}: {cov.mappedRequirements} / {cov.totalRequirements}
            </p>
            <p>
              {t('compliance.coverage.assessed')}: {cov.assessedControls} / {cov.mappedControls}
            </p>
            <p className="text-muted-foreground">{cov.notes}</p>
          </CardContent>
        </Card>
      ) : null}

      <DataTable columns={reqColumns} data={reqQuery.data ?? []} isLoading={reqQuery.isLoading} />
    </div>
  )
}
