const CHAT_RETENTION_DAYS = 7

/** Mirrors the API rule: session chat closes a week after the session ends. */
export function isChatOpen(session: { status: string; endedAtUtc: string | null }): boolean {
  if (session.status !== 'Ended' || !session.endedAtUtc) return true
  const endedAt = Date.parse(session.endedAtUtc)
  if (Number.isNaN(endedAt)) return true
  return Date.now() - endedAt < CHAT_RETENTION_DAYS * 24 * 60 * 60 * 1000
}
