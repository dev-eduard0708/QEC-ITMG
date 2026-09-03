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

export const ticketKeys = {
  all: ['tickets'] as const,
  mine: (filters: string) => ['me', 'tickets', filters] as const,
  mineDetail: (id: string) => ['me', 'tickets', 'detail', id] as const,
  list: (filters: string) => [...ticketKeys.all, 'list', filters] as const,
  detail: (id: string) => [...ticketKeys.all, 'detail', id] as const,
  queues: () => [...ticketKeys.all, 'queues'] as const,
  comments: (id: string, scope: 'me' | 'it') => [...ticketKeys.all, scope, id, 'comments'] as const,
  attachments: (id: string, scope: 'me' | 'it') =>
    [...ticketKeys.all, scope, id, 'attachments'] as const,
  timeline: (id: string, scope: 'me' | 'it') => [...ticketKeys.all, scope, id, 'timeline'] as const,
}
