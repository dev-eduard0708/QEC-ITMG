import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'
import { complianceApi, type ControlMapping } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { DataTable } from '@/components/shared/data-table'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'

export function MappingsPage() {
  const { t } = useTranslation()
  const { can } = useAuth()
  const qc = useQueryClient()
  const [controlId, setControlId] = useState('')
  const [requirementId, setRequirementId] = useState('')

  const listQuery = useQuery({
    queryKey: ['compliance', 'mappings'],
    queryFn: () => complianceApi.listMappings(),
  })

  const createMutation = useMutation({
    mutationFn: () =>
      complianceApi.createMapping({
        internalControlId: controlId,
        frameworkRequirementId: requirementId,
        relationship: 'Primary',
      }),
    onSuccess: async () => {
      setControlId('')
      setRequirementId('')
      await qc.invalidateQueries({ queryKey: ['compliance', 'mappings'] })
    },
  })

  const columns = useMemo<ColumnDef<ControlMapping, unknown>[]>(
    () => [
      { accessorKey: 'frameworkCode', header: t('compliance.mappings.columns.framework') },
      { accessorKey: 'requirementCode', header: t('compliance.mappings.columns.requirement') },
      { accessorKey: 'requirementTitle', header: t('compliance.mappings.columns.title') },
      { accessorKey: 'internalControlId', header: t('compliance.mappings.columns.control') },
      { accessorKey: 'relationship', header: t('compliance.mappings.columns.relationship') },
    ],
    [t],
  )

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('compliance.mappings.title')}
        description={t('compliance.mappings.description')}
        actions={
          <Button asChild variant="outline">
            <Link to="/it/compliance">{t('compliance.nav.back')}</Link>
          </Button>
        }
      />
      {can('framework.manage') ? (
        <div className="flex flex-wrap gap-2">
          <Input
            className="max-w-xs"
            value={controlId}
            onChange={(e) => setControlId(e.target.value)}
            placeholder={t('compliance.mappings.controlId')}
          />
          <Input
            className="max-w-xs"
            value={requirementId}
            onChange={(e) => setRequirementId(e.target.value)}
            placeholder={t('compliance.mappings.requirementId')}
          />
          <Button
            type="button"
            disabled={!controlId.trim() || !requirementId.trim() || createMutation.isPending}
            onClick={() => createMutation.mutate()}
          >
            {t('compliance.mappings.add')}
          </Button>
        </div>
      ) : null}
      <DataTable columns={columns} data={listQuery.data ?? []} isLoading={listQuery.isLoading} />
    </div>
  )
}
