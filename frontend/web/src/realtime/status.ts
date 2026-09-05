/**
 * Shared connection state for realtime (SignalR) backed panels.
 *
 * `polling` means the hub could not be reached and the caller fell back to
 * periodic REST reloads, so the UI stays functional either way.
 */
export type RealtimeStatus = 'idle' | 'connecting' | 'live' | 'polling'

export function realtimeStatusLabelKey(status: RealtimeStatus): string {
  switch (status) {
    case 'live':
      return 'remote.chat.realtime.live'
    case 'polling':
      return 'remote.chat.realtime.polling'
    case 'connecting':
      return 'remote.chat.realtime.connecting'
    default:
      return 'remote.chat.realtime.idle'
  }
}
