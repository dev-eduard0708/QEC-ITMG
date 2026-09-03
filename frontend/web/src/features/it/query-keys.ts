export const assetKeys = {
  all: ['assets'] as const,
  list: (search: string) => [...assetKeys.all, 'list', search] as const,
  detail: (id: string) => [...assetKeys.all, 'detail', id] as const,
  assignments: (id: string) => [...assetKeys.all, 'assignments', id] as const,
}

export const cmdbKeys = {
  all: ['cmdb'] as const,
  types: () => [...cmdbKeys.all, 'types'] as const,
  cis: (search: string) => [...cmdbKeys.all, 'cis', search] as const,
  relationships: (ciId: string) => [...cmdbKeys.all, 'relationships', ciId] as const,
}

export const equipmentKeys = {
  mine: ['me', 'equipment'] as const,
}
