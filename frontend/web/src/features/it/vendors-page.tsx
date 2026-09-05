import { useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'
import {
  cmdbApi,
  vendorsApi,
  type VendorItem,
  type VendorContractItem,
  type VendorAssessmentItem,
} from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { DataTable } from '@/components/shared/data-table'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'

type Section = 'overview' | 'contracts' | 'assessments' | 'access' | 'services' | 'history'

export function VendorsPage() {
  const { t } = useTranslation()
  const { can } = useAuth()
  const navigate = useNavigate()
  const [search, setSearch] = useState('')

  const dashQuery = useQuery({
    queryKey: ['vendors', 'dashboard'],
    queryFn: () => vendorsApi.dashboard(),
  })
  const listQuery = useQuery({
    queryKey: ['vendors', 'list', search],
    queryFn: () => vendorsApi.list({ search: search || undefined }),
  })

  const columns = useMemo<ColumnDef<VendorItem, unknown>[]>(
    () => [
      { accessorKey: 'vendorNumber', header: t('vendors.columns.number') },
      { accessorKey: 'name', header: t('vendors.columns.name') },
      { accessorKey: 'criticality', header: t('vendors.columns.criticality') },
      {
        accessorKey: 'status',
        header: t('vendors.columns.status'),
        cell: ({ row }) => <Badge variant="secondary">{row.original.status}</Badge>,
      },
    ],
    [t],
  )

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('vendors.title')}
        description={t('vendors.description')}
        actions={
          can('vendor.manage') ? (
            <Button type="button" onClick={() => navigate('/it/vendors/new')}>
              {t('vendors.create')}
            </Button>
          ) : null
        }
      />
      {dashQuery.data ? (
        <div className="space-y-3">
          <p className="text-sm text-muted-foreground">{dashQuery.data.note}</p>
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            {(
              [
                [t('vendors.dash.active'), dashQuery.data.activeVendors],
                [t('vendors.dash.critical'), dashQuery.data.criticalVendors],
                [t('vendors.dash.expiring'), dashQuery.data.contractsExpiring],
                [t('vendors.dash.expired'), dashQuery.data.expiredContracts],
                [t('vendors.dash.assessmentsDue'), dashQuery.data.assessmentsDue],
                [t('vendors.dash.assessmentsOverdue'), dashQuery.data.assessmentsOverdue],
                [t('vendors.dash.privileged'), dashQuery.data.vendorsWithPrivilegedAccess],
                [t('vendors.dash.risks'), dashQuery.data.openVendorLinkedRisks],
              ] as const
            ).map(([label, value]) => (
              <Card key={label}>
                <CardHeader className="pb-2">
                  <CardTitle className="text-sm font-medium">{label}</CardTitle>
                </CardHeader>
                <CardContent className="text-2xl font-semibold tabular-nums">{value}</CardContent>
              </Card>
            ))}
          </div>
        </div>
      ) : null}
      <Input
        className="max-w-xs"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        placeholder={t('vendors.searchPlaceholder')}
      />
      <DataTable
        columns={columns}
        data={listQuery.data ?? []}
        onRowClick={(row) => navigate(`/it/vendors/${row.id}`)}
      />
    </div>
  )
}

export function VendorNewPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [name, setName] = useState('')
  const [criticality, setCriticality] = useState('Medium')
  const [contact, setContact] = useState('')

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('vendors.create')}
        description={t('vendors.description')}
        actions={
          <Link to="/it/vendors" className="text-sm text-primary underline">
            {t('vendors.back')}
          </Link>
        }
      />
      <div className="flex flex-wrap gap-2">
        <Input className="max-w-xs" value={name} onChange={(e) => setName(e.target.value)} placeholder={t('vendors.fields.name')} />
        <Select value={criticality} onValueChange={setCriticality}>
          <SelectTrigger className="w-[160px]">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {['Low', 'Medium', 'High', 'Critical'].map((c) => (
              <SelectItem key={c} value={c}>
                {c}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Input
          className="max-w-xs"
          value={contact}
          onChange={(e) => setContact(e.target.value)}
          placeholder={t('vendors.fields.contact')}
        />
        <Button
          type="button"
          disabled={!name.trim()}
          onClick={async () => {
            const created = await vendorsApi.create({
              name: name.trim(),
              criticality,
              primaryContactName: contact.trim() || null,
            })
            navigate(`/it/vendors/${created.id}`)
          }}
        >
          {t('vendors.create')}
        </Button>
      </div>
    </div>
  )
}

export function VendorDetailPage() {
  const { t } = useTranslation()
  const { id = '' } = useParams()
  const { can } = useAuth()
  const qc = useQueryClient()
  const [section, setSection] = useState<Section>('overview')
  const [contractTitle, setContractTitle] = useState('')
  const [contractEnd, setContractEnd] = useState('')
  const [slaRef, setSlaRef] = useState('')
  const [assessType, setAssessType] = useState('Security')
  const [assessDue, setAssessDue] = useState('')
  const [ciId, setCiId] = useState('')
  const [caseId, setCaseId] = useState('')
  const [accountId, setAccountId] = useState('')
  const [contactName, setContactName] = useState('')
  const [linkType, setLinkType] = useState('Risk')
  const [linkId, setLinkId] = useState('')

  const vendorQuery = useQuery({
    queryKey: ['vendors', id],
    queryFn: () => vendorsApi.get(id),
    enabled: !!id,
  })
  const contractsQuery = useQuery({
    queryKey: ['vendors', id, 'contracts'],
    queryFn: () => vendorsApi.listContracts(id),
    enabled: !!id && (section === 'contracts' || section === 'overview'),
  })
  const assessmentsQuery = useQuery({
    queryKey: ['vendors', id, 'assessments'],
    queryFn: () => vendorsApi.listAssessments(id),
    enabled: !!id && (section === 'assessments' || section === 'overview'),
  })
  const accessQuery = useQuery({
    queryKey: ['vendors', id, 'access'],
    queryFn: () => vendorsApi.getAccess(id),
    enabled: !!id && section === 'access',
  })
  const cisQuery = useQuery({
    queryKey: ['vendors', id, 'cis'],
    queryFn: () => vendorsApi.listCis(id),
    enabled: !!id && section === 'services',
  })
  const linksQuery = useQuery({
    queryKey: ['vendors', id, 'links'],
    queryFn: () => vendorsApi.listLinks(id),
    enabled: !!id && section === 'history',
  })
  const contactsQuery = useQuery({
    queryKey: ['vendors', id, 'contacts'],
    queryFn: () => vendorsApi.listContacts(id),
    enabled: !!id && section === 'overview',
  })

  const refresh = async (...keys: string[]) => {
    for (const key of keys) await qc.invalidateQueries({ queryKey: ['vendors', id, key] })
    await qc.invalidateQueries({ queryKey: ['vendors', id] })
    await qc.invalidateQueries({ queryKey: ['vendors', 'dashboard'] })
  }

  const sections: [Section, string][] = [
    ['overview', 'vendors.sections.overview'],
    ['contracts', 'vendors.sections.contracts'],
    ['assessments', 'vendors.sections.assessments'],
    ['access', 'vendors.sections.access'],
    ['services', 'vendors.sections.services'],
    ['history', 'vendors.sections.history'],
  ]

  const contractColumns = useMemo<ColumnDef<VendorContractItem, unknown>[]>(
    () => [
      { accessorKey: 'contractNumber', header: t('vendors.columns.number') },
      { accessorKey: 'title', header: t('vendors.columns.title') },
      { accessorKey: 'status', header: t('vendors.columns.status') },
      {
        accessorKey: 'daysToExpiry',
        header: t('vendors.columns.daysToExpiry'),
        cell: ({ row }) => (
          <Badge variant={row.original.expired || row.original.expiringSoon ? 'warning' : 'secondary'}>
            {row.original.expired ? t('vendors.expired') : (row.original.daysToExpiry ?? '—')}
          </Badge>
        ),
      },
      { accessorKey: 'slaReference', header: t('vendors.columns.sla') },
    ],
    [t],
  )

  const assessColumns = useMemo<ColumnDef<VendorAssessmentItem, unknown>[]>(
    () => [
      { accessorKey: 'assessmentNumber', header: t('vendors.columns.number') },
      { accessorKey: 'assessmentType', header: t('vendors.columns.type') },
      {
        accessorKey: 'status',
        header: t('vendors.columns.status'),
        cell: ({ row }) => (
          <Badge variant={row.original.assessmentOverdue ? 'warning' : 'secondary'}>
            {row.original.assessmentOverdue ? t('vendors.overdue') : row.original.status}
          </Badge>
        ),
      },
      { accessorKey: 'result', header: t('vendors.columns.result') },
    ],
    [t],
  )

  if (vendorQuery.isLoading) return <p>{t('vendors.loading')}</p>
  if (!vendorQuery.data) return <p>{t('vendors.notFound')}</p>
  const vendor = vendorQuery.data

  return (
    <div className="space-y-6">
      <PageHeader
        title={`${vendor.vendorNumber} · ${vendor.name}`}
        description={vendor.serviceDescription ?? t('vendors.description')}
        actions={
          <Link to="/it/vendors" className="text-sm text-primary underline">
            {t('vendors.back')}
          </Link>
        }
      />
      <div className="flex flex-wrap gap-2">
        {sections.map(([key, label]) => (
          <Button
            key={key}
            type="button"
            size="sm"
            variant={section === key ? 'default' : 'outline'}
            onClick={() => setSection(key)}
          >
            {t(label)}
          </Button>
        ))}
      </div>

      {section === 'overview' ? (
        <div className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <Card>
              <CardHeader>
                <CardTitle className="text-sm">{t('vendors.sections.overview')}</CardTitle>
              </CardHeader>
              <CardContent className="space-y-1 text-sm">
                <p>
                  {t('vendors.columns.status')}: {vendor.status}
                </p>
                <p>
                  {t('vendors.columns.criticality')}: {vendor.criticality}
                </p>
                <p>
                  {t('vendors.fields.contact')}: {vendor.primaryContactName ?? '—'}
                </p>
                <p>{vendor.primaryContactEmail}</p>
              </CardContent>
            </Card>
            <Card>
              <CardHeader>
                <CardTitle className="text-sm">{t('vendors.contacts')}</CardTitle>
              </CardHeader>
              <CardContent className="space-y-2 text-sm">
                {(contactsQuery.data ?? []).map((c) => (
                  <p key={c.id}>
                    {c.name}
                    {c.isPrimary ? ' ★' : ''} {c.email ?? ''}
                  </p>
                ))}
                {can('vendor.manage') ? (
                  <div className="flex flex-wrap gap-2 pt-2">
                    <Input
                      className="max-w-xs"
                      value={contactName}
                      onChange={(e) => setContactName(e.target.value)}
                      placeholder={t('vendors.fields.contact')}
                    />
                    <Button
                      type="button"
                      size="sm"
                      disabled={!contactName.trim()}
                      onClick={async () => {
                        await vendorsApi.addContact(id, { name: contactName.trim(), isPrimary: false })
                        setContactName('')
                        await refresh('contacts')
                      }}
                    >
                      {t('vendors.addContact')}
                    </Button>
                  </div>
                ) : null}
              </CardContent>
            </Card>
          </div>
        </div>
      ) : null}

      {section === 'contracts' ? (
        <div className="space-y-4">
          {can('contract.manage') ? (
            <div className="flex flex-wrap gap-2">
              <Input
                className="max-w-xs"
                value={contractTitle}
                onChange={(e) => setContractTitle(e.target.value)}
                placeholder={t('vendors.fields.contractTitle')}
              />
              <Input
                className="max-w-[160px]"
                type="date"
                value={contractEnd}
                onChange={(e) => setContractEnd(e.target.value)}
              />
              <Input
                className="max-w-xs"
                value={slaRef}
                onChange={(e) => setSlaRef(e.target.value)}
                placeholder={t('vendors.fields.slaReference')}
              />
              <Button
                type="button"
                disabled={!contractTitle.trim()}
                onClick={async () => {
                  await vendorsApi.createContract(id, {
                    title: contractTitle.trim(),
                    startDate: new Date().toISOString().slice(0, 10),
                    endDate: contractEnd || null,
                    slaReference: slaRef.trim() || null,
                  })
                  setContractTitle('')
                  setSlaRef('')
                  await refresh('contracts')
                }}
              >
                {t('vendors.createContract')}
              </Button>
            </div>
          ) : null}
          <DataTable columns={contractColumns} data={contractsQuery.data ?? []} />
          {can('contract.manage')
            ? (contractsQuery.data ?? [])
                .filter((c) => c.status === 'Draft' || c.status === 'Active')
                .slice(0, 5)
                .map((c) => (
                  <div key={c.id} className="flex flex-wrap gap-2 text-sm">
                    <span>{c.contractNumber}</span>
                    {c.status === 'Draft' ? (
                      <Button
                        type="button"
                        size="sm"
                        variant="outline"
                        onClick={async () => {
                          await vendorsApi.transitionContract(c.id, 'Active')
                          await refresh('contracts')
                        }}
                      >
                        Active
                      </Button>
                    ) : null}
                    {c.status === 'Active' ? (
                      <Button
                        type="button"
                        size="sm"
                        variant="outline"
                        onClick={async () => {
                          await vendorsApi.transitionContract(c.id, 'Expired')
                          await refresh('contracts')
                        }}
                      >
                        Expired
                      </Button>
                    ) : null}
                  </div>
                ))
            : null}
        </div>
      ) : null}

      {section === 'assessments' ? (
        <div className="space-y-4">
          {can('vendor.assess') ? (
            <div className="flex flex-wrap gap-2">
              <Select value={assessType} onValueChange={setAssessType}>
                <SelectTrigger className="w-[180px]">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {['DueDiligence', 'Security', 'Risk', 'Performance', 'AnnualReview', 'Other'].map((x) => (
                    <SelectItem key={x} value={x}>
                      {x}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <Input className="max-w-[200px]" type="datetime-local" value={assessDue} onChange={(e) => setAssessDue(e.target.value)} />
              <Button
                type="button"
                onClick={async () => {
                  await vendorsApi.createAssessment(id, {
                    assessmentType: assessType,
                    dueAtUtc: assessDue ? new Date(assessDue).toISOString() : null,
                  })
                  await refresh('assessments')
                }}
              >
                {t('vendors.createAssessment')}
              </Button>
            </div>
          ) : null}
          <DataTable columns={assessColumns} data={assessmentsQuery.data ?? []} />
          {can('vendor.assess')
            ? (assessmentsQuery.data ?? []).map((a) => (
                <div key={a.id} className="flex flex-wrap gap-2 text-sm">
                  <span>
                    {a.assessmentNumber} · {a.status}
                  </span>
                  {a.status === 'Scheduled' ? (
                    <Button
                      type="button"
                      size="sm"
                      variant="outline"
                      onClick={async () => {
                        await vendorsApi.transitionAssessment(a.id, 'InProgress')
                        await refresh('assessments')
                      }}
                    >
                      InProgress
                    </Button>
                  ) : null}
                  {a.status === 'InProgress' ? (
                    <Button
                      type="button"
                      size="sm"
                      variant="outline"
                      onClick={async () => {
                        await vendorsApi.transitionAssessment(a.id, 'Review')
                        await refresh('assessments')
                      }}
                    >
                      Review
                    </Button>
                  ) : null}
                  {a.status === 'Review' ? (
                    <Button
                      type="button"
                      size="sm"
                      variant="outline"
                      onClick={async () => {
                        await vendorsApi.transitionAssessment(a.id, 'Complete', 'Satisfactory')
                        await refresh('assessments')
                      }}
                    >
                      Complete
                    </Button>
                  ) : null}
                </div>
              ))
            : null}
        </div>
      ) : null}

      {section === 'access' ? (
        <div className="space-y-4 text-sm">
          <p className="text-muted-foreground">{t('vendors.accessHint')}</p>
          {can('vendor.manage') ? (
            <div className="flex flex-wrap gap-2">
              <Input className="max-w-xs" value={caseId} onChange={(e) => setCaseId(e.target.value)} placeholder={t('vendors.fields.caseId')} />
              <Button
                type="button"
                size="sm"
                disabled={!caseId}
                onClick={async () => {
                  await vendorsApi.linkAccessCase(id, caseId.trim())
                  setCaseId('')
                  await refresh('access')
                }}
              >
                {t('vendors.linkCase')}
              </Button>
              <Input
                className="max-w-xs"
                value={accountId}
                onChange={(e) => setAccountId(e.target.value)}
                placeholder={t('vendors.fields.accountId')}
              />
              <Button
                type="button"
                size="sm"
                disabled={!accountId}
                onClick={async () => {
                  await vendorsApi.linkManagedAccount(id, accountId.trim())
                  setAccountId('')
                  await refresh('access')
                }}
              >
                {t('vendors.linkAccount')}
              </Button>
            </div>
          ) : null}
          <div>
            <h3 className="mb-1 font-medium">{t('vendors.accessCases')}</h3>
            <ul>
              {(accessQuery.data?.accessCases ?? []).map((c) => (
                <li key={c.id}>
                  {c.caseNumber} · {c.type} · {c.status}
                </li>
              ))}
            </ul>
          </div>
          <div>
            <h3 className="mb-1 font-medium">{t('vendors.managedAccounts')}</h3>
            <ul>
              {(accessQuery.data?.managedAccounts ?? []).map((a) => (
                <li key={a.id}>
                  {a.accountName} · {a.type} · {a.status}
                </li>
              ))}
            </ul>
          </div>
          <div>
            <h3 className="mb-1 font-medium">{t('vendors.vendorUsers')}</h3>
            <ul>
              {(accessQuery.data?.vendorUsers ?? []).map((u) => (
                <li key={u.id}>
                  {u.displayName} ({u.upn})
                </li>
              ))}
            </ul>
          </div>
        </div>
      ) : null}

      {section === 'services' ? (
        <div className="space-y-4">
          {can('vendor.manage') ? (
            <div className="flex flex-wrap gap-2">
              <Input className="max-w-xs" value={ciId} onChange={(e) => setCiId(e.target.value)} placeholder={t('vendors.fields.ciId')} />
              <Button
                type="button"
                disabled={!ciId}
                onClick={async () => {
                  const ci = await cmdbApi.getCi(ciId.trim())
                  await vendorsApi.linkCi(id, ci.id, ci.rowVersion)
                  setCiId('')
                  await refresh('cis')
                }}
              >
                {t('vendors.linkCi')}
              </Button>
            </div>
          ) : null}
          <ul className="space-y-1 text-sm">
            {(cisQuery.data ?? []).map((ci) => (
              <li key={ci.id}>
                {ci.ciNumber} — {ci.name}
              </li>
            ))}
          </ul>
        </div>
      ) : null}

      {section === 'history' ? (
        <div className="space-y-4">
          {can('vendor.manage') ? (
            <div className="flex flex-wrap gap-2">
              <Select value={linkType} onValueChange={setLinkType}>
                <SelectTrigger className="w-[180px]">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {['Evidence', 'Risk', 'InternalControl', 'ManagedDocument'].map((x) => (
                    <SelectItem key={x} value={x}>
                      {x}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <Input
                className="max-w-xs"
                value={linkId}
                onChange={(e) => setLinkId(e.target.value)}
                placeholder={t('vendors.fields.targetId')}
              />
              <Button
                type="button"
                disabled={!linkId}
                onClick={async () => {
                  await vendorsApi.addLink(id, linkType, linkId.trim())
                  setLinkId('')
                  await refresh('links')
                }}
              >
                {t('vendors.addLink')}
              </Button>
            </div>
          ) : null}
          <ul className="text-sm text-muted-foreground">
            {(linksQuery.data ?? []).map((l) => (
              <li key={l.id}>
                {l.targetType}: {l.targetId}
              </li>
            ))}
          </ul>
        </div>
      ) : null}
    </div>
  )
}
