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
  relatedProblems: (id: string) => [...ticketKeys.all, 'related-problems', id] as const,
}

export const problemKeys = {
  all: ['problems'] as const,
  list: (filters: string) => [...problemKeys.all, 'list', filters] as const,
  detail: (id: string) => [...problemKeys.all, 'detail', id] as const,
  incidents: (id: string) => [...problemKeys.all, 'incidents', id] as const,
  metrics: (id: string) => [...problemKeys.all, 'metrics', id] as const,
  recurringGroups: () => [...problemKeys.all, 'recurring-groups'] as const,
}

export const changeKeys = {
  all: ['changes'] as const,
  list: (filters: string) => [...changeKeys.all, 'list', filters] as const,
  detail: (id: string) => [...changeKeys.all, 'detail', id] as const,
  cis: (id: string) => [...changeKeys.all, 'cis', id] as const,
  approvals: (id: string) => [...changeKeys.all, 'approvals', id] as const,
  history: (id: string) => [...changeKeys.all, 'history', id] as const,
  catalog: () => [...changeKeys.all, 'catalog'] as const,
}

export const eventKeys = {
  all: ['events'] as const,
  list: (filters: string) => [...eventKeys.all, 'list', filters] as const,
  detail: (id: string) => [...eventKeys.all, 'detail', id] as const,
}
