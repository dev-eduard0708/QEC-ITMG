import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'
import { complianceApi, type CalendarItem } from '@/api/client'
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

export function CalendarPage() {
  const { t } = useTranslation()
  const { can } = useAuth()
  const qc = useQueryClient()
  const [bucket, setBucket] = useState('upcoming')
  const [title, setTitle] = useState('')
  const [dueAt, setDueAt] = useState('')

  const listQuery = useQuery({
    queryKey: ['compliance', 'calendar', bucket],
    queryFn: () => complianceApi.listCalendar(bucket),
  })

  const createMutation = useMutation({
    mutationFn: () =>
      complianceApi.createCalendarItem({
        title,
        itemType: 'ControlAssessment',
        dueAtUtc: new Date(dueAt).toISOString(),
      }),
    onSuccess: async () => {
      setTitle('')
      setDueAt('')
      await qc.invalidateQueries({ queryKey: ['compliance', 'calendar'] })
    },
  })

  const columns = useMemo<ColumnDef<CalendarItem, unknown>[]>(
    () => [
      { accessorKey: 'title', header: t('compliance.calendar.columns.title') },
      { accessorKey: 'itemType', header: t('compliance.calendar.columns.type') },
      {
        id: 'due',
        header: t('compliance.calendar.columns.due'),
        cell: ({ row }) => new Date(row.original.dueAtUtc).toLocaleString(),
      },
      {
        accessorKey: 'status',
        header: t('compliance.calendar.columns.status'),
        cell: ({ row }) => (
          <Badge variant={row.original.isOverdue ? 'outline' : 'secondary'}>{row.original.status}</Badge>
        ),
      },
    ],
    [t],
  )

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('compliance.calendar.title')}
        description={t('compliance.calendar.description')}
        actions={
          <Button asChild variant="outline">
            <Link to="/it/compliance">{t('compliance.nav.back')}</Link>
          </Button>
        }
      />
      <Select value={bucket} onValueChange={setBucket}>
        <SelectTrigger className="w-48">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="upcoming">{t('compliance.calendar.upcoming')}</SelectItem>
          <SelectItem value="overdue">{t('compliance.calendar.overdue')}</SelectItem>
          <SelectItem value="completed">{t('compliance.calendar.completed')}</SelectItem>
          <SelectItem value="all">{t('compliance.calendar.all')}</SelectItem>
        </SelectContent>
      </Select>
      {can('framework.manage') ? (
        <div className="flex flex-wrap gap-2">
          <Input
            className="max-w-xs"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder={t('compliance.calendar.columns.title')}
          />
          <Input
            className="max-w-xs"
            type="datetime-local"
            value={dueAt}
            onChange={(e) => setDueAt(e.target.value)}
          />
          <Button type="button" disabled={!title.trim() || !dueAt} onClick={() => createMutation.mutate()}>
            {t('compliance.calendar.add')}
          </Button>
        </div>
      ) : null}
      <DataTable columns={columns} data={listQuery.data ?? []} isLoading={listQuery.isLoading} />
    </div>
  )
}
