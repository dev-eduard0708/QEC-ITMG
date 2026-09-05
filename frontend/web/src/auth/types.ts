export type CurrentUserRole = {
  id: string
  name: string
}

export type CurrentUser = {
  id: string
  upn: string
  displayName: string
  userType: string
  timeZone: string | null
  authMethod: string
  roles: CurrentUserRole[]
  permissions: string[]
  avatarUrl?: string | null
}

export type AuthSession = {
  user: CurrentUser | null
  isAuthenticated: boolean
  isLoading: boolean
}
