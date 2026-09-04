export type AdminRoleSummary = {
  id: string
  name: string
}

export type AdminUser = {
  id: string
  upn: string
  displayName: string
  status: 'Active' | 'Disabled' | string
  userType: 'Employee' | 'Vendor' | 'Service' | string
  directoryObjectId: string | null
  timeZone: string | null
  rowVersion: string
  roles: AdminRoleSummary[]
}

export type AdminPermission = {
  id: string
  key: string
  description: string | null
}

export type AdminRole = {
  id: string
  name: string
  description: string | null
  isSystem: boolean
  rowVersion: string
  permissionCount: number
  permissions: AdminPermission[]
}

export type CreateAdminUserPayload = {
  upn: string
  displayName: string
  userType: string
  timeZone?: string | null
  directoryObjectId?: string | null
}

export type UpdateAdminUserPayload = {
  displayName: string
  userType: string
  status: string
  timeZone?: string | null
  directoryObjectId?: string | null
  rowVersion: string
}

export type CreateAdminRolePayload = {
  name: string
  description?: string | null
}

export type UpdateAdminRolePayload = {
  name: string
  description?: string | null
  rowVersion: string
}

export type LookupItem = {
  id: string
  name: string
  description: string | null
  isActive: boolean
  rowVersion: string
  createdAtUtc: string
  updatedAtUtc: string
}

export type CreateLookupItemPayload = {
  name: string
  description?: string | null
}

export type UpdateLookupItemPayload = {
  name: string
  description?: string | null
  isActive: boolean
  rowVersion: string
}

export class ApiError extends Error {
  readonly status: number
  readonly code?: string

  constructor(status: number, message: string, code?: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
  }
}

/**
 * Same-origin cookie credentials. Prefer relative URLs via Vite proxy in development.
 */
export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const isFormData = typeof FormData !== 'undefined' && init?.body instanceof FormData
  const response = await fetch(path, {
    ...init,
    credentials: 'include',
    headers: {
      Accept: 'application/json',
      ...(init?.body && !isFormData ? { 'Content-Type': 'application/json' } : {}),
      ...init?.headers,
    },
  })

  if (!response.ok) {
    let message = response.statusText || 'Request failed'
    let code: string | undefined
    try {
      const payload = (await response.json()) as {
        error?: { message?: string; code?: string }
      }
      message = payload.error?.message ?? message
      code = payload.error?.code
    } catch {
      // ignore non-JSON error bodies
    }
    throw new ApiError(response.status, message, code)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

export const adminApi = {
  listUsers: (search?: string) => {
    const query = search?.trim() ? `?search=${encodeURIComponent(search.trim())}` : ''
    return apiFetch<AdminUser[]>(`/api/v1/admin/users${query}`)
  },
  createUser: (payload: CreateAdminUserPayload) =>
    apiFetch<AdminUser>('/api/v1/admin/users', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  updateUser: (id: string, payload: UpdateAdminUserPayload) =>
    apiFetch<AdminUser>(`/api/v1/admin/users/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    }),
  replaceUserRoles: (id: string, roleIds: string[]) =>
    apiFetch<AdminUser>(`/api/v1/admin/users/${id}/roles`, {
      method: 'PUT',
      body: JSON.stringify({ roleIds }),
    }),
  listRoles: () => apiFetch<AdminRole[]>('/api/v1/admin/roles'),
  getRole: (id: string) => apiFetch<AdminRole>(`/api/v1/admin/roles/${id}`),
  createRole: (payload: CreateAdminRolePayload) =>
    apiFetch<AdminRole>('/api/v1/admin/roles', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  updateRole: (id: string, payload: UpdateAdminRolePayload) =>
    apiFetch<AdminRole>(`/api/v1/admin/roles/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    }),
  replaceRolePermissions: (id: string, permissionIds: string[]) =>
    apiFetch<AdminRole>(`/api/v1/admin/roles/${id}/permissions`, {
      method: 'PUT',
      body: JSON.stringify({ permissionIds }),
    }),
  listPermissions: () => apiFetch<AdminPermission[]>('/api/v1/admin/permissions'),
  listDepartments: () => apiFetch<LookupItem[]>('/api/v1/admin/lookups/departments'),
  createDepartment: (payload: CreateLookupItemPayload) =>
    apiFetch<LookupItem>('/api/v1/admin/lookups/departments', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  updateDepartment: (id: string, payload: UpdateLookupItemPayload) =>
    apiFetch<LookupItem>(`/api/v1/admin/lookups/departments/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    }),
  listLocations: () => apiFetch<LookupItem[]>('/api/v1/admin/lookups/locations'),
  createLocation: (payload: CreateLookupItemPayload) =>
    apiFetch<LookupItem>('/api/v1/admin/lookups/locations', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  updateLocation: (id: string, payload: UpdateLookupItemPayload) =>
    apiFetch<LookupItem>(`/api/v1/admin/lookups/locations/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    }),
}

export type CiType = {
  id: string
  key: string
  name: string
  description: string | null
  isActive: boolean
}

export type ConfigurationItem = {
  id: string
  ciNumber: string
  ciTypeId: string
  ciTypeKey: string
  ciTypeName: string
  name: string
  description: string | null
  status: string
  criticality: string | null
  locationId: string | null
  departmentId: string | null
  ownerUserId: string | null
  serialNumber: string | null
  manufacturer: string | null
  model: string | null
  notes: string | null
  rowVersion: string
  createdAtUtc: string
  updatedAtUtc: string
}

export type CiRelationship = {
  id: string
  sourceCiId: string
  targetCiId: string
  relationshipType: string
  notes: string | null
  createdAtUtc: string
}

export type Asset = {
  id: string
  assetNumber: string
  configurationItemId: string | null
  configurationItemNumber: string | null
  assetType: string
  name: string
  serialNumber: string | null
  manufacturer: string | null
  model: string | null
  purchaseDate: string | null
  purchaseCost: number | null
  warrantyExpiry: string | null
  status: string
  locationId: string | null
  notes: string | null
  activeAssignedToUserId: string | null
  activeAssignedAtUtc: string | null
  rowVersion: string
  createdAtUtc: string
  updatedAtUtc: string
}

export type AssetAssignment = {
  id: string
  assetId: string
  assignedToUserId: string
  assignedByUserId: string
  assignedAtUtc: string
  returnedAtUtc: string | null
  notes: string | null
  isActive: boolean
}

export type CreateAssetPayload = {
  assetType: string
  name: string
  configurationItemId?: string | null
  serialNumber?: string | null
  manufacturer?: string | null
  model?: string | null
  locationId?: string | null
  notes?: string | null
}

export type UpdateAssetPayload = {
  assetType: string
  name: string
  status: string
  configurationItemId?: string | null
  serialNumber?: string | null
  manufacturer?: string | null
  model?: string | null
  locationId?: string | null
  notes?: string | null
  rowVersion: string
}

export type CreateCiPayload = {
  ciTypeId: string
  name: string
  description?: string | null
  criticality?: string | null
  locationId?: string | null
  notes?: string | null
}

export type UpdateCiPayload = {
  name: string
  description?: string | null
  status: string
  criticality?: string | null
  locationId?: string | null
  notes?: string | null
  rowVersion: string
}

export const assetsApi = {
  list: (search?: string) => {
    const query = search?.trim() ? `?search=${encodeURIComponent(search.trim())}` : ''
    return apiFetch<Asset[]>(`/api/v1/assets${query}`)
  },
  get: (id: string) => apiFetch<Asset>(`/api/v1/assets/${id}`),
  create: (payload: CreateAssetPayload) =>
    apiFetch<Asset>('/api/v1/assets', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  update: (id: string, payload: UpdateAssetPayload) =>
    apiFetch<Asset>(`/api/v1/assets/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    }),
  listAssignments: (id: string) =>
    apiFetch<AssetAssignment[]>(`/api/v1/assets/${id}/assignments`),
  assign: (id: string, assignedToUserId: string, notes?: string | null) =>
    apiFetch<AssetAssignment>(`/api/v1/assets/${id}/assign`, {
      method: 'POST',
      body: JSON.stringify({ assignedToUserId, notes: notes ?? null }),
    }),
  returnAsset: (id: string, notes?: string | null) =>
    apiFetch<AssetAssignment>(`/api/v1/assets/${id}/return`, {
      method: 'POST',
      body: JSON.stringify({ notes: notes ?? null }),
    }),
}

export const cmdbApi = {
  listCiTypes: () => apiFetch<CiType[]>('/api/v1/cmdb/ci-types'),
  listCis: (search?: string) => {
    const query = search?.trim() ? `?search=${encodeURIComponent(search.trim())}` : ''
    return apiFetch<ConfigurationItem[]>(`/api/v1/cmdb/cis${query}`)
  },
  getCi: (id: string) => apiFetch<ConfigurationItem>(`/api/v1/cmdb/cis/${id}`),
  createCi: (payload: CreateCiPayload) =>
    apiFetch<ConfigurationItem>('/api/v1/cmdb/cis', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  updateCi: (id: string, payload: UpdateCiPayload) =>
    apiFetch<ConfigurationItem>(`/api/v1/cmdb/cis/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    }),
  listRelationships: (ciId: string) =>
    apiFetch<CiRelationship[]>(`/api/v1/cmdb/cis/${ciId}/relationships`),
  createRelationship: (
    ciId: string,
    payload: { targetCiId: string; relationshipType: string; notes?: string | null },
  ) =>
    apiFetch<CiRelationship>(`/api/v1/cmdb/cis/${ciId}/relationships`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  deleteRelationship: (id: string) =>
    apiFetch<void>(`/api/v1/cmdb/relationships/${id}`, { method: 'DELETE' }),
}

export const meApi = {
  listEquipment: () => apiFetch<Asset[]>('/api/v1/me/equipment'),
  listTickets: (params?: { page?: number; pageSize?: number; search?: string; status?: string }) => {
    const query = new URLSearchParams()
    if (params?.page) query.set('page', String(params.page))
    if (params?.pageSize) query.set('pageSize', String(params.pageSize))
    if (params?.search?.trim()) query.set('search', params.search.trim())
    if (params?.status?.trim()) query.set('status', params.status.trim())
    const qs = query.toString()
    return apiFetch<TicketListResult>(`/api/v1/me/tickets${qs ? `?${qs}` : ''}`)
  },
  getTicket: (id: string) => apiFetch<Ticket>(`/api/v1/me/tickets/${id}`),
  createTicket: (payload: CreateMeTicketPayload) =>
    apiFetch<Ticket>('/api/v1/me/tickets', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  listTicketComments: (id: string) =>
    apiFetch<TicketComment[]>(`/api/v1/me/tickets/${id}/comments`),
  addTicketComment: (id: string, body: string) =>
    apiFetch<TicketComment>(`/api/v1/me/tickets/${id}/comments`, {
      method: 'POST',
      body: JSON.stringify({ body }),
    }),
  listTicketAttachments: (id: string) =>
    apiFetch<TicketAttachment[]>(`/api/v1/me/tickets/${id}/attachments`),
  uploadTicketAttachment: async (id: string, file: File) => {
    const form = new FormData()
    form.append('file', file)
    return apiFetch<TicketAttachment>(`/api/v1/me/tickets/${id}/attachments`, {
      method: 'POST',
      body: form,
    })
  },
  listTicketTimeline: (id: string) =>
    apiFetch<TicketTimelineItem[]>(`/api/v1/me/tickets/${id}/timeline`),
  ticketAttachmentContentUrl: (ticketId: string, attachmentId: string) =>
    `/api/v1/me/tickets/${ticketId}/attachments/${attachmentId}/content`,
}

export type Ticket = {
  id: string
  ticketNumber: string
  type: string
  title: string
  description: string
  status: string
  priority: string
  requesterUserId: string
  assignedUserId: string | null
  queueId: string | null
  configurationItemId: string | null
  category: string | null
  slaPolicyId: string | null
  responseDueAtUtc: string | null
  resolutionDueAtUtc: string | null
  respondedAtUtc: string | null
  responseBreached: boolean
  resolutionBreached: boolean
  createdAtUtc: string
  updatedAtUtc: string
  resolvedAtUtc: string | null
  closedAtUtc: string | null
  rowVersion: string
  isMajorIncident: boolean
  securityClassification: string | null
  sourceEventId: string | null
}

export type TicketListResult = {
  items: Ticket[]
  totalCount: number
  page: number
  pageSize: number
}

export type SupportQueue = {
  id: string
  name: string
  description: string | null
  isActive: boolean
}

export type CreateMeTicketPayload = {
  type?: string
  title: string
  description: string
  priority?: string
  configurationItemId?: string | null
  category?: string | null
}

export type TicketComment = {
  id: string
  resourceType: string
  resourceId: string
  authorUserId: string
  body: string
  visibility: string
  createdAtUtc: string
  editedAtUtc: string | null
}

export type TicketAttachment = {
  id: string
  fileName: string
  contentType: string
  sizeBytes: number
  scanStatus: string
  uploadedByUserId: string
  uploadedAtUtc: string
}

export type TicketTimelineItem = {
  id: string
  type: string
  timestamp: string
  title: string
  description: string | null
  actor: string | null
  status: string | null
}

export const ticketsApi = {
  list: (params?: {
    page?: number
    pageSize?: number
    search?: string
    status?: string
    type?: string
    priority?: string
  }) => {
    const query = new URLSearchParams()
    if (params?.page) query.set('page', String(params.page))
    if (params?.pageSize) query.set('pageSize', String(params.pageSize))
    if (params?.search?.trim()) query.set('search', params.search.trim())
    if (params?.status?.trim()) query.set('status', params.status.trim())
    if (params?.type?.trim()) query.set('type', params.type.trim())
    if (params?.priority?.trim()) query.set('priority', params.priority.trim())
    const qs = query.toString()
    return apiFetch<TicketListResult>(`/api/v1/tickets${qs ? `?${qs}` : ''}`)
  },
  get: (id: string) => apiFetch<Ticket>(`/api/v1/tickets/${id}`),
  listQueues: () => apiFetch<SupportQueue[]>('/api/v1/tickets/queues'),
  changeStatus: (id: string, status: string, rowVersion?: string | null) =>
    apiFetch<Ticket>(`/api/v1/tickets/${id}/status`, {
      method: 'POST',
      body: JSON.stringify({ status, rowVersion: rowVersion ?? null }),
    }),
  assign: (
    id: string,
    payload: { queueId?: string | null; assignedUserId?: string | null; notes?: string | null },
  ) =>
    apiFetch<Ticket>(`/api/v1/tickets/${id}/assign`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  listComments: (id: string) => apiFetch<TicketComment[]>(`/api/v1/tickets/${id}/comments`),
  addComment: (id: string, body: string, visibility: string) =>
    apiFetch<TicketComment>(`/api/v1/tickets/${id}/comments`, {
      method: 'POST',
      body: JSON.stringify({ body, visibility }),
    }),
  listAttachments: (id: string) =>
    apiFetch<TicketAttachment[]>(`/api/v1/tickets/${id}/attachments`),
  uploadAttachment: async (id: string, file: File) => {
    const form = new FormData()
    form.append('file', file)
    return apiFetch<TicketAttachment>(`/api/v1/tickets/${id}/attachments`, {
      method: 'POST',
      body: form,
    })
  },
  listTimeline: (id: string) =>
    apiFetch<TicketTimelineItem[]>(`/api/v1/tickets/${id}/timeline`),
  attachmentContentUrl: (ticketId: string, attachmentId: string) =>
    `/api/v1/tickets/${ticketId}/attachments/${attachmentId}/content`,
  dashboard: () => apiFetch<TicketDashboard>('/api/v1/tickets/dashboard'),
  updateIncident: (
    id: string,
    payload: {
      isMajorIncident: boolean
      securityClassification?: string | null
      rowVersion?: string | null
    },
  ) =>
    apiFetch<Ticket>(`/api/v1/tickets/${id}/incident`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    }),
  listRelatedProblems: (id: string) =>
    apiFetch<RelatedProblem[]>(`/api/v1/tickets/${id}/problems`),
}

export type RelatedProblem = {
  problemId: string
  problemNumber: string
  title: string
  status: string
  linkedAtUtc: string
}

export type Problem = {
  id: string
  problemNumber: string
  title: string
  description: string
  status: string
  priority: string
  ownerUserId: string | null
  configurationItemId: string | null
  rootCause: string | null
  workaround: string | null
  isKnownError: boolean
  knownErrorAtUtc: string | null
  knownErrorByUserId: string | null
  createdAtUtc: string
  updatedAtUtc: string
  resolvedAtUtc: string | null
  closedAtUtc: string | null
  rowVersion: string
}

export type ProblemRecurringMetrics = {
  linkedIncidentCount: number
  openLinkedIncidents: number
  majorLinkedIncidents: number
  firstOccurrenceUtc: string | null
  latestOccurrenceUtc: string | null
  recentOccurrenceCount: number
  recentWindowDays: number
}

export type RecurringIncidentGroup = {
  groupType: string
  groupKey: string
  incidentCount: number
  linkedProblemCount: number
}

export type ProblemListResult = {
  items: Problem[]
  totalCount: number
  page: number
  pageSize: number
}

export type ProblemIncidentLink = {
  problemId: string
  incidentTicketId: string
  ticketNumber: string
  title: string
  status: string
  priority: string
  isMajorIncident: boolean
  linkedAtUtc: string
  linkedByUserId: string
}

export const problemsApi = {
  list: (params?: {
    page?: number
    pageSize?: number
    search?: string
    status?: string
    priority?: string
  }) => {
    const query = new URLSearchParams()
    if (params?.page) query.set('page', String(params.page))
    if (params?.pageSize) query.set('pageSize', String(params.pageSize))
    if (params?.search?.trim()) query.set('search', params.search.trim())
    if (params?.status?.trim()) query.set('status', params.status.trim())
    if (params?.priority?.trim()) query.set('priority', params.priority.trim())
    const qs = query.toString()
    return apiFetch<ProblemListResult>(`/api/v1/problems${qs ? `?${qs}` : ''}`)
  },
  get: (id: string) => apiFetch<Problem>(`/api/v1/problems/${id}`),
  metrics: (id: string, recentDays = 30) =>
    apiFetch<ProblemRecurringMetrics>(`/api/v1/problems/${id}/metrics?recentDays=${recentDays}`),
  recurringGroups: (take = 10) =>
    apiFetch<RecurringIncidentGroup[]>(`/api/v1/problems/recurring-groups?take=${take}`),
  create: (payload: {
    title: string
    description: string
    priority?: string
    ownerUserId?: string | null
    configurationItemId?: string | null
  }) =>
    apiFetch<Problem>('/api/v1/problems', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  update: (
    id: string,
    payload: {
      title: string
      description: string
      priority: string
      ownerUserId?: string | null
      configurationItemId?: string | null
      rootCause?: string | null
      workaround?: string | null
      rowVersion?: string | null
    },
  ) =>
    apiFetch<Problem>(`/api/v1/problems/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    }),
  changeStatus: (id: string, status: string, rowVersion?: string | null) =>
    apiFetch<Problem>(`/api/v1/problems/${id}/status`, {
      method: 'POST',
      body: JSON.stringify({ status, rowVersion: rowVersion ?? null }),
    }),
  setKnownError: (id: string, isKnownError: boolean, rowVersion?: string | null) =>
    apiFetch<Problem>(`/api/v1/problems/${id}/known-error`, {
      method: 'POST',
      body: JSON.stringify({ isKnownError, rowVersion: rowVersion ?? null }),
    }),
  listIncidents: (id: string) =>
    apiFetch<ProblemIncidentLink[]>(`/api/v1/problems/${id}/incidents`),
  linkIncident: (id: string, incidentTicketId: string) =>
    apiFetch<ProblemIncidentLink[]>(`/api/v1/problems/${id}/incidents`, {
      method: 'POST',
      body: JSON.stringify({ incidentTicketId }),
    }),
    unlinkIncident: (id: string, ticketId: string) =>
    apiFetch<void>(`/api/v1/problems/${id}/incidents/${ticketId}`, { method: 'DELETE' }),
}

export type ChangeRequest = {
  id: string
  changeNumber: string
  title: string
  description: string
  type: string
  status: string
  riskRating: string
  requesterUserId: string
  ownerUserId: string | null
  businessImpact: string | null
  technicalImpact: string | null
  securityImpact: string | null
  implementationPlan: string | null
  testPlan: string | null
  rollbackPlan: string | null
  scheduledStartUtc: string | null
  scheduledEndUtc: string | null
  implementationStartedAtUtc: string | null
  implementationCompletedAtUtc: string | null
  result: string
  validationNotes: string | null
  pirNotes: string | null
  isRetrospective: boolean
  isPreAuthorizedStandard: boolean
  catalogItemId: string | null
  retrospectiveReason: string | null
  actualImplementationAtUtc: string | null
  retrospectiveRecordedAtUtc: string | null
  createdAtUtc: string
  updatedAtUtc: string
  closedAtUtc: string | null
  rowVersion: string
  affectedCiCount: number
}

export type ChangeListResult = {
  items: ChangeRequest[]
  totalCount: number
  page: number
  pageSize: number
}

export type ChangeCiLink = {
  changeRequestId: string
  configurationItemId: string
  linkedAtUtc: string
  linkedByUserId: string
}

export type ChangeApproval = {
  id: string
  changeRequestId: string
  approverUserId: string
  decision: string
  comment: string | null
  decidedAtUtc: string | null
  createdAtUtc: string
}

export type ChangeTimelineEvent = {
  id: string
  event: string
  actorUserId: string | null
  occurredAtUtc: string
  summary: string
  details: string | null
}

export type ChangeCatalogItem = {
  id: string
  code: string
  name: string
  description: string | null
  riskRating: string
  implementationPlan: string
  testPlan: string
  rollbackPlan: string
  isActive: boolean
  createdAtUtc: string
  updatedAtUtc: string
  rowVersion: string
}

export const changesApi = {
  list: (params?: {
    page?: number
    pageSize?: number
    search?: string
    type?: string
    status?: string
    risk?: string
    ownerUserId?: string
  }) => {
    const query = new URLSearchParams()
    if (params?.page) query.set('page', String(params.page))
    if (params?.pageSize) query.set('pageSize', String(params.pageSize))
    if (params?.search?.trim()) query.set('search', params.search.trim())
    if (params?.type?.trim()) query.set('type', params.type.trim())
    if (params?.status?.trim()) query.set('status', params.status.trim())
    if (params?.risk?.trim()) query.set('risk', params.risk.trim())
    if (params?.ownerUserId?.trim()) query.set('ownerUserId', params.ownerUserId.trim())
    const qs = query.toString()
    return apiFetch<ChangeListResult>(`/api/v1/changes${qs ? `?${qs}` : ''}`)
  },
  get: (id: string) => apiFetch<ChangeRequest>(`/api/v1/changes/${id}`),
  create: (payload: {
    title: string
    description: string
    type: string
    riskRating?: string
    ownerUserId?: string | null
    isRetrospective?: boolean
    isPreAuthorizedStandard?: boolean
    retrospectiveReason?: string | null
    actualImplementationAtUtc?: string | null
  }) =>
    apiFetch<ChangeRequest>('/api/v1/changes', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  createFromCatalog: (catalogItemId: string, payload?: { title?: string; description?: string }) =>
    apiFetch<ChangeRequest>(`/api/v1/changes/from-catalog/${catalogItemId}`, {
      method: 'POST',
      body: JSON.stringify(payload ?? {}),
    }),
  update: (
    id: string,
    payload: {
      title: string
      description: string
      type: string
      riskRating: string
      ownerUserId?: string | null
      businessImpact?: string | null
      technicalImpact?: string | null
      securityImpact?: string | null
      implementationPlan?: string | null
      testPlan?: string | null
      rollbackPlan?: string | null
      scheduledStartUtc?: string | null
      scheduledEndUtc?: string | null
      isPreAuthorizedStandard?: boolean
      rowVersion?: string | null
    },
  ) =>
    apiFetch<ChangeRequest>(`/api/v1/changes/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    }),
  markRetrospective: (
    id: string,
    payload: { reason: string; actualImplementationAtUtc?: string | null; rowVersion?: string | null },
  ) =>
    apiFetch<ChangeRequest>(`/api/v1/changes/${id}/retrospective`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  listCis: (id: string) => apiFetch<ChangeCiLink[]>(`/api/v1/changes/${id}/configuration-items`),
  linkCi: (id: string, configurationItemId: string) =>
    apiFetch<ChangeCiLink[]>(`/api/v1/changes/${id}/configuration-items`, {
      method: 'POST',
      body: JSON.stringify({ configurationItemId }),
    }),
  unlinkCi: (id: string, ciId: string) =>
    apiFetch<void>(`/api/v1/changes/${id}/configuration-items/${ciId}`, { method: 'DELETE' }),
  listApprovals: (id: string) => apiFetch<ChangeApproval[]>(`/api/v1/changes/${id}/approvals`),
  approve: (id: string, comment?: string | null) =>
    apiFetch<ChangeApproval[]>(`/api/v1/changes/${id}/approve`, {
      method: 'POST',
      body: JSON.stringify({ comment: comment ?? null }),
    }),
  reject: (id: string, comment?: string | null) =>
    apiFetch<ChangeApproval[]>(`/api/v1/changes/${id}/reject`, {
      method: 'POST',
      body: JSON.stringify({ comment: comment ?? null }),
    }),
  listHistory: (id: string) => apiFetch<ChangeTimelineEvent[]>(`/api/v1/changes/${id}/history`),
  listCatalog: () => apiFetch<ChangeCatalogItem[]>('/api/v1/changes/catalog'),
  getCatalog: (id: string) => apiFetch<ChangeCatalogItem>(`/api/v1/changes/catalog/${id}`),
  createCatalog: (payload: {
    code: string
    name: string
    description?: string | null
    riskRating: string
    implementationPlan: string
    testPlan: string
    rollbackPlan: string
  }) =>
    apiFetch<ChangeCatalogItem>('/api/v1/changes/catalog', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  updateCatalog: (
    id: string,
    payload: {
      name: string
      description?: string | null
      riskRating: string
      implementationPlan: string
      testPlan: string
      rollbackPlan: string
      isActive: boolean
      rowVersion: string
    },
  ) =>
    apiFetch<ChangeCatalogItem>(`/api/v1/changes/catalog/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    }),
  transition: (
    id: string,
    payload: {
      targetStatus: string
      comment?: string | null
      validationNotes?: string | null
      pirNotes?: string | null
      result?: string | null
      rowVersion?: string | null
      approverUserId?: string | null
    },
  ) =>
    apiFetch<ChangeRequest>(`/api/v1/changes/${id}/transition`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
}

export type OperationalEvent = {
  id: string
  eventNumber: string
  source: string
  sourceEventKey: string
  severity: string
  title: string
  summary: string
  configurationItemId: string | null
  status: string
  occurrenceCount: number
  firstSeenAtUtc: string
  lastSeenAtUtc: string
  acknowledgedAtUtc: string | null
  acknowledgedByUserId: string | null
  linkedTicketId: string | null
  createdAtUtc: string
  updatedAtUtc: string
  rowVersion: string
}

export type EventListResult = {
  items: OperationalEvent[]
  totalCount: number
  page: number
  pageSize: number
}

export type IngestEventResult = {
  event: OperationalEvent
  created: boolean
}

export type PromoteEventResult = {
  event: OperationalEvent
  ticketId: string
  ticketNumber: string
}

export const eventsApi = {
  list: (params?: {
    page?: number
    pageSize?: number
    search?: string
    status?: string
    severity?: string
    source?: string
  }) => {
    const query = new URLSearchParams()
    if (params?.page) query.set('page', String(params.page))
    if (params?.pageSize) query.set('pageSize', String(params.pageSize))
    if (params?.search?.trim()) query.set('search', params.search.trim())
    if (params?.status?.trim()) query.set('status', params.status.trim())
    if (params?.severity?.trim()) query.set('severity', params.severity.trim())
    if (params?.source?.trim()) query.set('source', params.source.trim())
    const qs = query.toString()
    return apiFetch<EventListResult>(`/api/v1/events${qs ? `?${qs}` : ''}`)
  },
  get: (id: string) => apiFetch<OperationalEvent>(`/api/v1/events/${id}`),
  ingest: (payload: {
    source: string
    sourceEventKey: string
    severity: string
    title: string
    summary: string
    configurationItemId?: string | null
  }) =>
    apiFetch<IngestEventResult>('/api/v1/events/ingest', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  acknowledge: (id: string) =>
    apiFetch<OperationalEvent>(`/api/v1/events/${id}/acknowledge`, { method: 'POST' }),
  promote: (id: string, payload?: { title?: string; description?: string; priority?: string }) =>
    apiFetch<PromoteEventResult>(`/api/v1/events/${id}/promote`, {
      method: 'POST',
      body: JSON.stringify(payload ?? {}),
    }),
  close: (id: string) =>
    apiFetch<OperationalEvent>(`/api/v1/events/${id}/close`, { method: 'POST' }),
}

export type OpsPaged<T> = {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export type BackupJob = {
  id: string
  name: string
  provider: string
  externalJobId: string | null
  configurationItemId: string | null
  isActive: boolean
  createdAtUtc: string
  updatedAtUtc: string
}

export type BackupRun = {
  id: string
  backupJobId: string
  startedAtUtc: string
  completedAtUtc: string | null
  status: string
  summary: string | null
  externalReference: string | null
}

export type RestoreTest = {
  id: string
  backupJobId: string | null
  configurationItemId: string | null
  scheduledAtUtc: string | null
  performedAtUtc: string | null
  result: string
  performedByUserId: string | null
  notes: string | null
  createdAtUtc: string
}

export type CertificateRecord = {
  id: string
  name: string
  configurationItemId: string | null
  subject: string | null
  issuer: string | null
  thumbprint: string | null
  expiresAtUtc: string
  ownerUserId: string | null
  isActive: boolean
  daysToExpiry: number
  expired: boolean
  expiringSoon: boolean
  createdAtUtc: string
  updatedAtUtc: string
}

export type PatchBaseline = {
  id: string
  name: string
  description: string | null
  version: string | null
  isActive: boolean
  createdAtUtc: string
  updatedAtUtc: string
}

export type PatchDeployment = {
  id: string
  patchBaselineId: string | null
  configurationItemId: string
  externalReference: string | null
  status: string
  scheduledAtUtc: string | null
  startedAtUtc: string | null
  completedAtUtc: string | null
  summary: string | null
  createdAtUtc: string
}

export type ScheduledJob = {
  id: string
  name: string
  provider: string | null
  externalJobId: string | null
  configurationItemId: string | null
  scheduleDescription: string | null
  isActive: boolean
  lastRunAtUtc: string | null
  lastResult: string
  nextRunAtUtc: string | null
  createdAtUtc: string
  updatedAtUtc: string
}

function opsQuery(params?: Record<string, string | number | undefined | null>) {
  const query = new URLSearchParams()
  if (!params) return ''
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null || value === '') continue
    query.set(key, String(value))
  }
  const qs = query.toString()
  return qs ? `?${qs}` : ''
}

export const opsApi = {
  listBackupJobs: (params?: { page?: number; pageSize?: number; search?: string }) =>
    apiFetch<OpsPaged<BackupJob>>(`/api/v1/ops/backup-jobs${opsQuery(params)}`),
  createBackupJob: (payload: {
    name: string
    provider: string
    externalJobId?: string | null
    configurationItemId?: string | null
  }) =>
    apiFetch<BackupJob>('/api/v1/ops/backup-jobs', { method: 'POST', body: JSON.stringify(payload) }),
  listBackupRuns: (params?: { page?: number; pageSize?: number; backupJobId?: string; status?: string }) =>
    apiFetch<OpsPaged<BackupRun>>(`/api/v1/ops/backup-runs${opsQuery(params)}`),
  createBackupRun: (payload: {
    backupJobId: string
    startedAtUtc?: string
    status?: string
    summary?: string | null
    externalReference?: string | null
  }) =>
    apiFetch<BackupRun>('/api/v1/ops/backup-runs', { method: 'POST', body: JSON.stringify(payload) }),
  listRestoreTests: (params?: { page?: number; pageSize?: number; result?: string }) =>
    apiFetch<OpsPaged<RestoreTest>>(`/api/v1/ops/restore-tests${opsQuery(params)}`),
  createRestoreTest: (payload: {
    backupJobId?: string | null
    configurationItemId?: string | null
    scheduledAtUtc?: string | null
    notes?: string | null
  }) =>
    apiFetch<RestoreTest>('/api/v1/ops/restore-tests', { method: 'POST', body: JSON.stringify(payload) }),
  listCertificates: (params?: { page?: number; pageSize?: number; search?: string; activeOnly?: boolean }) =>
    apiFetch<OpsPaged<CertificateRecord>>(
      `/api/v1/ops/certificates${opsQuery({
        page: params?.page,
        pageSize: params?.pageSize,
        search: params?.search,
        activeOnly: params?.activeOnly === undefined ? undefined : String(params.activeOnly),
      })}`,
    ),
  createCertificate: (payload: {
    name: string
    expiresAtUtc: string
    configurationItemId?: string | null
    subject?: string | null
    issuer?: string | null
    thumbprint?: string | null
    ownerUserId?: string | null
  }) =>
    apiFetch<CertificateRecord>('/api/v1/ops/certificates', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  listPatchBaselines: (params?: { page?: number; pageSize?: number; search?: string }) =>
    apiFetch<OpsPaged<PatchBaseline>>(`/api/v1/ops/patch-baselines${opsQuery(params)}`),
  createPatchBaseline: (payload: { name: string; description?: string | null; version?: string | null }) =>
    apiFetch<PatchBaseline>('/api/v1/ops/patch-baselines', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  listPatchDeployments: (params?: {
    page?: number
    pageSize?: number
    configurationItemId?: string
    status?: string
  }) => apiFetch<OpsPaged<PatchDeployment>>(`/api/v1/ops/patch-deployments${opsQuery(params)}`),
  createPatchDeployment: (payload: {
    configurationItemId: string
    patchBaselineId?: string | null
    externalReference?: string | null
    scheduledAtUtc?: string | null
    summary?: string | null
  }) =>
    apiFetch<PatchDeployment>('/api/v1/ops/patch-deployments', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  listJobs: (params?: { page?: number; pageSize?: number; search?: string }) =>
    apiFetch<OpsPaged<ScheduledJob>>(`/api/v1/ops/jobs${opsQuery(params)}`),
  createJob: (payload: {
    name: string
    provider?: string | null
    externalJobId?: string | null
    configurationItemId?: string | null
    scheduleDescription?: string | null
    nextRunAtUtc?: string | null
  }) => apiFetch<ScheduledJob>('/api/v1/ops/jobs', { method: 'POST', body: JSON.stringify(payload) }),
}

export type AccessCase = {
  id: string
  caseNumber: string
  type: string
  status: string
  requesterUserId: string
  subjectUserId: string | null
  subjectName: string | null
  subjectEmail: string | null
  departmentId: string | null
  managerUserId: string | null
  designatedApproverUserId: string | null
  linkedTicketId: string | null
  effectiveAtUtc: string | null
  reason: string
  existingAccessConfirmed: boolean
  existingAccessConfirmedAtUtc: string | null
  existingAccessConfirmedByUserId: string | null
  createdAtUtc: string
  updatedAtUtc: string
  closedAtUtc: string | null
  rowVersion: string
  itemCount: number
  pendingMandatoryCount: number
}

export type AccessCaseItem = {
  id: string
  accessCaseId: string
  configurationItemId: string | null
  entitlementKey: string
  action: string
  isPrivileged: boolean
  isMandatory: boolean
  status: string
  fulfilledByUserId: string | null
  fulfilledAtUtc: string | null
  notes: string | null
  createdAtUtc: string
}

export type ExistingAccessItem = {
  id: string
  accessCaseId: string
  configurationItemId: string | null
  entitlementKey: string
  accessSummary: string | null
  createdAtUtc: string
}

export type AccessReviewCampaign = {
  id: string
  name: string
  type: string
  reviewerUserId: string
  startsAtUtc: string
  dueAtUtc: string
  status: string
  createdAtUtc: string
  updatedAtUtc: string
  itemCount: number
  pendingCount: number
  isOverdue: boolean
}

export type AccessReviewCampaignList = OpsPaged<AccessReviewCampaign> & {
  overdueCount: number
  pendingDecisionCount: number
}

export type AccessReviewItem = {
  id: string
  campaignId: string
  subjectUserId: string | null
  accountRecordId: string | null
  configurationItemId: string | null
  accessSummary: string
  decision: string
  reviewerComment: string | null
  reviewedAtUtc: string | null
  createdAtUtc: string
}

export type ManagedAccount = {
  id: string
  accountName: string
  type: string
  configurationItemId: string | null
  ownerUserId: string | null
  purpose: string
  status: string
  lastReviewedAtUtc: string | null
  createdAtUtc: string
  updatedAtUtc: string
  rowVersion: string
  isPrivileged: boolean
}

export type SodRule = {
  id: string
  name: string
  applicationConfigurationItemId: string | null
  leftEntitlementKey: string
  rightEntitlementKey: string
  severity: string
  isActive: boolean
  description: string | null
  createdAtUtc: string
  updatedAtUtc: string
}

export type AccessEvidenceProjection = {
  sourceType: string
  recordId: string
  businessNumber: string | null
  status: string
  periodStartUtc: string | null
  periodEndUtc: string | null
  createdAtUtc: string
  completedAtUtc: string | null
  approvals: string[]
  fulfillmentOrReviewDecisions: string[]
  linkedReferences: string[]
  actorHistorySummary: string[]
}

export const accessApi = {
  listCases: (params?: { page?: number; pageSize?: number; search?: string; type?: string; status?: string }) =>
    apiFetch<OpsPaged<AccessCase>>(`/api/v1/access/cases${opsQuery(params)}`),
  getCase: (id: string) => apiFetch<AccessCase>(`/api/v1/access/cases/${id}`),
  createCase: (payload: {
    type: string
    reason: string
    subjectUserId?: string | null
    subjectName?: string | null
    subjectEmail?: string | null
    designatedApproverUserId?: string | null
    effectiveAtUtc?: string | null
  }) =>
    apiFetch<AccessCase>('/api/v1/access/cases', { method: 'POST', body: JSON.stringify(payload) }),
  submit: (id: string) => apiFetch<AccessCase>(`/api/v1/access/cases/${id}/submit`, { method: 'POST' }),
  startApproval: (id: string) =>
    apiFetch<AccessCase>(`/api/v1/access/cases/${id}/start-approval`, { method: 'POST' }),
  approve: (id: string) => apiFetch<AccessCase>(`/api/v1/access/cases/${id}/approve`, { method: 'POST' }),
  reject: (id: string, reason?: string) =>
    apiFetch<AccessCase>(`/api/v1/access/cases/${id}/reject`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    }),
  startVerification: (id: string) =>
    apiFetch<AccessCase>(`/api/v1/access/cases/${id}/start-verification`, { method: 'POST' }),
  close: (id: string) => apiFetch<AccessCase>(`/api/v1/access/cases/${id}/close`, { method: 'POST' }),
  cancel: (id: string, reason?: string) =>
    apiFetch<AccessCase>(`/api/v1/access/cases/${id}/cancel`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    }),
  listItems: (id: string) => apiFetch<AccessCaseItem[]>(`/api/v1/access/cases/${id}/items`),
  addItem: (
    id: string,
    payload: {
      entitlementKey: string
      action: string
      configurationItemId?: string | null
      isPrivileged?: boolean
      isMandatory?: boolean
      notes?: string | null
    },
  ) =>
    apiFetch<AccessCaseItem>(`/api/v1/access/cases/${id}/items`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  completeItem: (id: string, itemId: string, reason?: string) =>
    apiFetch<AccessCaseItem>(`/api/v1/access/cases/${id}/items/${itemId}/complete`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    }),
  listExistingAccess: (id: string) =>
    apiFetch<ExistingAccessItem[]>(`/api/v1/access/cases/${id}/existing-access`),
  addExistingAccess: (
    id: string,
    payload: { entitlementKey: string; configurationItemId?: string | null; accessSummary?: string | null },
  ) =>
    apiFetch<ExistingAccessItem>(`/api/v1/access/cases/${id}/existing-access`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  confirmExistingAccess: (id: string) =>
    apiFetch<AccessCase>(`/api/v1/access/cases/${id}/confirm-existing-access`, { method: 'POST' }),
  prepareCaseEvidence: (id: string) =>
    apiFetch<AccessEvidenceProjection>(`/api/v1/access/cases/${id}/evidence`),
  listReviews: (params?: { page?: number; pageSize?: number; status?: string }) =>
    apiFetch<AccessReviewCampaignList>(`/api/v1/access/reviews${opsQuery(params)}`),
  createReview: (payload: {
    name: string
    type: string
    reviewerUserId: string
    startsAtUtc: string
    dueAtUtc: string
  }) =>
    apiFetch<AccessReviewCampaign>('/api/v1/access/reviews', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  openReview: (id: string) => apiFetch<AccessReviewCampaign>(`/api/v1/access/reviews/${id}/open`, { method: 'POST' }),
  completeReview: (id: string) =>
    apiFetch<AccessReviewCampaign>(`/api/v1/access/reviews/${id}/complete`, { method: 'POST' }),
  listReviewItems: (id: string) => apiFetch<AccessReviewItem[]>(`/api/v1/access/reviews/${id}/items`),
  addReviewItem: (id: string, payload: { accessSummary: string; subjectUserId?: string | null }) =>
    apiFetch<AccessReviewItem>(`/api/v1/access/reviews/${id}/items`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  decideReviewItem: (id: string, itemId: string, decision: string, comment?: string) =>
    apiFetch<AccessReviewItem>(`/api/v1/access/reviews/${id}/items/${itemId}/decide`, {
      method: 'POST',
      body: JSON.stringify({ decision, comment }),
    }),
  prepareReviewEvidence: (id: string) =>
    apiFetch<AccessEvidenceProjection>(`/api/v1/access/reviews/${id}/evidence`),
  listAccounts: (params?: { page?: number; pageSize?: number; search?: string; type?: string }) =>
    apiFetch<OpsPaged<ManagedAccount>>(`/api/v1/access/accounts${opsQuery(params)}`),
  createAccount: (payload: {
    accountName: string
    type: string
    purpose: string
    ownerUserId?: string | null
    configurationItemId?: string | null
  }) =>
    apiFetch<ManagedAccount>('/api/v1/access/accounts', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  listSod: (params?: { page?: number; pageSize?: number; activeOnly?: boolean }) =>
    apiFetch<OpsPaged<SodRule>>(
      `/api/v1/access/sod${opsQuery({
        page: params?.page,
        pageSize: params?.pageSize,
        activeOnly: params?.activeOnly === undefined ? undefined : String(params.activeOnly),
      })}`,
    ),
  createSod: (payload: {
    name: string
    leftEntitlementKey: string
    rightEntitlementKey: string
    severity: string
    description?: string | null
  }) =>
    apiFetch<SodRule>('/api/v1/access/sod', { method: 'POST', body: JSON.stringify(payload) }),
}

export type ManagedDocument = {
  id: string
  documentNumber: string
  title: string
  documentType: string
  ownerUserId: string
  designatedApproverUserId: string | null
  classification: string
  status: string
  currentVersionId: string | null
  effectiveDate: string | null
  reviewDate: string | null
  requiresAcknowledgement: boolean
  retirementReason: string | null
  createdAtUtc: string
  updatedAtUtc: string
  rowVersion: string
  daysToReview: number | null
  reviewDueSoon: boolean
  reviewOverdue: boolean
  currentVersionNumber: number | null
  currentAttachmentId: string | null
  currentApprovedByUserId: string | null
  currentApprovedAtUtc: string | null
  currentPublishedAtUtc: string | null
}

export type DocumentListResult = OpsPaged<ManagedDocument> & {
  reviewOverdueCount: number
  reviewDueSoonCount: number
}

export type DocumentVersion = {
  id: string
  managedDocumentId: string
  versionNumber: number
  createdByUserId: string
  createdAtUtc: string
  changeSummary: string | null
  attachmentId: string | null
  approvedByUserId: string | null
  approvedAtUtc: string | null
  publishedAtUtc: string | null
  supersedesVersionId: string | null
}

export type AcknowledgementSummary = {
  outstandingForUser: number
  totalOutstandingVersions: number
}

export const documentsApi = {
  list: (params?: {
    page?: number
    pageSize?: number
    search?: string
    type?: string
    status?: string
    reviewOverdueOnly?: boolean
  }) =>
    apiFetch<DocumentListResult>(
      `/api/v1/documents${opsQuery({
        page: params?.page,
        pageSize: params?.pageSize,
        search: params?.search,
        type: params?.type,
        status: params?.status,
        reviewOverdueOnly:
          params?.reviewOverdueOnly === undefined ? undefined : String(params.reviewOverdueOnly),
      })}`,
    ),
  get: (id: string) => apiFetch<ManagedDocument>(`/api/v1/documents/${id}`),
  create: (payload: {
    title: string
    documentType: string
    classification?: string
    ownerUserId?: string | null
    designatedApproverUserId?: string | null
    effectiveDate?: string | null
    reviewDate?: string | null
    requiresAcknowledgement?: boolean
    changeSummary?: string | null
  }) =>
    apiFetch<ManagedDocument>('/api/v1/documents', { method: 'POST', body: JSON.stringify(payload) }),
  listVersions: (id: string) => apiFetch<DocumentVersion[]>(`/api/v1/documents/${id}/versions`),
  submit: (id: string) => apiFetch<ManagedDocument>(`/api/v1/documents/${id}/submit`, { method: 'POST' }),
  approve: (id: string) => apiFetch<ManagedDocument>(`/api/v1/documents/${id}/approve`, { method: 'POST' }),
  returnToDraft: (id: string, reason?: string) =>
    apiFetch<ManagedDocument>(`/api/v1/documents/${id}/return`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    }),
  publish: (id: string) => apiFetch<ManagedDocument>(`/api/v1/documents/${id}/publish`, { method: 'POST' }),
  retire: (id: string, reason: string) =>
    apiFetch<ManagedDocument>(`/api/v1/documents/${id}/retire`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    }),
  createRevision: (id: string, changeSummary?: string) =>
    apiFetch<DocumentVersion>(`/api/v1/documents/${id}/revisions`, {
      method: 'POST',
      body: JSON.stringify({ changeSummary }),
    }),
  uploadAttachment: async (id: string, file: File) => {
    const form = new FormData()
    form.append('file', file)
    return apiFetch<{ attachmentId: string; fileName: string }>(`/api/v1/documents/${id}/attachments`, {
      method: 'POST',
      body: form,
    })
  },
}

export const policiesApi = {
  list: (params?: {
    page?: number
    pageSize?: number
    search?: string
    status?: string
    reviewOverdueOnly?: boolean
  }) =>
    apiFetch<DocumentListResult>(
      `/api/v1/policies${opsQuery({
        page: params?.page,
        pageSize: params?.pageSize,
        search: params?.search,
        status: params?.status,
        reviewOverdueOnly:
          params?.reviewOverdueOnly === undefined ? undefined : String(params.reviewOverdueOnly),
      })}`,
    ),
  get: (id: string) => apiFetch<ManagedDocument>(`/api/v1/policies/${id}`),
  create: (payload: {
    title: string
    classification?: string
    designatedApproverUserId?: string | null
    reviewDate?: string | null
    requiresAcknowledgement?: boolean
  }) =>
    apiFetch<ManagedDocument>('/api/v1/policies', { method: 'POST', body: JSON.stringify(payload) }),
  seedCatalog: () => apiFetch<{ seeded: boolean }>('/api/v1/policies/seed-catalog', { method: 'POST' }),
  listVersions: (id: string) => apiFetch<DocumentVersion[]>(`/api/v1/policies/${id}/versions`),
  submit: (id: string) => apiFetch<ManagedDocument>(`/api/v1/policies/${id}/submit`, { method: 'POST' }),
  approve: (id: string) => apiFetch<ManagedDocument>(`/api/v1/policies/${id}/approve`, { method: 'POST' }),
  returnToDraft: (id: string, reason?: string) =>
    apiFetch<ManagedDocument>(`/api/v1/policies/${id}/return`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    }),
  publish: (id: string) => apiFetch<ManagedDocument>(`/api/v1/policies/${id}/publish`, { method: 'POST' }),
  acknowledge: (id: string) =>
    apiFetch(`/api/v1/policies/${id}/acknowledge`, { method: 'POST' }),
  outstanding: () => apiFetch<ManagedDocument[]>('/api/v1/me/policies/outstanding'),
  summary: () => apiFetch<AcknowledgementSummary>('/api/v1/me/policies/summary'),
}

export type TicketDashboard = {
  openTickets: number
  unassigned: number
  criticalOpen: number
  slaBreached: number
  myAssigned: number
  newToday: number
  resolvedToday: number
  byStatus: Record<string, number>
  byPriority: Record<string, number>
}

export type KnowledgeArticle = {
  id: string
  title: string
  slug: string
  summary: string | null
  body: string
  status: string
  createdByUserId: string
  updatedByUserId: string
  createdAtUtc: string
  updatedAtUtc: string
  publishedAtUtc: string | null
}

export type UpsertKnowledgeArticlePayload = {
  title: string
  slug: string
  body: string
  summary?: string | null
}

export const kbApi = {
  listPublished: (search?: string) => {
    const query = search?.trim() ? `?search=${encodeURIComponent(search.trim())}` : ''
    return apiFetch<KnowledgeArticle[]>(`/api/v1/kb${query}`)
  },
  getPublished: (slug: string) => apiFetch<KnowledgeArticle>(`/api/v1/kb/${encodeURIComponent(slug)}`),
  listAdmin: (params?: { status?: string; search?: string }) => {
    const query = new URLSearchParams()
    if (params?.status?.trim()) query.set('status', params.status.trim())
    if (params?.search?.trim()) query.set('search', params.search.trim())
    const qs = query.toString()
    return apiFetch<KnowledgeArticle[]>(`/api/v1/kb/admin${qs ? `?${qs}` : ''}`)
  },
  getAdmin: (id: string) => apiFetch<KnowledgeArticle>(`/api/v1/kb/admin/${id}`),
  create: (payload: UpsertKnowledgeArticlePayload) =>
    apiFetch<KnowledgeArticle>('/api/v1/kb/admin', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  update: (id: string, payload: UpsertKnowledgeArticlePayload) =>
    apiFetch<KnowledgeArticle>(`/api/v1/kb/admin/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    }),
  publish: (id: string) =>
    apiFetch<KnowledgeArticle>(`/api/v1/kb/admin/${id}/publish`, { method: 'POST' }),
  archive: (id: string) =>
    apiFetch<KnowledgeArticle>(`/api/v1/kb/admin/${id}/archive`, { method: 'POST' }),
}

/** @deprecated Prefer relative same-origin requests through the Vite proxy. */
export const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? ''
