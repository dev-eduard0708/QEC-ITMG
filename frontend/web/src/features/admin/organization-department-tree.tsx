import { Building2 } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Badge } from '@/components/ui/badge'
import { cn } from '@/lib/utils'

export type OrganizationDepartmentTreeNode = {
  id: string
  nameEn: string
  nameAr: string | null
  code: string
  parentDepartmentId: string | null
  peopleCount: number
  positionCount: number
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
    peopleCount: number
    positionCount: number
    isActive: boolean
    sortOrder: number
  }>,
): OrganizationDepartmentTreeNode[] {
  const nodes = new Map<string, OrganizationDepartmentTreeNode>()
  for (const card of cards) {
    nodes.set(card.id, { ...card, children: [] })
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

export function OrganizationDepartmentTree({
  language,
  roots,
  onSelect,
}: {
  language: string
  roots: OrganizationDepartmentTreeNode[]
  onSelect: (departmentId: string) => void
}) {
  const { t } = useTranslation()
  if (roots.length === 0) {
    return (
      <p className="rounded-md border border-dashed border-border p-8 text-center text-sm text-muted-foreground">
        {t('admin.hierarchy.noDepartments')}
      </p>
    )
  }

  return (
    <div className="space-y-8 overflow-x-auto pb-2">
      <p className="text-center text-lg font-semibold tracking-wide text-foreground">QEC</p>
      <div className="flex flex-col items-stretch gap-8 md:items-center">
        {roots.map((root) => (
          <OrganizationDepartmentNode
            key={root.id}
            node={root}
            language={language}
            onSelect={onSelect}
            depth={0}
          />
        ))}
      </div>
    </div>
  )
}

function OrganizationDepartmentNode({
  node,
  language,
  onSelect,
  depth,
}: {
  node: OrganizationDepartmentTreeNode
  language: string
  onSelect: (departmentId: string) => void
  depth: number
}) {
  const { t } = useTranslation()
  return (
    <div className="flex w-full flex-col items-stretch md:items-center">
      <button
        type="button"
        onClick={() => onSelect(node.id)}
        className={cn(
          'w-full max-w-sm rounded-lg border border-border bg-card p-4 text-start shadow-sm transition hover:border-primary/40 hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
          !node.isActive && 'opacity-70',
          depth === 0 && 'border-primary/30',
        )}
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
        <div className="mt-3 space-y-1 text-sm text-muted-foreground">
          <p>{t('admin.hierarchy.peopleCount', { count: node.peopleCount })}</p>
          <p>{t('admin.hierarchy.positionsCount', { count: node.positionCount })}</p>
          {!node.isActive ? (
            <Badge variant="secondary">{t('admin.hierarchy.inactive')}</Badge>
          ) : null}
        </div>
      </button>

      {node.children.length > 0 ? (
        <div className="flex w-full flex-col items-stretch md:items-center">
          <div className="mx-auto h-6 w-px bg-border" aria-hidden />
          <div
            className={cn(
              'flex w-full flex-col gap-6 md:flex-row md:flex-wrap md:justify-center md:gap-8',
              node.children.length > 1 && 'md:relative',
            )}
          >
            {node.children.length > 1 ? (
              <div
                className="pointer-events-none absolute start-[12%] end-[12%] top-0 hidden h-px bg-border md:block"
                aria-hidden
              />
            ) : null}
            {node.children.map((child) => (
              <div
                key={child.id}
                className="relative flex min-w-0 flex-col items-stretch md:min-w-[14rem] md:items-center"
              >
                <div className="mx-auto hidden h-6 w-px bg-border md:block" aria-hidden />
                <div className="ms-4 border-s border-border ps-4 md:ms-0 md:border-0 md:ps-0">
                  <OrganizationDepartmentNode
                    node={child}
                    language={language}
                    onSelect={onSelect}
                    depth={depth + 1}
                  />
                </div>
              </div>
            ))}
          </div>
        </div>
      ) : null}
    </div>
  )
}
