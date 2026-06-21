export const FARM_RUN_COUNT_STORAGE_KEY = 'kaezan:farm-run-count';

export function normalizeFarmRunCount(value: number, min = 1, max = 5): number {
  if (!Number.isFinite(value)) return min;
  return Math.min(max, Math.max(min, Math.round(value)));
}

export function readFarmRunCount(min = 1, max = 5): number {
  return normalizeFarmRunCount(Number(localStorage.getItem(FARM_RUN_COUNT_STORAGE_KEY) ?? min), min, max);
}

export function writeFarmRunCount(count: number, min = 1, max = 5): void {
  localStorage.setItem(FARM_RUN_COUNT_STORAGE_KEY, String(normalizeFarmRunCount(count, min, max)));
}
