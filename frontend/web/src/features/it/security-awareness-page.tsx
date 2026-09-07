import { useMemo, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Plus } from 'lucide-react'
import {
  ApiError,
  awarenessV1Api,
  type AwarenessV1CampaignListItem,
} from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { cn } from '@/lib/utils'
import { isAppLanguage } from '@/i18n'

type TabKey = 'dashboard' | 'campaigns'
type StatusFilter = 'All' | 'Active' | 'Draft' | 'Closed' | 'Archived'

export function SecurityAwarenessPage() {
  const { t, i18n } = useTranslation()
  const { can } = useAuth()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [searchParams, setSearchParams] = useSearchParams()
  const tab: TabKey = searchParams.get('tab') === 'campaigns' ? 'campaigns' : 'dashboard'
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('All')
  const [createOpen, setCreateOpen] = useState(false)
  const [titleEn, setTitleEn] = useState('')
  const [createError, setCreateError] = useState<string | null>(null)

  const canManage = can('sec.awareness.manage')
  const canRead = can('sec.awareness.read') || canManage

  const dashQuery = useQuery({
    queryKey: ['security', 'awareness-v1', 'dashboard'],
    queryFn: () => awarenessV1Api.dashboard(),
    enabled: canRead,
  })

  const listQuery = useQuery({
    queryKey: ['security', 'awareness-v1', 'campaigns', statusFilter],
    queryFn: () =>
      awarenessV1Api.listCampaigns(statusFilter === 'All' ? undefined : statusFilter),
    enabled: canRead && tab === 'campaigns',
  })

  const createMutation = useMutation({
    mutationFn: () => awarenessV1Api.createDraft({ titleEn: titleEn.trim() }),
    onSuccess: async (created) => {
      setCreateOpen(false)
      setTitleEn('')
      await qc.invalidateQueries({ queryKey: ['security', 'awareness-v1'] })
      navigate(`/it/security/awareness/campaigns/${created.id}`)
    },
    onError: (err) => {
      setCreateError(err instanceof ApiError ? err.message : t('awarenessAdmin.error.generic'))
    },
  })

  const campaigns = useMemo(() => {
    if (tab === 'dashboard') return dashQuery.data?.campaigns ?? []
    return listQuery.data ?? []
  }, [tab, dashQuery.data?.campaigns, listQuery.data])

  const filteredCampaigns = useMemo(() => {
    if (tab === 'campaigns') return campaigns
    if (statusFilter === 'All') return campaigns
    return campaigns.filter((c) => c.status === statusFilter)
  }, [campaigns, statusFilter, tab])

  if (!canRead) {
    return <p className="text-sm text-muted-foreground">{t('awarenessAdmin.noPermission')}</p>
  }

  const lang = isAppLanguage(i18n.language) ? i18n.language : 'en'

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('awarenessAdmin.title')}
        description={t('awarenessAdmin.description')}
        actions={
          canManage ? (
            <Button type="button" onClick={() => setCreateOpen(true)}>
              <Plus className="me-2 h-4 w-4" aria-hidden />
              {t('awarenessAdmin.newCampaign')}
            </Button>
          ) : null
        }
      />

      <div className="flex flex-wrap gap-2">
        {(
          [
            ['dashboard', 'awarenessAdmin.tabs.dashboard'],
            ['campaigns', 'awarenessAdmin.tabs.campaigns'],
          ] as const
        ).map(([key, label]) => (
          <Button
            key={key}
            type="button"
            size="sm"
            variant={tab === key ? 'default' : 'outline'}
            onClick={() => setSearchParams(key === 'dashboard' ? {} : { tab: key })}
          >
            {t(label)}
          </Button>
        ))}
      </div>

      {tab === 'dashboard' && dashQuery.data ? (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6">
          {(
            [
              [t('awarenessAdmin.metrics.active'), dashQuery.data.activeCampaigns],
              [t('awarenessAdmin.metrics.assigned'), dashQuery.data.assignedEmployees],
              [t('awarenessAdmin.metrics.completed'), dashQuery.data.completed],
              [t('awarenessAdmin.metrics.overdue'), dashQuery.data.overdue],
              [t('awarenessAdmin.metrics.notStarted'), dashQuery.data.notStarted],
              [
                t('awarenessAdmin.metrics.completionRate'),
                `${dashQuery.data.completionRatePercent}%`,
              ],
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
      ) : null}

      <div className="flex flex-wrap gap-2">
        {(['All', 'Active', 'Draft', 'Closed', 'Archived'] as StatusFilter[]).map((key) => (
          <button
            key={key}
            type="button"
            onClick={() => setStatusFilter(key)}
            className={cn(
              'rounded-full border px-3 py-1.5 text-sm transition-colors',
              statusFilter === key
                ? 'border-primary bg-primary text-primary-foreground'
                : 'border-border bg-card text-muted-foreground hover:bg-muted/40',
            )}
          >
            {t(`awarenessAdmin.filter.${key.toLowerCase()}`)}
          </button>
        ))}
      </div>

      <div className="overflow-x-auto rounded-xl border">
        <table className="w-full min-w-[720px] text-sm">
          <thead className="border-b bg-muted/40 text-start">
            <tr>
              <th className="px-3 py-2 font-medium">{t('awarenessAdmin.columns.campaign')}</th>
              <th className="px-3 py-2 font-medium">{t('awarenessAdmin.columns.status')}</th>
              <th className="px-3 py-2 font-medium">{t('awarenessAdmin.columns.audience')}</th>
              <th className="px-3 py-2 font-medium">{t('awarenessAdmin.columns.assigned')}</th>
              <th className="px-3 py-2 font-medium">{t('awarenessAdmin.columns.completion')}</th>
              <th className="px-3 py-2 font-medium">{t('awarenessAdmin.columns.overdue')}</th>
              <th className="px-3 py-2 font-medium">{t('awarenessAdmin.columns.due')}</th>
            </tr>
          </thead>
          <tbody>
            {filteredCampaigns.map((c) => (
              <CampaignRow key={c.id} campaign={c} lang={lang} />
            ))}
            {filteredCampaigns.length === 0 ? (
              <tr>
                <td colSpan={7} className="px-3 py-8 text-center text-muted-foreground">
                  {t('awarenessAdmin.empty')}
                </td>
              </tr>
            ) : null}
          </tbody>
        </table>
      </div>

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('awarenessAdmin.newCampaign')}</DialogTitle>
          </DialogHeader>
          <div className="space-y-3">
            <div className="space-y-1.5">
              <Label htmlFor="awareness-title-en">{t('awarenessAdmin.fields.titleEn')}</Label>
              <Input
                id="awareness-title-en"
                value={titleEn}
                onChange={(e) => setTitleEn(e.target.value)}
                placeholder={t('awarenessAdmin.fields.titleEnPlaceholder')}
              />
            </div>
            {createError ? <p className="text-sm text-destructive">{createError}</p> : null}
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setCreateOpen(false)}>
              {t('awarenessAdmin.cancel')}
            </Button>
            <Button
              type="button"
              disabled={!titleEn.trim() || createMutation.isPending}
              onClick={() => {
                setCreateError(null)
                createMutation.mutate()
              }}
            >
              {t('awarenessAdmin.createDraft')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}

function CampaignRow({
  campaign,
  lang,
}: {
  campaign: AwarenessV1CampaignListItem
  lang: 'en' | 'ar'
}) {
  const { t } = useTranslation()
  const title =
    lang === 'ar' && campaign.titleAr?.trim() ? campaign.titleAr : campaign.titleEn

  return (
    <tr className="border-b last:border-0 hover:bg-muted/20">
      <td className="px-3 py-2">
        <Link
          to={`/it/security/awareness/campaigns/${campaign.id}`}
          className="font-medium text-primary underline-offset-2 hover:underline"
        >
          {title}
        </Link>
        {campaign.number ? (
          <div className="text-xs text-muted-foreground">{campaign.number}</div>
        ) : null}
      </td>
      <td className="px-3 py-2">
        <Badge variant={statusVariant(campaign.status)}>{campaign.status}</Badge>
      </td>
      <td className="px-3 py-2 text-muted-foreground">{campaign.audienceSummary}</td>
      <td className="px-3 py-2 tabular-nums">
        {campaign.completedCount} / {campaign.assignedCount}
      </td>
      <td className="px-3 py-2 tabular-nums">{campaign.completionRatePercent}%</td>
      <td className="px-3 py-2 tabular-nums">
        {campaign.overdueCount > 0 ? (
          <span className="text-amber-700 dark:text-amber-400">{campaign.overdueCount}</span>
        ) : (
          0
        )}
      </td>
      <td className="px-3 py-2 text-muted-foreground">
        {campaign.dueAtUtc
          ? new Date(campaign.dueAtUtc).toLocaleDateString()
          : t('awarenessAdmin.noDue')}
      </td>
    </tr>
  )
}

function statusVariant(status: string): 'default' | 'secondary' | 'outline' | 'warning' {
  if (status === 'Active') return 'default'
  if (status === 'Draft') return 'outline'
  if (status === 'Closed') return 'secondary'
  return 'secondary'
}
