export const READINESS_FRAMEWORK_ALIASES: Record<string, string> = {
  'isa-315': 'ISA315-IT-READINESS',
  isa315: 'ISA315-IT-READINESS',
  cyber: 'QEC-CYBER-READINESS',
}

export const READINESS_PROFILE_CODES = {
  isa315: 'ISA315-IT-READINESS',
  cyber: 'QEC-CYBER-READINESS',
} as const

export const READINESS_STATES = [
  'ReadyForReview',
  'AssessedNeedsEvidence',
  'MappedNeedsAssessment',
  'Unmapped',
  'NotApplicable',
] as const

export type ReadinessState = (typeof READINESS_STATES)[number]

export function resolveReadinessFrameworkCode(raw: string | undefined | null): string {
  const key = (raw ?? '').trim()
  if (!key) return ''
  const alias = READINESS_FRAMEWORK_ALIASES[key.toLowerCase()]
  return alias ?? key.toUpperCase()
}

export function periodBoundsForYear(year: number): { periodStart: string; periodEnd: string } {
  return {
    periodStart: `${year}-01-01`,
    periodEnd: `${year}-12-31`,
  }
}

export function currentPeriodYear(now = new Date()): number {
  return now.getFullYear()
}

export function formatPercent(value: number | null | undefined): string {
  if (typeof value !== 'number' || !Number.isFinite(value)) return '0%'
  const rounded = Math.round(value * 10) / 10
  return `${Number.isInteger(rounded) ? rounded.toFixed(0) : rounded.toFixed(1)}%`
}

export function readinessStateLabelKey(state: string): string {
  switch (state) {
    case 'ReadyForReview':
      return 'readiness.state.readyForReview'
    case 'AssessedNeedsEvidence':
      return 'readiness.state.needsEvidence'
    case 'MappedNeedsAssessment':
      return 'readiness.state.needsAssessment'
    case 'Unmapped':
      return 'readiness.state.unmapped'
    case 'NotApplicable':
      return 'readiness.state.notApplicable'
    default:
      return 'readiness.state.unknown'
  }
}

export function readinessStateBadgeVariant(
  state: string,
): 'default' | 'secondary' | 'outline' | 'success' | 'warning' {
  switch (state) {
    case 'ReadyForReview':
      return 'success'
    case 'AssessedNeedsEvidence':
    case 'MappedNeedsAssessment':
      return 'warning'
    case 'Unmapped':
      return 'outline'
    case 'NotApplicable':
      return 'secondary'
    default:
      return 'outline'
  }
}

export function isReadinessDashboardProfile(profileType: string | null | undefined): boolean {
  return profileType === 'AuditReadiness' || profileType === 'CybersecurityReadiness'
}

export function operationalLinkTitle(
  link: { titleEn: string; titleAr: string },
  language: string,
): string {
  if (language.startsWith('ar') && link.titleAr.trim()) return link.titleAr
  return link.titleEn || link.titleAr
}

export function safeInternalRoute(route: string | null | undefined): string | null {
  if (!route) return null
  const trimmed = route.trim()
  if (!trimmed.startsWith('/')) return null
  if (trimmed.startsWith('//')) return null
  if (/^[a-z][a-z0-9+.-]*:/i.test(trimmed)) return null
  return trimmed
}
