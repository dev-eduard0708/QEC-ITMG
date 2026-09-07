import {
  Background,
  Controls,
  MiniMap,
  ReactFlow,
  ReactFlowProvider,
  useEdgesState,
  useNodesState,
  type Edge,
  type Node,
  type NodeMouseHandler,
  type OnNodeDrag,
} from '@xyflow/react'
import '@xyflow/react/dist/style.css'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Cable, LayoutGrid, Search } from 'lucide-react'
import {
  ApiError,
  cmdbApi,
  type TopologyEdge,
  type TopologyNode,
} from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
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
import { Sheet, SheetContent } from '@/components/ui/sheet'
import { cmdbKeys } from '@/features/it/query-keys'
import { cn } from '@/lib/utils'

function nodeTone(typeKey: string, name: string): string {
  const key = typeKey.toLowerCase()
  const lower = name.toLowerCase()
  if (key === 'network-link' || lower.includes('internet') || lower.includes('zain')) {
    return 'border-sky-600 bg-sky-50 text-sky-950 dark:bg-sky-950/40 dark:text-sky-100'
  }
  if (key === 'firewall' || lower.includes('firewall') || lower.includes('sonicwall')) {
    return 'border-rose-700 bg-rose-50 text-rose-950 dark:bg-rose-950/40 dark:text-rose-100'
  }
  if (lower.includes('core')) {
    return 'border-amber-700 bg-amber-50 text-amber-950 dark:bg-amber-950/40 dark:text-amber-100'
  }
  if (key === 'network-device' || lower.includes('switch')) {
    return 'border-emerald-700 bg-emerald-50 text-emerald-950 dark:bg-emerald-950/40 dark:text-emerald-100'
  }
  return 'border-slate-500 bg-slate-50 text-slate-900 dark:bg-slate-900 dark:text-slate-100'
}

function toFlowNodes(items: TopologyNode[], missingLayout: TopologyNode[]): Node[] {
  const columns = Math.max(3, Math.ceil(Math.sqrt(Math.max(items.length, 1))))
  return items.map((item, index) => {
    const hasLayout = item.positionX != null && item.positionY != null
    const fallbackIndex = missingLayout.findIndex((n) => n.configurationItemId === item.configurationItemId)
    const col = (hasLayout ? 0 : fallbackIndex >= 0 ? fallbackIndex : index) % columns
    const row = Math.floor((hasLayout ? 0 : fallbackIndex >= 0 ? fallbackIndex : index) / columns)
    return {
      id: item.configurationItemId,
      position: {
        x: hasLayout ? item.positionX! : 40 + col * 220,
        y: hasLayout ? item.positionY! : 40 + row * 120,
      },
      data: { item },
      type: 'default',
      className: cn('rounded-md border-2 px-3 py-2 text-xs shadow-sm min-w-[160px]', nodeTone(item.ciTypeKey, item.name)),
      style: { width: 180 },
    }
  })
}

function TopologyNodeLabel({ item }: { item: TopologyNode }) {
  const primaryIp = item.identities.find((i) => i.isPrimary)?.ipAddress ?? item.identities[0]?.ipAddress
  return (
    <div className="space-y-0.5 text-start">
      <div className="font-semibold leading-tight">{item.name}</div>
      <div className="opacity-80">{item.manufacturer ?? '—'} {item.model ?? ''}</div>
      <div className="opacity-70">SN: {item.serialNumber ?? '—'}</div>
      <div className="opacity-70">IP: {primaryIp ?? '—'}</div>
      <div className="opacity-70">{item.status}</div>
    </div>
  )
}

function NetworkTopologyCanvas() {
  const { t } = useTranslation()
  const { can } = useAuth()
  const queryClient = useQueryClient()
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [typeFilter, setTypeFilter] = useState<string>('all')
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [connectOpen, setConnectOpen] = useState(false)
  const [targetCiId, setTargetCiId] = useState('')
  const [fromPort, setFromPort] = useState('')
  const [toPort, setToPort] = useState('')
  const [mediaType, setMediaType] = useState('Ethernet')
  const [linkMode, setLinkMode] = useState('Access')
  const [notes, setNotes] = useState('')
  const [formError, setFormError] = useState<string | null>(null)

  const filtersKey = `${typeFilter}|${search}`
  const graphQuery = useQuery({
    queryKey: cmdbKeys.topologyGraph(filtersKey),
    queryFn: () =>
      cmdbApi.getTopologyGraph({
        type: typeFilter === 'all' ? undefined : typeFilter,
        search: search || undefined,
      }),
  })

  const typesQuery = useQuery({
    queryKey: cmdbKeys.types(),
    queryFn: () => cmdbApi.listCiTypes(),
  })

  const cisQuery = useQuery({
    queryKey: cmdbKeys.cis('topology-connect'),
    queryFn: () => cmdbApi.listCis(),
    enabled: connectOpen,
  })

  const graph = graphQuery.data
  const layoutMissing = useMemo(
    () => (graph?.nodes ?? []).filter((n) => n.positionX == null || n.positionY == null),
    [graph?.nodes],
  )

  const initialNodes = useMemo(
    () =>
      (graph?.nodes ?? []).map((item) => {
        const node = toFlowNodes([item], layoutMissing)[0]!
        return {
          ...node,
          data: { label: <TopologyNodeLabel item={item} />, item },
        }
      }),
    [graph?.nodes, layoutMissing],
  )

  const initialEdges: Edge[] = useMemo(
    () =>
      (graph?.edges ?? []).map((edge: TopologyEdge) => ({
        id: edge.relationshipId,
        source: edge.sourceCiId,
        target: edge.targetCiId,
        label: [edge.fromPort, edge.toPort].filter(Boolean).join(' → ') || edge.mediaType || undefined,
        animated: !edge.isConfirmed,
      })),
    [graph?.edges],
  )

  const [nodes, setNodes, onNodesChange] = useNodesState(initialNodes)
  const [edges, setEdges, onEdgesChange] = useEdgesState(initialEdges)

  useEffect(() => {
    setNodes(initialNodes)
    setEdges(initialEdges)
  }, [initialNodes, initialEdges, setNodes, setEdges])

  const saveLayouts = useMutation({
    mutationFn: (layouts: { configurationItemId: string; positionX: number; positionY: number }[]) =>
      cmdbApi.saveTopologyLayouts(graph!.view.id, layouts),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: cmdbKeys.all }),
  })

  const connectMutation = useMutation({
    mutationFn: () =>
      cmdbApi.createNetworkConnection({
        sourceCiId: selectedId!,
        targetCiId,
        fromPort: fromPort || null,
        toPort: toPort || null,
        mediaType,
        linkMode,
        notes: notes || null,
      }),
    onSuccess: () => {
      setConnectOpen(false)
      setFormError(null)
      queryClient.invalidateQueries({ queryKey: cmdbKeys.all })
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('cmdb.error.generic'))
    },
  })

  const deleteEdgeMutation = useMutation({
    mutationFn: (relationshipId: string) => cmdbApi.deleteNetworkConnection(relationshipId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: cmdbKeys.all }),
  })

  const onNodeDragStop: OnNodeDrag = useCallback(
    (_event, node) => {
      if (!can('cmdb.manage') || !graph?.view.id) return
      saveLayouts.mutate([
        {
          configurationItemId: node.id,
          positionX: node.position.x,
          positionY: node.position.y,
        },
      ])
    },
    [can, graph?.view.id, saveLayouts],
  )

  const onNodeClick: NodeMouseHandler = useCallback((_event, node) => {
    setSelectedId(node.id)
  }, [])

  const selectedNode = graph?.nodes.find((n) => n.configurationItemId === selectedId) ?? null

  const applyAutoLayout = () => {
    if (!graph || !can('cmdb.manage')) return
    const columns = Math.max(3, Math.ceil(Math.sqrt(Math.max(graph.nodes.length, 1))))
    const layouts = graph.nodes.map((item, index) => ({
      configurationItemId: item.configurationItemId,
      positionX: 40 + (index % columns) * 220,
      positionY: 40 + Math.floor(index / columns) * 120,
    }))
    saveLayouts.mutate(layouts)
  }

  return (
    <div className="space-y-4">
      <PageHeader title={t('cmdb.topology.title')} description={t('cmdb.topology.description')} />

      <div className="flex flex-wrap items-end gap-3">
        <div className="space-y-1">
          <Label>{t('cmdb.search')}</Label>
          <div className="flex gap-2">
            <Input
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              placeholder={t('cmdb.topology.searchPlaceholder')}
              className="w-56"
            />
            <Button type="button" variant="secondary" onClick={() => setSearch(searchInput.trim())}>
              <Search className="h-4 w-4" />
            </Button>
          </div>
        </div>
        <div className="space-y-1">
          <Label>{t('cmdb.fields.type')}</Label>
          <Select value={typeFilter} onValueChange={setTypeFilter}>
            <SelectTrigger className="w-48">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">{t('cmdb.topology.allTypes')}</SelectItem>
              {(typesQuery.data ?? [])
                .filter((type) =>
                  ['network-device', 'firewall', 'network-link', 'server'].includes(type.key),
                )
                .map((type) => (
                  <SelectItem key={type.id} value={type.key}>
                    {type.name}
                  </SelectItem>
                ))}
            </SelectContent>
          </Select>
        </div>
        {can('cmdb.manage') ? (
          <Button type="button" variant="outline" onClick={applyAutoLayout}>
            <LayoutGrid className="me-2 h-4 w-4" />
            {t('cmdb.topology.autoLayout')}
          </Button>
        ) : null}
      </div>

      <div className="grid gap-4 lg:grid-cols-[1fr_260px]">
        <Card className="overflow-hidden">
          <CardContent className="h-[640px] p-0">
            {graphQuery.isLoading ? (
              <div className="flex h-full items-center justify-center text-sm text-muted-foreground">
                {t('cmdb.topology.loading')}
              </div>
            ) : (graph?.nodes.length ?? 0) === 0 ? (
              <div className="flex h-full items-center justify-center p-6 text-center text-sm text-muted-foreground">
                {t('cmdb.topology.emptyGraph')}
              </div>
            ) : (
              <ReactFlow
                nodes={nodes}
                edges={edges}
                onNodesChange={onNodesChange}
                onEdgesChange={onEdgesChange}
                onNodeClick={onNodeClick}
                onNodeDragStop={onNodeDragStop}
                fitView
                proOptions={{ hideAttribution: true }}
              >
                <Background />
                <Controls />
                <MiniMap />
              </ReactFlow>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t('cmdb.topology.unmapped')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            {(graph?.unmapped.length ?? 0) === 0 ? (
              <p className="text-sm text-muted-foreground">{t('cmdb.topology.unmappedEmpty')}</p>
            ) : (
              graph!.unmapped.map((item) => (
                <button
                  key={item.configurationItemId}
                  type="button"
                  className="w-full rounded-md border px-3 py-2 text-start text-sm hover:bg-muted/50"
                  onClick={() => setSelectedId(item.configurationItemId)}
                >
                  <div className="font-medium">{item.name}</div>
                  <div className="text-xs text-muted-foreground">
                    {item.ciTypeName} · {item.serialNumber ?? '—'}
                  </div>
                </button>
              ))
            )}
            {(graph?.edges.length ?? 0) === 0 ? (
              <p className="pt-2 text-xs text-muted-foreground">{t('cmdb.topology.noConnections')}</p>
            ) : null}
          </CardContent>
        </Card>
      </div>

      <Sheet open={Boolean(selectedNode)} onOpenChange={(open) => !open && setSelectedId(null)}>
        <SheetContent className="start-auto end-0 w-full max-w-md overflow-y-auto bg-background text-foreground">
          {selectedNode ? (
            <div className="space-y-4 p-4 pe-10">
              <div>
                <h2 className="text-lg font-semibold">{selectedNode.name}</h2>
                <p className="text-sm text-muted-foreground">{selectedNode.ciTypeName}</p>
              </div>
              <dl className="grid gap-2 text-sm">
                <div>
                  <dt className="text-muted-foreground">{t('cmdb.topology.vendorModel')}</dt>
                  <dd>
                    {selectedNode.manufacturer ?? '—'} / {selectedNode.model ?? '—'}
                  </dd>
                </div>
                <div>
                  <dt className="text-muted-foreground">{t('cmdb.topology.serial')}</dt>
                  <dd>{selectedNode.serialNumber ?? '—'}</dd>
                </div>
                <div>
                  <dt className="text-muted-foreground">{t('cmdb.topology.hostname')}</dt>
                  <dd>{selectedNode.identities.find((i) => i.hostname)?.hostname ?? '—'}</dd>
                </div>
                <div>
                  <dt className="text-muted-foreground">{t('cmdb.topology.ipIdentities')}</dt>
                  <dd>
                    {selectedNode.identities.length === 0
                      ? '—'
                      : selectedNode.identities.map((i) => i.ipAddress).join(', ')}
                  </dd>
                </div>
                <div>
                  <dt className="text-muted-foreground">{t('cmdb.columns.criticality')}</dt>
                  <dd>{selectedNode.criticality ?? '—'}</dd>
                </div>
                <div>
                  <dt className="text-muted-foreground">{t('cmdb.topology.connections')}</dt>
                  <dd className="space-y-1">
                    {(graph?.edges ?? [])
                      .filter(
                        (e) =>
                          e.sourceCiId === selectedNode.configurationItemId ||
                          e.targetCiId === selectedNode.configurationItemId,
                      )
                      .map((edge) => (
                        <div key={edge.relationshipId} className="flex items-center justify-between gap-2">
                          <span className="truncate text-xs">
                            {edge.sourceCiId === selectedNode.configurationItemId
                              ? `→ ${graph?.nodes.find((n) => n.configurationItemId === edge.targetCiId)?.name ?? edge.targetCiId}`
                              : `← ${graph?.nodes.find((n) => n.configurationItemId === edge.sourceCiId)?.name ?? edge.sourceCiId}`}
                          </span>
                          {can('cmdb.relationship.manage') ? (
                            <Button
                              type="button"
                              size="sm"
                              variant="ghost"
                              onClick={() => deleteEdgeMutation.mutate(edge.relationshipId)}
                            >
                              {t('cmdb.relationships.delete')}
                            </Button>
                          ) : null}
                        </div>
                      ))}
                  </dd>
                </div>
              </dl>
              <div className="flex flex-wrap gap-2">
                <Button asChild variant="outline">
                  <Link to={`/it/cmdb?ci=${selectedNode.configurationItemId}`}>{t('cmdb.topology.openCi')}</Link>
                </Button>
                {can('cmdb.relationship.manage') ? (
                  <Button
                    type="button"
                    onClick={() => {
                      setTargetCiId('')
                      setFromPort('')
                      setToPort('')
                      setMediaType('Ethernet')
                      setLinkMode('Access')
                      setNotes('')
                      setFormError(null)
                      setConnectOpen(true)
                    }}
                  >
                    <Cable className="me-2 h-4 w-4" />
                    {t('cmdb.topology.connect')}
                  </Button>
                ) : null}
              </div>
            </div>
          ) : null}
        </SheetContent>
      </Sheet>

      <Dialog open={connectOpen} onOpenChange={setConnectOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('cmdb.topology.connectTitle')}</DialogTitle>
            <DialogDescription>{t('cmdb.topology.connectDescription')}</DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <div>
              <Label>{t('cmdb.topology.from')}</Label>
              <Input value={selectedNode?.name ?? ''} disabled />
            </div>
            <div>
              <Label>{t('cmdb.topology.fromPort')}</Label>
              <Input value={fromPort} onChange={(e) => setFromPort(e.target.value)} />
            </div>
            <div>
              <Label>{t('cmdb.topology.to')}</Label>
              <Select value={targetCiId} onValueChange={setTargetCiId}>
                <SelectTrigger>
                  <SelectValue placeholder={t('cmdb.relationships.targetPlaceholder')} />
                </SelectTrigger>
                <SelectContent>
                  {(cisQuery.data ?? [])
                    .filter((ci) => ci.id !== selectedId)
                    .map((ci) => (
                      <SelectItem key={ci.id} value={ci.id}>
                        {ci.name}
                      </SelectItem>
                    ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>{t('cmdb.topology.toPort')}</Label>
              <Input value={toPort} onChange={(e) => setToPort(e.target.value)} />
            </div>
            <div>
              <Label>{t('cmdb.topology.mediaType')}</Label>
              <Select value={mediaType} onValueChange={setMediaType}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {['Ethernet', 'Fiber', 'Wireless', 'Other'].map((item) => (
                    <SelectItem key={item} value={item}>
                      {item}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>{t('cmdb.topology.linkMode')}</Label>
              <Select value={linkMode} onValueChange={setLinkMode}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {['Access', 'Trunk', 'Routed', 'Other'].map((item) => (
                    <SelectItem key={item} value={item}>
                      {item}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>{t('cmdb.fields.description')}</Label>
              <Input value={notes} onChange={(e) => setNotes(e.target.value)} />
            </div>
            {formError ? <p className="text-sm text-destructive">{formError}</p> : null}
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setConnectOpen(false)}>
              {t('cmdb.topology.cancel')}
            </Button>
            <Button
              type="button"
              disabled={!targetCiId || connectMutation.isPending}
              onClick={() => connectMutation.mutate()}
            >
              {t('cmdb.topology.saveConnection')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}

export function NetworkTopologyPage() {
  return (
    <ReactFlowProvider>
      <NetworkTopologyCanvas />
    </ReactFlowProvider>
  )
}
