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
  const response = await fetch(path, {
    ...init,
    credentials: 'include',
    headers: {
      Accept: 'application/json',
      ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
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
}

/** @deprecated Prefer relative same-origin requests through the Vite proxy. */
export const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? ''
