import { useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'
import {
  opsApi,
  type BackupJob,
  type BackupRun,
  type CertificateRecord,
  type PatchBaseline,
  type PatchDeployment,
  type RestoreTest,
  type ScheduledJob,
  ApiError,
} from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { DataTable } from '@/components/shared/data-table'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { cn } from '@/lib/utils'

type TabId = 'events' | 'backups' | 'restore' | 'certificates' | 'patches' | 'jobs'

const tabs: { id: TabId; labelKey: string }[] = [
  { id: 'events', labelKey: 'ops.tabs.events' },
  { id: 'backups', labelKey: 'ops.tabs.backups' },
  { id: 'restore', labelKey: 'ops.tabs.restore' },
  { id: 'certificates', labelKey: 'ops.tabs.certificates' },
  { id: 'patches', labelKey: 'ops.tabs.patches' },
  { id: 'jobs', labelKey: 'ops.tabs.jobs' },
]

export function OperationsPage() {
  const { t } = useTranslation()
  const { can } = useAuth()
  const [params, setParams] = useSearchParams()
  const tabParam = params.get('tab') as TabId | null
  const tab: TabId = tabs.some((x) => x.id === tabParam) ? (tabParam as TabId) : 'events'

  const setTab = (next: TabId) => {
    const copy = new URLSearchParams(params)
    copy.set('tab', next)
    setParams(copy, { replace: true })
  }

  return (
    <div className="space-y-6">
      <PageHeader title={t('ops.title')} description={t('ops.description')} />
      <div className="flex flex-wrap gap-2 border-b pb-2">
        {tabs.map((item) => (
          <Button
            key={item.id}
            type="button"
            size="sm"
            variant={tab === item.id ? 'default' : 'ghost'}
            className={cn(tab === item.id && 'shadow-sm')}
            onClick={() => setTab(item.id)}
          >
            {t(item.labelKey)}
          </Button>
        ))}
      </div>

      {tab === 'events' && (
        <div className="space-y-3">
          <p className="text-sm text-muted-foreground">{t('ops.events.hint')}</p>
          <Button asChild>
            <Link to="/it/events">{t('ops.events.open')}</Link>
          </Button>
        </div>
      )}
      {tab === 'backups' && <BackupsPanel canManage={can('backup.manage')} />}
      {tab === 'restore' && <RestorePanel canManage={can('backup.manage')} />}
      {tab === 'certificates' && <CertificatesPanel canManage={can('cert.manage')} />}
      {tab === 'patches' && <PatchesPanel canManage={can('patch.manage')} />}
      {tab === 'jobs' && <JobsPanel canManage={can('ops.manage')} />}
    </div>
  )
}

function BackupsPanel({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [open, setOpen] = useState(false)
  const [name, setName] = useState('')
  const [provider, setProvider] = useState('Veeam')
  const [error, setError] = useState<string | null>(null)

  const jobsQuery = useQuery({
    queryKey: ['ops', 'backup-jobs'],
    queryFn: () => opsApi.listBackupJobs({ pageSize: 50 }),
  })
  const runsQuery = useQuery({
    queryKey: ['ops', 'backup-runs'],
    queryFn: () => opsApi.listBackupRuns({ pageSize: 50 }),
  })

  const createMutation = useMutation({
    mutationFn: () => opsApi.createBackupJob({ name, provider }),
    onSuccess: async () => {
      setOpen(false)
      setName('')
      setError(null)
      await qc.invalidateQueries({ queryKey: ['ops', 'backup-jobs'] })
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t('ops.error.generic')),
  })

  const jobColumns = useMemo<ColumnDef<BackupJob, unknown>[]>(
    () => [
      { accessorKey: 'name', header: t('ops.columns.name') },
      { accessorKey: 'provider', header: t('ops.columns.provider') },
      {
        accessorKey: 'isActive',
        header: t('ops.columns.active'),
        cell: ({ row }) => (
          <Badge variant={row.original.isActive ? 'secondary' : 'outline'}>
            {row.original.isActive ? t('ops.yes') : t('ops.no')}
          </Badge>
        ),
      },
    ],
    [t],
  )

  const runColumns = useMemo<ColumnDef<BackupRun, unknown>[]>(
    () => [
      { accessorKey: 'backupJobId', header: t('ops.columns.jobId') },
      {
        accessorKey: 'status',
        header: t('ops.columns.status'),
        cell: ({ row }) => <Badge variant="outline">{row.original.status}</Badge>,
      },
      {
        id: 'started',
        header: t('ops.columns.started'),
        cell: ({ row }) => new Date(row.original.startedAtUtc).toLocaleString(),
      },
    ],
    [t],
  )

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between gap-2">
        <h2 className="text-base font-medium">{t('ops.backups.jobs')}</h2>
        {canManage ? (
          <Button type="button" size="sm" onClick={() => setOpen(true)}>
            {t('ops.backups.add')}
          </Button>
        ) : null}
      </div>
      <DataTable
        columns={jobColumns}
        data={jobsQuery.data?.items ?? []}
        emptyMessage={t('ops.empty')}
        isLoading={jobsQuery.isLoading}
      />
      <h2 className="text-base font-medium">{t('ops.backups.runs')}</h2>
      <DataTable
        columns={runColumns}
        data={runsQuery.data?.items ?? []}
        emptyMessage={t('ops.empty')}
        isLoading={runsQuery.isLoading}
      />

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('ops.backups.add')}</DialogTitle>
          </DialogHeader>
          <div className="space-y-3">
            <div className="space-y-1">
              <Label htmlFor="bj-name">{t('ops.columns.name')}</Label>
              <Input id="bj-name" value={name} onChange={(e) => setName(e.target.value)} />
            </div>
            <div className="space-y-1">
              <Label htmlFor="bj-provider">{t('ops.columns.provider')}</Label>
              <Input id="bj-provider" value={provider} onChange={(e) => setProvider(e.target.value)} />
            </div>
            {error ? <p className="text-sm text-destructive">{error}</p> : null}
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setOpen(false)}>
              {t('ops.cancel')}
            </Button>
            <Button
              type="button"
              disabled={!name.trim() || createMutation.isPending}
              onClick={() => createMutation.mutate()}
            >
              {t('ops.save')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}

function RestorePanel({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [open, setOpen] = useState(false)
  const [notes, setNotes] = useState('')
  const [error, setError] = useState<string | null>(null)

  const listQuery = useQuery({
    queryKey: ['ops', 'restore-tests'],
    queryFn: () => opsApi.listRestoreTests({ pageSize: 50 }),
  })

  const createMutation = useMutation({
    mutationFn: () => opsApi.createRestoreTest({ notes: notes || null }),
    onSuccess: async () => {
      setOpen(false)
      setNotes('')
      setError(null)
      await qc.invalidateQueries({ queryKey: ['ops', 'restore-tests'] })
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t('ops.error.generic')),
  })

  const columns = useMemo<ColumnDef<RestoreTest, unknown>[]>(
    () => [
      {
        accessorKey: 'result',
        header: t('ops.columns.result'),
        cell: ({ row }) => <Badge variant="outline">{row.original.result}</Badge>,
      },
      {
        id: 'scheduled',
        header: t('ops.columns.scheduled'),
        cell: ({ row }) =>
          row.original.scheduledAtUtc ? new Date(row.original.scheduledAtUtc).toLocaleString() : '—',
      },
      {
        id: 'created',
        header: t('ops.columns.created'),
        cell: ({ row }) => new Date(row.original.createdAtUtc).toLocaleString(),
      },
      { accessorKey: 'notes', header: t('ops.columns.notes') },
    ],
    [t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-2">
        <h2 className="text-base font-medium">{t('ops.tabs.restore')}</h2>
        {canManage ? (
          <Button type="button" size="sm" onClick={() => setOpen(true)}>
            {t('ops.restore.add')}
          </Button>
        ) : null}
      </div>
      <DataTable
        columns={columns}
        data={listQuery.data?.items ?? []}
        emptyMessage={t('ops.empty')}
        isLoading={listQuery.isLoading}
      />
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('ops.restore.add')}</DialogTitle>
          </DialogHeader>
          <div className="space-y-1">
            <Label htmlFor="rt-notes">{t('ops.columns.notes')}</Label>
            <Input id="rt-notes" value={notes} onChange={(e) => setNotes(e.target.value)} />
            {error ? <p className="text-sm text-destructive">{error}</p> : null}
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setOpen(false)}>
              {t('ops.cancel')}
            </Button>
            <Button type="button" disabled={createMutation.isPending} onClick={() => createMutation.mutate()}>
              {t('ops.save')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}

function CertificatesPanel({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [open, setOpen] = useState(false)
  const [name, setName] = useState('')
  const [expires, setExpires] = useState('')
  const [error, setError] = useState<string | null>(null)

  const listQuery = useQuery({
    queryKey: ['ops', 'certificates'],
    queryFn: () => opsApi.listCertificates({ pageSize: 50 }),
  })

  const createMutation = useMutation({
    mutationFn: () =>
      opsApi.createCertificate({
        name,
        expiresAtUtc: new Date(expires).toISOString(),
      }),
    onSuccess: async () => {
      setOpen(false)
      setName('')
      setExpires('')
      setError(null)
      await qc.invalidateQueries({ queryKey: ['ops', 'certificates'] })
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t('ops.error.generic')),
  })

  const columns = useMemo<ColumnDef<CertificateRecord, unknown>[]>(
    () => [
      { accessorKey: 'name', header: t('ops.columns.name') },
      {
        id: 'expires',
        header: t('ops.columns.expires'),
        cell: ({ row }) => new Date(row.original.expiresAtUtc).toLocaleDateString(),
      },
      {
        accessorKey: 'daysToExpiry',
        header: t('ops.columns.daysToExpiry'),
        cell: ({ row }) => (
          <Badge variant={row.original.expired ? 'warning' : row.original.expiringSoon ? 'outline' : 'secondary'}>
            {row.original.daysToExpiry}
          </Badge>
        ),
      },
      {
        id: 'flags',
        header: t('ops.columns.status'),
        cell: ({ row }) =>
          row.original.expired
            ? t('ops.certificates.expired')
            : row.original.expiringSoon
              ? t('ops.certificates.expiringSoon')
              : t('ops.certificates.ok'),
      },
    ],
    [t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-2">
        <h2 className="text-base font-medium">{t('ops.tabs.certificates')}</h2>
        {canManage ? (
          <Button type="button" size="sm" onClick={() => setOpen(true)}>
            {t('ops.certificates.add')}
          </Button>
        ) : null}
      </div>
      <DataTable
        columns={columns}
        data={listQuery.data?.items ?? []}
        emptyMessage={t('ops.empty')}
        isLoading={listQuery.isLoading}
      />
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('ops.certificates.add')}</DialogTitle>
          </DialogHeader>
          <div className="space-y-3">
            <div className="space-y-1">
              <Label htmlFor="cert-name">{t('ops.columns.name')}</Label>
              <Input id="cert-name" value={name} onChange={(e) => setName(e.target.value)} />
            </div>
            <div className="space-y-1">
              <Label htmlFor="cert-exp">{t('ops.columns.expires')}</Label>
              <Input
                id="cert-exp"
                type="datetime-local"
                value={expires}
                onChange={(e) => setExpires(e.target.value)}
              />
            </div>
            {error ? <p className="text-sm text-destructive">{error}</p> : null}
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setOpen(false)}>
              {t('ops.cancel')}
            </Button>
            <Button
              type="button"
              disabled={!name.trim() || !expires || createMutation.isPending}
              onClick={() => createMutation.mutate()}
            >
              {t('ops.save')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}

function PatchesPanel({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [open, setOpen] = useState(false)
  const [name, setName] = useState('')
  const [version, setVersion] = useState('')
  const [error, setError] = useState<string | null>(null)

  const baselinesQuery = useQuery({
    queryKey: ['ops', 'patch-baselines'],
    queryFn: () => opsApi.listPatchBaselines({ pageSize: 50 }),
  })
  const deploymentsQuery = useQuery({
    queryKey: ['ops', 'patch-deployments'],
    queryFn: () => opsApi.listPatchDeployments({ pageSize: 50 }),
  })

  const createMutation = useMutation({
    mutationFn: () => opsApi.createPatchBaseline({ name, version: version || null }),
    onSuccess: async () => {
      setOpen(false)
      setName('')
      setVersion('')
      setError(null)
      await qc.invalidateQueries({ queryKey: ['ops', 'patch-baselines'] })
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t('ops.error.generic')),
  })

  const baselineColumns = useMemo<ColumnDef<PatchBaseline, unknown>[]>(
    () => [
      { accessorKey: 'name', header: t('ops.columns.name') },
      { accessorKey: 'version', header: t('ops.columns.version') },
      {
        accessorKey: 'isActive',
        header: t('ops.columns.active'),
        cell: ({ row }) => (row.original.isActive ? t('ops.yes') : t('ops.no')),
      },
    ],
    [t],
  )

  const deploymentColumns = useMemo<ColumnDef<PatchDeployment, unknown>[]>(
    () => [
      { accessorKey: 'configurationItemId', header: t('ops.columns.ci') },
      {
        accessorKey: 'status',
        header: t('ops.columns.status'),
        cell: ({ row }) => <Badge variant="outline">{row.original.status}</Badge>,
      },
      {
        id: 'created',
        header: t('ops.columns.created'),
        cell: ({ row }) => new Date(row.original.createdAtUtc).toLocaleString(),
      },
    ],
    [t],
  )

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between gap-2">
        <h2 className="text-base font-medium">{t('ops.patches.baselines')}</h2>
        {canManage ? (
          <Button type="button" size="sm" onClick={() => setOpen(true)}>
            {t('ops.patches.addBaseline')}
          </Button>
        ) : null}
      </div>
      <DataTable
        columns={baselineColumns}
        data={baselinesQuery.data?.items ?? []}
        emptyMessage={t('ops.empty')}
        isLoading={baselinesQuery.isLoading}
      />
      <h2 className="text-base font-medium">{t('ops.patches.deployments')}</h2>
      <DataTable
        columns={deploymentColumns}
        data={deploymentsQuery.data?.items ?? []}
        emptyMessage={t('ops.empty')}
        isLoading={deploymentsQuery.isLoading}
      />
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('ops.patches.addBaseline')}</DialogTitle>
          </DialogHeader>
          <div className="space-y-3">
            <div className="space-y-1">
              <Label htmlFor="pb-name">{t('ops.columns.name')}</Label>
              <Input id="pb-name" value={name} onChange={(e) => setName(e.target.value)} />
            </div>
            <div className="space-y-1">
              <Label htmlFor="pb-ver">{t('ops.columns.version')}</Label>
              <Input id="pb-ver" value={version} onChange={(e) => setVersion(e.target.value)} />
            </div>
            {error ? <p className="text-sm text-destructive">{error}</p> : null}
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setOpen(false)}>
              {t('ops.cancel')}
            </Button>
            <Button
              type="button"
              disabled={!name.trim() || createMutation.isPending}
              onClick={() => createMutation.mutate()}
            >
              {t('ops.save')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}

function JobsPanel({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [open, setOpen] = useState(false)
  const [name, setName] = useState('')
  const [schedule, setSchedule] = useState('')
  const [error, setError] = useState<string | null>(null)

  const listQuery = useQuery({
    queryKey: ['ops', 'jobs'],
    queryFn: () => opsApi.listJobs({ pageSize: 50 }),
  })

  const createMutation = useMutation({
    mutationFn: () => opsApi.createJob({ name, scheduleDescription: schedule || null }),
    onSuccess: async () => {
      setOpen(false)
      setName('')
      setSchedule('')
      setError(null)
      await qc.invalidateQueries({ queryKey: ['ops', 'jobs'] })
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t('ops.error.generic')),
  })

  const columns = useMemo<ColumnDef<ScheduledJob, unknown>[]>(
    () => [
      { accessorKey: 'name', header: t('ops.columns.name') },
      { accessorKey: 'scheduleDescription', header: t('ops.columns.schedule') },
      {
        accessorKey: 'lastResult',
        header: t('ops.columns.lastResult'),
        cell: ({ row }) => <Badge variant="outline">{row.original.lastResult}</Badge>,
      },
      {
        id: 'next',
        header: t('ops.columns.nextRun'),
        cell: ({ row }) =>
          row.original.nextRunAtUtc ? new Date(row.original.nextRunAtUtc).toLocaleString() : '—',
      },
    ],
    [t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-2">
        <h2 className="text-base font-medium">{t('ops.tabs.jobs')}</h2>
        {canManage ? (
          <Button type="button" size="sm" onClick={() => setOpen(true)}>
            {t('ops.jobs.add')}
          </Button>
        ) : null}
      </div>
      <DataTable
        columns={columns}
        data={listQuery.data?.items ?? []}
        emptyMessage={t('ops.empty')}
        isLoading={listQuery.isLoading}
      />
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('ops.jobs.add')}</DialogTitle>
          </DialogHeader>
          <div className="space-y-3">
            <div className="space-y-1">
              <Label htmlFor="job-name">{t('ops.columns.name')}</Label>
              <Input id="job-name" value={name} onChange={(e) => setName(e.target.value)} />
            </div>
            <div className="space-y-1">
              <Label htmlFor="job-sched">{t('ops.columns.schedule')}</Label>
              <Input id="job-sched" value={schedule} onChange={(e) => setSchedule(e.target.value)} />
            </div>
            {error ? <p className="text-sm text-destructive">{error}</p> : null}
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setOpen(false)}>
              {t('ops.cancel')}
            </Button>
            <Button
              type="button"
              disabled={!name.trim() || createMutation.isPending}
              onClick={() => createMutation.mutate()}
            >
              {t('ops.save')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
