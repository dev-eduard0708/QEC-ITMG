import type { AccessCase } from '@/api/client'

export type AccessFriendlyStatusKey =
  | 'draft'
  | 'waitingApproval'
  | 'changesRequested'
  | 'inFulfillment'
  | 'waitingVerification'
  | 'readyToClose'
  | 'completed'
  | 'rejected'
  | 'cancelled'
  | 'submitted'
  | 'unknown'

export type AccessFriendlyStatus = {
  key: AccessFriendlyStatusKey
  /** i18n key under access.friendlyStatus.* */
  labelKey: string
  /** Badge visual tone */
  tone: 'default' | 'secondary' | 'outline' | 'success' | 'warning'
}

/** Map raw case status (+ ready-to-close) to a user-facing workflow label. */
export function getAccessFriendlyStatus(
  accessCase: Pick<AccessCase, 'status' | 'isReadyToClose'>,
): AccessFriendlyStatus {
  const status = accessCase.status

  if (status === 'Draft') {
    return { key: 'draft', labelKey: 'access.friendlyStatus.draft', tone: 'warning' }
  }
  if (status === 'Rework') {
    return {
      key: 'changesRequested',
      labelKey: 'access.friendlyStatus.changesRequested',
      tone: 'warning',
    }
  }
  if (status === 'Rejected') {
    return { key: 'rejected', labelKey: 'access.friendlyStatus.rejected', tone: 'secondary' }
  }
  if (status === 'Cancelled') {
    return { key: 'cancelled', labelKey: 'access.friendlyStatus.cancelled', tone: 'secondary' }
  }
  if (status === 'Closed') {
    return { key: 'completed', labelKey: 'access.friendlyStatus.completed', tone: 'success' }
  }
  if (status === 'Verification' && accessCase.isReadyToClose) {
    return { key: 'readyToClose', labelKey: 'access.friendlyStatus.readyToClose', tone: 'success' }
  }
  if (status === 'Verification') {
    return {
      key: 'waitingVerification',
      labelKey: 'access.friendlyStatus.waitingVerification',
      tone: 'outline',
    }
  }
  if (status === 'Fulfillment') {
    return { key: 'inFulfillment', labelKey: 'access.friendlyStatus.inFulfillment', tone: 'outline' }
  }
  if (status === 'Approval' || status === 'Submitted') {
    return {
      key: status === 'Submitted' ? 'submitted' : 'waitingApproval',
      labelKey:
        status === 'Submitted'
          ? 'access.friendlyStatus.submitted'
          : 'access.friendlyStatus.waitingApproval',
      tone: 'outline',
    }
  }

  return { key: 'unknown', labelKey: 'access.friendlyStatus.unknown', tone: 'secondary' }
}

export type WorkflowStepId = 'request' | 'approval' | 'fulfillment' | 'verification' | 'closure'

export type WorkflowStepState = 'completed' | 'current' | 'upcoming' | 'problem'

export type WorkflowStep = {
  id: WorkflowStepId
  labelKey: string
  state: WorkflowStepState
}

/** Five-step lifecycle only — fallback verifier is metadata under Verification. */
export function getAccessWorkflowSteps(
  accessCase: Pick<AccessCase, 'status' | 'isReadyToClose' | 'verificationOutcome'>,
): WorkflowStep[] {
  const status = accessCase.status
  const problem =
    status === 'Rejected' ||
    status === 'Cancelled' ||
    status === 'Rework' ||
    accessCase.verificationOutcome === 'Problem'

  const order: WorkflowStepId[] = [
    'request',
    'approval',
    'fulfillment',
    'verification',
    'closure',
  ]

  let currentIndex = 0
  if (status === 'Draft') currentIndex = 0
  else if (status === 'Submitted' || status === 'Approval' || status === 'Rework') currentIndex = 1
  else if (status === 'Fulfillment') currentIndex = 2
  else if (status === 'Verification' && !accessCase.isReadyToClose) currentIndex = 3
  else if (status === 'Verification' && accessCase.isReadyToClose) currentIndex = 4
  else if (status === 'Closed') currentIndex = 5
  else if (status === 'Rejected') currentIndex = 1
  else if (status === 'Cancelled') currentIndex = 0

  return order.map((id, index) => {
    let state: WorkflowStepState = 'upcoming'
    if (status === 'Closed') {
      state = 'completed'
    } else if (problem && index === currentIndex) {
      state = 'problem'
    } else if (index < currentIndex) {
      state = 'completed'
    } else if (index === currentIndex) {
      state = 'current'
    }
    return {
      id,
      labelKey: `access.workflowSteps.${id}`,
      state,
    }
  })
}
