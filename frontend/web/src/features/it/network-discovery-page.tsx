import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Plus, Radar } from 'lucide-react'
import {
  ApiError,
  cmdbApi,
  type NetworkDiscoveryObservation,
  type NetworkDiscoveryProfile,
  type NetworkDiscoveryRun,
} from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { cmdbKeys } from '@/features/it/query-keys'

export function NetworkDiscoveryPage() {
  const { t } = useTranslation()
  const { can } = useAuth()
  const queryClient = useQueryClient()
  const canManage = can('cmdb.discovery.manage')

  const [profileOpen, setProfileOpen] = useState(false)
  const [profileName, setProfileName] = useState('')
  const [profileCidr, setProfileCidr] = useState('')
  const [activeRunId, setActiveRunId] = useState<string | null>(null)
  const [review, setReview] = useState<NetworkDiscoveryObservation | null>(null)
  const [reviewMode, setReviewMode] = useState<'match' | 'create' | 'accept' | null>(null)
  const [matchCiId, setMatchCiId] = useState('')
  const [createName, setCreateName] = useState('')
  const [createTypeId, setCreateTypeId] = useState('')
  const [formError, setFormError] = useState<string | null>(null)

  const profilesQuery = useQuery({
    queryKey: cmdbKeys.discoveryProfiles(),
    queryFn: () => cmdbApi.listDiscoveryProfiles(),
  })

  const runQuery = useQuery({
    queryKey: cmdbKeys.discoveryRun(activeRunId ?? ''),
    queryFn: () => cmdbApi.getDiscoveryRun(activeRunId!),
    enabled: Boolean(activeRunId),
    refetchInterval: (query) => {
      const status = query.state.data?.status
      return status === 'Running' || status === 'Pending' ? 1500 : false
    },
  })

  const observationsQuery = useQuery({
    queryKey: cmdbKeys.discoveryObservations(activeRunId ?? ''),
    queryFn: () => cmdbApi.listDiscoveryObservations(activeRunId!),
    enabled: Boolean(activeRunId) && runQuery.data?.status === 'Completed',
  })

  const typesQuery = useQuery({
    queryKey: cmdbKeys.types(),
    queryFn: () => cmdbApi.listCiTypes(),
    enabled: reviewMode === 'create',
  })

  const cisQuery = useQuery({
    queryKey: cmdbKeys.cis('discovery-match'),
    queryFn: () => cmdbApi.listCis(),
    enabled: reviewMode === 'match',
  })

  useEffect(() => {
    if (runQuery.data?.status === 'Completed' || runQuery.data?.status === 'Failed') {
      void queryClient.invalidateQueries({
        queryKey: cmdbKeys.discoveryObservations(activeRunId ?? ''),
      })
    }
  }, [runQuery.data?.status, activeRunId, queryClient])

  const createProfile = useMutation({
    mutationFn: () =>
      cmdbApi.createDiscoveryProfile({
        name: profileName.trim(),
        cidr: profileCidr.trim(),
        timeoutMs: 1000,
        maxConcurrency: 32,
        isActive: true,
      }),
    onSuccess: () => {
      setProfileOpen(false)
      setProfileName('')
      setProfileCidr('')
      setFormError(null)
      void queryClient.invalidateQueries({ queryKey: cmdbKeys.discoveryProfiles() })
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('cmdb.error.generic'))
    },
  })

  const scanMutation = useMutation({
    mutationFn: (profileId: string) => cmdbApi.startDiscoveryScan(profileId),
    onSuccess: (run: NetworkDiscoveryRun) => {
      setActiveRunId(run.id)
      void queryClient.invalidateQueries({ queryKey: cmdbKeys.discoveryRun(run.id) })
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('cmdb.error.generic'))
    },
  })

  const matchMutation = useMutation({
    mutationFn: () =>
      cmdbApi.matchDiscoveryObservation(review!.id, {
        configurationItemId: matchCiId,
        addIdentityIfMissing: true,
      }),
    onSuccess: () => {
      setReview(null)
      setReviewMode(null)
      void queryClient.invalidateQueries({ queryKey: cmdbKeys.discoveryObservations(activeRunId ?? '') })
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('cmdb.error.generic'))
    },
  })

  const createCiMutation = useMutation({
    mutationFn: () =>
      cmdbApi.createCiFromDiscoveryObservation(review!.id, {
        ciTypeId: createTypeId,
        name: createName.trim(),
      }),
    onSuccess: () => {
      setReview(null)
      setReviewMode(null)
      void queryClient.invalidateQueries({ queryKey: cmdbKeys.discoveryObservations(activeRunId ?? '') })
      void queryClient.invalidateQueries({ queryKey: cmdbKeys.all })
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('cmdb.error.generic'))
    },
  })

  const acceptMutation = useMutation({
    mutationFn: () => cmdbApi.acceptDiscoveryChanges(review!.id),
    onSuccess: () => {
      setReview(null)
      setReviewMode(null)
      void queryClient.invalidateQueries({ queryKey: cmdbKeys.discoveryObservations(activeRunId ?? '') })
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('cmdb.error.generic'))
    },
  })

  const ignoreMutation = useMutation({
    mutationFn: (id: string) => cmdbApi.ignoreDiscoveryObservation(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: cmdbKeys.discoveryObservations(activeRunId ?? '') })
    },
  })

  const summary = useMemo(() => {
    const items = observationsQuery.data ?? []
    return {
      matched: items.filter((i) => i.matchStatus === 'Matched').length,
      possible: items.filter((i) => i.matchStatus === 'PossibleMatch').length,
      createdNew: items.filter((i) => i.matchStatus === 'New').length,
      changed: items.filter((i) => i.matchStatus === 'Changed').length,
    }
  }, [observationsQuery.data])

  const run = runQuery.data

  return (
    <div className="space-y-4">
      <PageHeader
        title={t('cmdb.discovery.title')}
        description={t('cmdb.discovery.description')}
        actions={
          canManage ? (
            <Button
              type="button"
              onClick={() => {
                setFormError(null)
                setProfileOpen(true)
              }}
            >
              <Plus className="me-2 h-4 w-4" />
              {t('cmdb.discovery.addProfile')}
            </Button>
          ) : undefined
        }
      />

      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
        {(profilesQuery.data ?? []).length === 0 ? (
          <Card className="md:col-span-2 xl:col-span-3">
            <CardContent className="py-10 text-center text-sm text-muted-foreground">
              {t('cmdb.discovery.emptyProfiles')}
            </CardContent>
          </Card>
        ) : (
          (profilesQuery.data ?? []).map((profile: NetworkDiscoveryProfile) => (
            <Card key={profile.id}>
              <CardHeader className="pb-2">
                <CardTitle className="text-base">{profile.name}</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                <div className="text-sm text-muted-foreground">{profile.cidr}</div>
                <div className="flex items-center justify-between gap-2">
                  <Badge variant={profile.isActive ? 'default' : 'secondary'}>
                    {profile.isActive ? t('cmdb.discovery.active') : t('cmdb.discovery.inactive')}
                  </Badge>
                  {canManage ? (
                    <Button
                      type="button"
                      size="sm"
                      disabled={!profile.isActive || scanMutation.isPending}
                      onClick={() => scanMutation.mutate(profile.id)}
                    >
                      <Radar className="me-2 h-4 w-4" />
                      {t('cmdb.discovery.scanNow')}
                    </Button>
                  ) : null}
                </div>
              </CardContent>
            </Card>
          ))
        )}
      </div>

      {run ? (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t('cmdb.discovery.scanStatus')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2 text-sm">
            <div>
              {t('cmdb.discovery.status')}: <strong>{run.status}</strong>
            </div>
            {(run.status === 'Running' || run.status === 'Pending') && (
              <div>
                {t('cmdb.discovery.scanningProgress', {
                  scanned: run.addressesScanned,
                  total: run.expectedAddressCount || '—',
                  responsive: run.responsiveHosts,
                })}
              </div>
            )}
            {run.status === 'Completed' && (
              <div className="flex flex-wrap gap-3">
                <span>
                  {run.addressesScanned} {t('cmdb.discovery.scanned')}
                </span>
                <span>
                  {run.responsiveHosts} {t('cmdb.discovery.responsiveHosts')}
                </span>
                <span>
                  {summary.matched} {t('cmdb.discovery.matched')}
                </span>
                <span>
                  {summary.possible} {t('cmdb.discovery.possibleMatch')}
                </span>
                <span>
                  {summary.createdNew} {t('cmdb.discovery.newDevice')}
                </span>
                <span>
                  {summary.changed} {t('cmdb.discovery.changed')}
                </span>
              </div>
            )}
            {run.errorSummary ? <p className="text-destructive">{run.errorSummary}</p> : null}
          </CardContent>
        </Card>
      ) : null}

      {observationsQuery.data ? (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t('cmdb.discovery.discoveredDevices')}</CardTitle>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>IP</TableHead>
                  <TableHead>{t('cmdb.topology.hostname')}</TableHead>
                  <TableHead>{t('cmdb.discovery.response')}</TableHead>
                  <TableHead>{t('cmdb.discovery.match')}</TableHead>
                  <TableHead>{t('cmdb.discovery.matchedCi')}</TableHead>
                  <TableHead>{t('cmdb.columns.status')}</TableHead>
                  <TableHead>{t('cmdb.discovery.actions')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {observationsQuery.data.map((item) => (
                  <TableRow key={item.id}>
                    <TableCell>{item.ipAddress}</TableCell>
                    <TableCell>{item.hostname ?? '—'}</TableCell>
                    <TableCell>{item.responseMs != null ? `${item.responseMs} ms` : '—'}</TableCell>
                    <TableCell>{item.matchStatus}</TableCell>
                    <TableCell>{item.matchedCiName ?? '—'}</TableCell>
                    <TableCell>{item.reviewStatus}</TableCell>
                    <TableCell>
                      {canManage && item.reviewStatus === 'Pending' ? (
                        <div className="flex flex-wrap gap-1">
                          <Button
                            type="button"
                            size="sm"
                            variant="outline"
                            onClick={() => {
                              setReview(item)
                              setReviewMode('match')
                              setMatchCiId(item.matchedConfigurationItemId ?? '')
                              setFormError(null)
                            }}
                          >
                            {t('cmdb.discovery.matchExisting')}
                          </Button>
                          <Button
                            type="button"
                            size="sm"
                            variant="outline"
                            onClick={() => {
                              setReview(item)
                              setReviewMode('create')
                              setCreateName(item.hostname ?? item.ipAddress)
                              setCreateTypeId('')
                              setFormError(null)
                            }}
                          >
                            {t('cmdb.discovery.createCi')}
                          </Button>
                          {item.matchStatus === 'Changed' || item.matchedConfigurationItemId ? (
                            <Button
                              type="button"
                              size="sm"
                              variant="outline"
                              onClick={() => {
                                setReview(item)
                                setReviewMode('accept')
                                setFormError(null)
                              }}
                            >
                              {t('cmdb.discovery.acceptChanges')}
                            </Button>
                          ) : null}
                          <Button
                            type="button"
                            size="sm"
                            variant="ghost"
                            onClick={() => ignoreMutation.mutate(item.id)}
                          >
                            {t('cmdb.discovery.ignore')}
                          </Button>
                        </div>
                      ) : (
                        '—'
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      ) : null}

      <Dialog open={profileOpen} onOpenChange={setProfileOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('cmdb.discovery.addProfile')}</DialogTitle>
            <DialogDescription>{t('cmdb.discovery.profileHelp')}</DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <div>
              <Label>{t('cmdb.fields.name')}</Label>
              <Input value={profileName} onChange={(e) => setProfileName(e.target.value)} />
            </div>
            <div>
              <Label>{t('cmdb.discovery.cidr')}</Label>
              <Input
                value={profileCidr}
                onChange={(e) => setProfileCidr(e.target.value)}
                placeholder="192.168.1.0/24"
              />
            </div>
            {formError ? <p className="text-sm text-destructive">{formError}</p> : null}
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setProfileOpen(false)}>
              {t('cmdb.discovery.cancel')}
            </Button>
            <Button
              type="button"
              disabled={!profileName.trim() || !profileCidr.trim() || createProfile.isPending}
              onClick={() => createProfile.mutate()}
            >
              {t('cmdb.discovery.save')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(review) && reviewMode != null}
        onOpenChange={(open) => {
          if (!open) {
            setReview(null)
            setReviewMode(null)
          }
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {reviewMode === 'match'
                ? t('cmdb.discovery.matchExisting')
                : reviewMode === 'create'
                  ? t('cmdb.discovery.createCi')
                  : t('cmdb.discovery.acceptChanges')}
            </DialogTitle>
            <DialogDescription>
              {review?.ipAddress}
              {review?.hostname ? ` · ${review.hostname}` : ''}
            </DialogDescription>
          </DialogHeader>
          {reviewMode === 'match' ? (
            <div className="space-y-3">
              <Label>{t('cmdb.relationships.target')}</Label>
              <Select value={matchCiId} onValueChange={setMatchCiId}>
                <SelectTrigger>
                  <SelectValue placeholder={t('cmdb.relationships.targetPlaceholder')} />
                </SelectTrigger>
                <SelectContent>
                  {(cisQuery.data ?? []).map((ci) => (
                    <SelectItem key={ci.id} value={ci.id}>
                      {ci.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          ) : null}
          {reviewMode === 'create' ? (
            <div className="space-y-3">
              <div>
                <Label>{t('cmdb.fields.name')}</Label>
                <Input value={createName} onChange={(e) => setCreateName(e.target.value)} />
              </div>
              <div>
                <Label>{t('cmdb.fields.type')}</Label>
                <Select value={createTypeId} onValueChange={setCreateTypeId}>
                  <SelectTrigger>
                    <SelectValue placeholder={t('cmdb.fields.typePlaceholder')} />
                  </SelectTrigger>
                  <SelectContent>
                    {(typesQuery.data ?? []).map((type) => (
                      <SelectItem key={type.id} value={type.id}>
                        {type.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
          ) : null}
          {reviewMode === 'accept' ? (
            <p className="text-sm text-muted-foreground">{t('cmdb.discovery.acceptHelp')}</p>
          ) : null}
          {formError ? <p className="text-sm text-destructive">{formError}</p> : null}
          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              onClick={() => {
                setReview(null)
                setReviewMode(null)
              }}
            >
              {t('cmdb.discovery.cancel')}
            </Button>
            {reviewMode === 'match' ? (
              <Button type="button" disabled={!matchCiId || matchMutation.isPending} onClick={() => matchMutation.mutate()}>
                {t('cmdb.discovery.confirmMatch')}
              </Button>
            ) : null}
            {reviewMode === 'create' ? (
              <Button
                type="button"
                disabled={!createName.trim() || !createTypeId || createCiMutation.isPending}
                onClick={() => createCiMutation.mutate()}
              >
                {t('cmdb.discovery.createCi')}
              </Button>
            ) : null}
            {reviewMode === 'accept' ? (
              <Button type="button" disabled={acceptMutation.isPending} onClick={() => acceptMutation.mutate()}>
                {t('cmdb.discovery.acceptChanges')}
              </Button>
            ) : null}
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
