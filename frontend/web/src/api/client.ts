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

export type ChangeHistoryEntry = {
  id: string
  fromStatus: string
  toStatus: string
  changedByUserId: string
  comment: string | null
  changedAtUtc: string
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
  }) =>
    apiFetch<ChangeRequest>('/api/v1/changes', {
      method: 'POST',
      body: JSON.stringify(payload),
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
  listHistory: (id: string) => apiFetch<ChangeHistoryEntry[]>(`/api/v1/changes/${id}/history`),
  transition: (
    id: string,
    payload: {
      targetStatus: string
      comment?: string | null
      validationNotes?: string | null
      pirNotes?: string | null
      result?: string | null
      rowVersion?: string | null
    },
  ) =>
    apiFetch<ChangeRequest>(`/api/v1/changes/${id}/transition`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
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
