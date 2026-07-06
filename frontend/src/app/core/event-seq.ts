import type { EventDto } from './types';

/**
 * Filter out events the renderer already ingested. Snapshots re-send a short
 * replay window of events, so a dropped or coalesced snapshot no longer loses FX.
 */
export function takeNewEvents(
  events: EventDto[],
  lastSeq: number,
): { fresh: EventDto[]; lastSeq: number } {
  let cursor = lastSeq;
  const fresh: EventDto[] = [];
  for (const ev of events) {
    if (ev.seq > cursor) {
      fresh.push(ev);
      cursor = ev.seq;
    }
  }
  return { fresh, lastSeq: cursor };
}
