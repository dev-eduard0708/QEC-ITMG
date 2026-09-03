export const adminKeys = {
  all: ['admin'] as const,
  users: (search: string) => [...adminKeys.all, 'users', search] as const,
  roles: () => [...adminKeys.all, 'roles'] as const,
  role: (id: string) => [...adminKeys.all, 'roles', id] as const,
  permissions: () => [...adminKeys.all, 'permissions'] as const,
  lookups: (kind: 'departments' | 'locations') => [...adminKeys.all, 'lookups', kind] as const,
}
