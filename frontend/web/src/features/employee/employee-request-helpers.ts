/** Employee-facing labels for internal ticket fields (display only). */

export type TicketImpactChoice = 'can_work' | 'difficult' | 'cannot_work' | 'several_people'

export const IMPACT_TO_PRIORITY: Record<TicketImpactChoice, 'Low' | 'Medium' | 'High'> = {
  can_work: 'Low',
  difficult: 'Medium',
  cannot_work: 'High',
  several_people: 'High',
}

export const EMPLOYEE_CATEGORIES = [
  'computer',
  'account',
  'email',
  'internet',
  'printer',
  'software',
  'access',
  'equipment',
  'phone',
  'remote',
  'other',
] as const

export type EmployeeCategory = (typeof EMPLOYEE_CATEGORIES)[number]

export function friendlyTicketTypeKey(type: string): string {
  if (type === 'Incident') return 'employee.types.notWorking'
  return 'employee.types.needSomething'
}

export function friendlyStatusKey(status: string): string {
  switch (status) {
    case 'New':
      return 'employee.status.received'
    case 'Assigned':
    case 'InProgress':
      return 'employee.status.working'
    case 'PendingCustomer':
      return 'employee.status.waitingReply'
    case 'PendingVendor':
    case 'PendingChange':
      return 'employee.status.waitingOther'
    case 'Resolved':
      return 'employee.status.resolved'
    case 'Closed':
      return 'employee.status.closed'
    case 'Cancelled':
      return 'employee.status.cancelled'
    default:
      return 'employee.status.received'
  }
}

export function isOpenTicketStatus(status: string): boolean {
  return !['Resolved', 'Closed', 'Cancelled'].includes(status)
}

export function isWaitingForEmployee(status: string): boolean {
  return status === 'PendingCustomer'
}

export function categoryLabelKey(category: string | null | undefined): string | null {
  if (!category) return null
  const key = category.trim().toLowerCase()
  if ((EMPLOYEE_CATEGORIES as readonly string[]).includes(key)) {
    return `employee.category.${key}`
  }
  return null
}

export function formatDeviceLabel(asset: {
  name: string
  manufacturer: string | null
  model: string | null
  assetType: string
}): string {
  const model = [asset.manufacturer, asset.model].filter(Boolean).join(' ')
  if (model) return `${asset.name} — ${model}`
  return asset.name
}
