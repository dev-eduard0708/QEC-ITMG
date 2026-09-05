import type {
  RemoteDeviceReadinessStatus,
  RemoteOnboardingOverallStatus,
} from '@/api/client'

export type FriendlyBadgeVariant = 'default' | 'secondary' | 'success' | 'warning' | 'outline'

/** Employee-facing session status wording (no engine/vendor jargon). */
export function friendlySessionStatusKey(status: string): string {
  switch (status) {
    case 'NotifyUser':
      return 'employee.remote.sessionStatus.needsApproval'
    case 'Requested':
      return 'employee.remote.sessionStatus.waitingForTechnician'
    case 'Allowed':
    case 'Authorized':
      return 'employee.remote.sessionStatus.waitingForIt'
    case 'Connecting':
      return 'employee.remote.sessionStatus.connecting'
    case 'InSession':
      return 'employee.remote.sessionStatus.active'
    case 'Ended':
      return 'employee.remote.sessionStatus.finished'
    case 'Declined':
      return 'employee.remote.sessionStatus.declined'
    case 'Expired':
      return 'employee.remote.sessionStatus.expired'
    default:
      return 'employee.remote.sessionStatus.unknown'
  }
}

export function sessionStatusVariant(status: string): FriendlyBadgeVariant {
  switch (status) {
    case 'InSession':
      return 'success'
    case 'NotifyUser':
    case 'Requested':
      return 'warning'
    case 'Connecting':
    case 'Allowed':
    case 'Authorized':
      return 'secondary'
    case 'Declined':
    case 'Expired':
      return 'outline'
    default:
      return 'outline'
  }
}

export function deviceReadinessVariant(status: RemoteDeviceReadinessStatus): FriendlyBadgeVariant {
  switch (status) {
    case 'Ready':
      return 'success'
    case 'SetupRequired':
      return 'warning'
    case 'WaitingForIt':
      return 'secondary'
    default:
      return 'outline'
  }
}

export function overallStatusVariant(status: RemoteOnboardingOverallStatus): FriendlyBadgeVariant {
  switch (status) {
    case 'Ready':
      return 'success'
    case 'SetupRequired':
      return 'warning'
    case 'WaitingForIt':
      return 'secondary'
    default:
      return 'outline'
  }
}

/** True when the employee can act (install the agent) to make a device usable. */
export function needsEmployeeSetup(status: RemoteOnboardingOverallStatus): boolean {
  return status === 'SetupRequired'
}
