import { useState } from 'react'
import { Building2, ChevronDown, ChevronRight } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import type { OrganizationUnitType } from '@/api/client'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

export type OrganizationDepartmentTreeNode = {
  id: string
  nameEn: string
  nameAr: string | null
  code: string
  parentDepartmentId: string | null
  unitType: OrganizationUnitType
  peopleCount: number
  positionCount: number
  memberCount?: number
  isActive: boolean
  sortOrder: number
  children: OrganizationDepartmentTreeNode[]
}

function localizedName(language: string, nameEn: string, nameAr: string | null | undefined): string {
  return language.startsWith('ar') ? nameAr || nameEn : nameEn || nameAr || ''
}

export function buildOrganizationDepartmentForest(
  cards: Array<{
    id: string
    nameEn: string
    nameAr: string | null
    code: string
    parentDepartmentId: string | null
    unitType?: OrganizationUnitType
    peopleCount: number
    positionCount: number
    memberCount?: number
    isActive: boolean
    sortOrder: number
  }>,
): OrganizationDepartmentTreeNode[] {
  const nodes = new Map<string, OrganizationDepartmentTreeNode>()
  for (const card of cards) {
    nodes.set(card.id, {
      ...card,
      unitType: card.unitType ?? 'Department',
      children: [],
    })
  }

  const roots: OrganizationDepartmentTreeNode[] = []
  for (const node of nodes.values()) {
    const parentId = node.parentDepartmentId
    if (parentId && nodes.has(parentId) && parentId !== node.id) {
      nodes.get(parentId)!.children.push(node)
    } else {
      roots.push(node)
    }
  }

  const sortRecursive = (list: OrganizationDepartmentTreeNode[]) => {
    list.sort((a, b) => a.sortOrder - b.sortOrder || a.nameEn.localeCompare(b.nameEn))
    for (const child of list) sortRecursive(child.children)
  }
  sortRecursive(roots)
  return roots
}

export function buildUnitBreadcrumb(
  nodes: OrganizationDepartmentTreeNode[],
  departmentId: string,
): OrganizationDepartmentTreeNode[] {
  const byId = new Map<string, OrganizationDepartmentTreeNode>()
  const walk = (list: OrganizationDepartmentTreeNode[]) => {
    for (const node of list) {
      byId.set(node.id, node)
      walk(node.children)
    }
  }
  walk(nodes)

  const trail: OrganizationDepartmentTreeNode[] = []
  let current = byId.get(departmentId)
  const guard = new Set<string>()
  while (current && !guard.has(current.id)) {
    guard.add(current.id)
    trail.unshift(current)
    current = current.parentDepartmentId ? byId.get(current.parentDepartmentId) : undefined
  }
  return trail
}

export type OrganizationDepartmentTreeActions = {
  onSelect: (departmentId: string) => void
  onEdit?: (departmentId: string) => void
  onMove?: (departmentId: string) => void
  onDeactivate?: (departmentId: string) => void
  onReactivate?: (departmentId: string) => void
  onManageEmployees?: (departmentId: string) => void
  canManage?: boolean
}

export function OrganizationDepartmentTree({
  language,
  roots,
  actions,
  selectedId,
}: {
  language: string
  roots: OrganizationDepartmentTreeNode[]
  actions: OrganizationDepartmentTreeActions
  selectedId?: string | null
}) {
  const { t } = useTranslation()
  if (roots.length === 0) {
    return (
      <p className="rounded-md border border-dashed border-border p-8 text-center text-sm text-muted-foreground">
        {t('admin.hierarchy.noOrganizationalUnits')}
      </p>
    )
  }

  return (
    <div className="space-y-4 overflow-x-auto pb-2">
      <div className="flex flex-col gap-2">
        {roots.map((root) => (
          <OrganizationDepartmentNode
            key={root.id}
            node={root}
            language={language}
            actions={actions}
            selectedId={selectedId}
            depth={0}
            defaultExpanded
          />
        ))}
      </div>
    </div>
  )
}

function OrganizationDepartmentNode({
  node,
  language,
  actions,
  selectedId,
  depth,
  defaultExpanded,
}: {
  node: OrganizationDepartmentTreeNode
  language: string
  actions: OrganizationDepartmentTreeActions
  selectedId?: string | null
  depth: number
  defaultExpanded?: boolean
}) {
  const { t } = useTranslation()
  const [expanded, setExpanded] = useState(defaultExpanded ?? depth < 2)
  const hasChildren = node.children.length > 0
  const memberCount = node.memberCount ?? node.peopleCount

  return (
    <div className="min-w-[18rem]">
      <div
        className={cn(
          'rounded-lg border border-border bg-card p-3 shadow-sm transition',
          selectedId === node.id && 'border-primary/50 ring-1 ring-primary/30',
          !node.isActive && 'opacity-70',
        )}
        style={{ marginInlineStart: depth * 12 }}
      >
        <div className="flex items-start gap-2">
          {hasChildren ? (
            <button
              type="button"
              className="mt-0.5 rounded p-0.5 text-muted-foreground hover:bg-accent"
              aria-expanded={expanded}
              onClick={() => setExpanded((v) => !v)}
            >
              {expanded ? (
                <ChevronDown className="h-4 w-4" aria-hidden />
              ) : (
                <ChevronRight className="h-4 w-4" aria-hidden />
              )}
            </button>
          ) : (
            <span className="mt-0.5 inline-block w-5" aria-hidden />
          )}
          <button
            type="button"
            onClick={() => actions.onSelect(node.id)}
            className="min-w-0 flex-1 text-start focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            <div className="flex items-start justify-between gap-2">
              <div className="min-w-0">
                <p className="font-semibold text-foreground">
                  {localizedName(language, node.nameEn, node.nameAr)}
                </p>
                <p className="mt-0.5 text-xs text-muted-foreground">{node.code}</p>
              </div>
              <Building2 className="h-4 w-4 shrink-0 text-muted-foreground" aria-hidden />
            </div>
            <div className="mt-2 flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
              <Badge variant="outline">
                {t(`admin.hierarchy.unitTypes.${node.unitType}`)}
              </Badge>
              <span>{t('admin.hierarchy.peopleCount', { count: memberCount })}</span>
              <span>·</span>
              <span>{t('admin.hierarchy.positionsCount', { count: node.positionCount })}</span>
              {!node.isActive ? (
                <Badge variant="secondary">{t('admin.hierarchy.inactive')}</Badge>
              ) : null}
            </div>
          </button>
        </div>

        {actions.canManage ? (
          <div className="mt-3 flex flex-wrap gap-1.5 border-t border-border pt-2">
            {actions.onEdit ? (
              <Button size="sm" variant="outline" onClick={() => actions.onEdit?.(node.id)}>
                {t('admin.hierarchy.editOrganizationalUnit')}
              </Button>
            ) : null}
            {actions.onMove ? (
              <Button size="sm" variant="outline" onClick={() => actions.onMove?.(node.id)}>
                {t('admin.hierarchy.moveOrganizationalUnit')}
              </Button>
            ) : null}
            {actions.onManageEmployees ? (
              <Button
                size="sm"
                variant="outline"
                onClick={() => actions.onManageEmployees?.(node.id)}
              >
                {t('admin.hierarchy.manageEmployees')}
              </Button>
            ) : null}
            {node.isActive && actions.onDeactivate ? (
              <Button
                size="sm"
                variant="outline"
                onClick={() => actions.onDeactivate?.(node.id)}
              >
                {t('admin.hierarchy.deactivate')}
              </Button>
            ) : null}
            {!node.isActive && actions.onReactivate ? (
              <Button
                size="sm"
                variant="outline"
                onClick={() => actions.onReactivate?.(node.id)}
              >
                {t('admin.hierarchy.reactivate')}
              </Button>
            ) : null}
          </div>
        ) : null}
      </div>

      {hasChildren && expanded ? (
        <div className="mt-2 space-y-2 border-s border-border ms-4 ps-2">
          {node.children.map((child) => (
            <OrganizationDepartmentNode
              key={child.id}
              node={child}
              language={language}
              actions={actions}
              selectedId={selectedId}
              depth={depth + 1}
              defaultExpanded={depth < 1}
            />
          ))}
        </div>
      ) : null}
    </div>
  )
}
