import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'
import { complianceApi, type ControlAssessment } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { DataTable } from '@/components/shared/data-table'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'

export function AssessmentsPage() {
  const { t } = useTranslation()
  const { can } = useAuth()
  const qc = useQueryClient()
  const [controlId, setControlId] = useState('')
  const [completeId, setCompleteId] = useState('')
  const [result, setResult] = useState('Compliant')

  const listQuery = useQuery({
    queryKey: ['compliance', 'assessments'],
    queryFn: () => complianceApi.listAssessments({ pageSize: 50 }),
  })

  const createMutation = useMutation({
    mutationFn: () => complianceApi.createAssessment({ internalControlId: controlId }),
    onSuccess: async () => {
      setControlId('')
      await qc.invalidateQueries({ queryKey: ['compliance', 'assessments'] })
    },
  })
  const startMutation = useMutation({
    mutationFn: (id: string) => complianceApi.startAssessment(id),
    onSuccess: async () => qc.invalidateQueries({ queryKey: ['compliance', 'assessments'] }),
  })
  const completeMutation = useMutation({
    mutationFn: () => complianceApi.completeAssessment(completeId, result),
    onSuccess: async () => {
      setCompleteId('')
      await qc.invalidateQueries({ queryKey: ['compliance', 'assessments'] })
    },
  })

  const columns = useMemo<ColumnDef<ControlAssessment, unknown>[]>(
    () => [
      { accessorKey: 'internalControlId', header: t('compliance.assessments.columns.control') },
      {
        accessorKey: 'status',
        header: t('compliance.assessments.columns.status'),
        cell: ({ row }) => <Badge variant="secondary">{row.original.status}</Badge>,
      },
      { accessorKey: 'result', header: t('compliance.assessments.columns.result') },
      {
        id: 'updated',
        header: t('compliance.assessments.columns.updated'),
        cell: ({ row }) => new Date(row.original.updatedAtUtc).toLocaleString(),
      },
      {
        id: 'actions',
        header: '',
        cell: ({ row }) =>
          can('assessment.perform') && row.original.status === 'NotStarted' ? (
            <Button type="button" size="sm" variant="secondary" onClick={() => startMutation.mutate(row.original.id)}>
              {t('compliance.assessments.start')}
            </Button>
          ) : null,
      },
    ],
    [t, can, startMutation],
  )

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('compliance.assessments.title')}
        description={t('compliance.assessments.description')}
        actions={
          <Button asChild variant="outline">
            <Link to="/it/compliance">{t('compliance.nav.back')}</Link>
          </Button>
        }
      />
      {can('assessment.perform') ? (
        <div className="space-y-3">
          <div className="flex flex-wrap gap-2">
            <Input
              className="max-w-xs"
              value={controlId}
              onChange={(e) => setControlId(e.target.value)}
              placeholder={t('compliance.assessments.controlId')}
            />
            <Button type="button" disabled={!controlId.trim()} onClick={() => createMutation.mutate()}>
              {t('compliance.assessments.create')}
            </Button>
          </div>
          <div className="flex flex-wrap gap-2">
            <Input
              className="max-w-xs"
              value={completeId}
              onChange={(e) => setCompleteId(e.target.value)}
              placeholder={t('compliance.assessments.assessmentId')}
            />
            <Select value={result} onValueChange={setResult}>
              <SelectTrigger className="w-48">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {['Compliant', 'PartiallyCompliant', 'NonCompliant', 'NotApplicable', 'NotTested'].map((r) => (
                  <SelectItem key={r} value={r}>
                    {r}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Button type="button" disabled={!completeId.trim()} onClick={() => completeMutation.mutate()}>
              {t('compliance.assessments.complete')}
            </Button>
          </div>
        </div>
      ) : null}
      <DataTable columns={columns} data={listQuery.data?.items ?? []} isLoading={listQuery.isLoading} />
    </div>
  )
}
