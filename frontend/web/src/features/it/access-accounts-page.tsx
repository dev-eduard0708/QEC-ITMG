import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'
import { ApiError, accessApi, type ManagedAccount } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { DataTable } from '@/components/shared/data-table'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'

export function AccessAccountsPage() {
  const { t } = useTranslation()
  const { user } = useAuth()
  const qc = useQueryClient()
  const [accountName, setAccountName] = useState('')
  const [type, setType] = useState('Privileged')
  const [purpose, setPurpose] = useState('')
  const [error, setError] = useState<string | null>(null)

  const listQuery = useQuery({
    queryKey: ['access', 'accounts'],
    queryFn: () => accessApi.listAccounts({ pageSize: 50 }),
  })

  const createMutation = useMutation({
    mutationFn: () =>
      accessApi.createAccount({
        accountName,
        type,
        purpose,
        ownerUserId: type === 'Service' ? user?.id ?? null : null,
      }),
    onSuccess: async () => {
      setAccountName('')
      setPurpose('')
      setError(null)
      await qc.invalidateQueries({ queryKey: ['access', 'accounts'] })
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t('access.error.generic')),
  })

  const columns = useMemo<ColumnDef<ManagedAccount, unknown>[]>(
    () => [
      { accessorKey: 'accountName', header: t('access.columns.name') },
      {
        accessorKey: 'type',
        header: t('access.columns.type'),
        cell: ({ row }) => (
          <Badge variant={row.original.isPrivileged ? 'warning' : 'outline'}>{row.original.type}</Badge>
        ),
      },
      { accessorKey: 'purpose', header: t('access.columns.purpose') },
      { accessorKey: 'status', header: t('access.columns.status') },
    ],
    [t],
  )

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('access.accountsTitle')}
        description={t('access.accountsDescription')}
        actions={
          <Button asChild variant="secondary">
            <Link to="/it/access">{t('access.back')}</Link>
          </Button>
        }
      />
      <DataTable
        columns={columns}
        data={listQuery.data?.items ?? []}
        emptyMessage={t('access.empty')}
        isLoading={listQuery.isLoading}
      />
      <div className="grid max-w-xl gap-3">
        <h2 className="text-base font-medium">{t('access.accountsCreate')}</h2>
        <div className="space-y-1">
          <Label>{t('access.columns.name')}</Label>
          <Input value={accountName} onChange={(e) => setAccountName(e.target.value)} />
        </div>
        <div className="space-y-1">
          <Label>{t('access.columns.type')}</Label>
          <Select value={type} onValueChange={setType}>
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="Privileged">Privileged</SelectItem>
              <SelectItem value="Service">Service</SelectItem>
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-1">
          <Label>{t('access.columns.purpose')}</Label>
          <Input value={purpose} onChange={(e) => setPurpose(e.target.value)} />
        </div>
        {error ? <p className="text-sm text-destructive">{error}</p> : null}
        <Button
          type="button"
          disabled={!accountName.trim() || !purpose.trim() || createMutation.isPending}
          onClick={() => createMutation.mutate()}
        >
          {t('access.save')}
        </Button>
      </div>
    </div>
  )
}
