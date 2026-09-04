import { useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'
import { controlsApi, type ControlListItem } from '@/api/client'
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

export function ControlsPage() {
  const { t } = useTranslation()
  const { can } = useAuth()
  const navigate = useNavigate()
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [domain, setDomain] = useState('all')
  const [status, setStatus] = useState('all')

  const domainsQuery = useQuery({
    queryKey: ['controls', 'domains'],
    queryFn: () => controlsApi.listDomains(),
  })

  const listQuery = useQuery({
    queryKey: ['controls', search, domain, status],
    queryFn: () =>
      controlsApi.list({
        pageSize: 50,
        search: search || undefined,
        domain: domain === 'all' ? undefined : domain,
        status: status === 'all' ? undefined : status,
      }),
  })

  const columns = useMemo<ColumnDef<ControlListItem, unknown>[]>(
    () => [
      { accessorKey: 'controlNumber', header: t('controls.columns.number') },
      { accessorKey: 'title', header: t('controls.columns.title') },
      { accessorKey: 'domainLabel', header: t('controls.columns.domain') },
      {
        id: 'owner',
        header: t('controls.columns.owner'),
        cell: ({ row }) => row.original.primaryOwnerUserId?.slice(0, 8) ?? '—',
      },
      { accessorKey: 'frequency', header: t('controls.columns.frequency') },
      { accessorKey: 'automationType', header: t('controls.columns.automation') },
      {
        accessorKey: 'status',
        header: t('controls.columns.status'),
        cell: ({ row }) => <Badge variant="secondary">{row.original.status}</Badge>,
      },
    ],
    [t],
  )

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('controls.title')}
        description={t('controls.description')}
        actions={
          <div className="flex flex-wrap gap-2">
            <Button asChild variant="outline">
              <Link to="/it/governance">{t('governance.nav.back')}</Link>
            </Button>
            {can('control.manage') ? (
              <Button asChild>
                <Link to="/it/controls/new">{t('controls.new')}</Link>
              </Button>
            ) : null}
          </div>
        }
      />

      <div className="flex flex-wrap gap-2">
        <Input
          className="max-w-xs"
          value={searchInput}
          onChange={(e) => setSearchInput(e.target.value)}
          placeholder={t('governance.searchPlaceholder')}
          onKeyDown={(e) => {
            if (e.key === 'Enter') setSearch(searchInput.trim())
          }}
        />
        <Button type="button" variant="secondary" onClick={() => setSearch(searchInput.trim())}>
          {t('governance.search')}
        </Button>
        <Select value={domain} onValueChange={setDomain}>
          <SelectTrigger className="w-48">
            <SelectValue placeholder={t('controls.columns.domain')} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t('governance.all')}</SelectItem>
            {(domainsQuery.data ?? []).map((d) => (
              <SelectItem key={d.code} value={d.code}>
                {d.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select value={status} onValueChange={setStatus}>
          <SelectTrigger className="w-40">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t('governance.all')}</SelectItem>
            <SelectItem value="Draft">Draft</SelectItem>
            <SelectItem value="Active">Active</SelectItem>
            <SelectItem value="Retired">Retired</SelectItem>
          </SelectContent>
        </Select>
      </div>

      <DataTable
        columns={columns}
        data={listQuery.data?.items ?? []}
        isLoading={listQuery.isLoading}
        onRowClick={(row) => navigate(`/it/controls/${row.id}`)}
      />
    </div>
  )
}

export function ControlNewPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [title, setTitle] = useState('')
  const [objective, setObjective] = useState('')
  const [description, setDescription] = useState('')
  const [domain, setDomain] = useState('IAM')
  const [frequency, setFrequency] = useState('Quarterly')
  const [automationType, setAutomationType] = useState('Manual')

  const domainsQuery = useQuery({
    queryKey: ['controls', 'domains'],
    queryFn: () => controlsApi.listDomains(),
  })

  const createMutation = useMutation({
    mutationFn: () =>
      controlsApi.create({
        title,
        objective,
        description,
        domain,
        frequency,
        automationType,
      }),
    onSuccess: async (created) => {
      await qc.invalidateQueries({ queryKey: ['controls'] })
      navigate(`/it/controls/${created.id}`)
    },
  })

  return (
    <div className="space-y-6 max-w-2xl">
      <PageHeader
        title={t('controls.new')}
        description={t('controls.description')}
        actions={
          <Button asChild variant="outline">
            <Link to="/it/controls">{t('controls.back')}</Link>
          </Button>
        }
      />
      <div className="space-y-3">
        <Input value={title} onChange={(e) => setTitle(e.target.value)} placeholder={t('controls.fields.title')} />
        <Input
          value={objective}
          onChange={(e) => setObjective(e.target.value)}
          placeholder={t('controls.fields.objective')}
        />
        <Input
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          placeholder={t('controls.fields.description')}
        />
        <Select value={domain} onValueChange={setDomain}>
          <SelectTrigger>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {(domainsQuery.data ?? []).map((d) => (
              <SelectItem key={d.code} value={d.code}>
                {d.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select value={frequency} onValueChange={setFrequency}>
          <SelectTrigger>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {['Continuous', 'Daily', 'Weekly', 'Monthly', 'Quarterly', 'SemiAnnual', 'Annual', 'EventDriven', 'AdHoc'].map(
              (f) => (
                <SelectItem key={f} value={f}>
                  {f}
                </SelectItem>
              ),
            )}
          </SelectContent>
        </Select>
        <Select value={automationType} onValueChange={setAutomationType}>
          <SelectTrigger>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="Manual">Manual</SelectItem>
            <SelectItem value="Automated">Automated</SelectItem>
            <SelectItem value="ItmgNative">ITMG Native</SelectItem>
          </SelectContent>
        </Select>
        <Button
          type="button"
          disabled={!title.trim() || !objective.trim() || !description.trim() || createMutation.isPending}
          onClick={() => createMutation.mutate()}
        >
          {t('controls.create')}
        </Button>
      </div>
    </div>
  )
}
