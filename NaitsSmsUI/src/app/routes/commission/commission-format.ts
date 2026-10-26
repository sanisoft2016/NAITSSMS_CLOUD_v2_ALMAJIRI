/** Attendance colour bands used across Commission pages (same as the Programme overview). */
export function rateTone(rate: number | null | undefined): string {
  const value = Number(rate ?? 0);
  if (value >= 85) return 'tone-good';
  if (value >= 70) return 'tone-warn';
  return 'tone-bad';
}

/** Score colour bands: 60+ is a B or better, below the 40 pass mark is a fail. */
export function scoreTone(score: number | null | undefined): string {
  const value = Number(score ?? 0);
  if (value >= 60) return 'tone-good';
  if (value >= 40) return 'tone-warn';
  return 'tone-bad';
}

export function barTone(value: number, goodAt: number, warnAt: number): string {
  if (value >= goodAt) return 'bar-row__fill--good';
  if (value >= warnAt) return 'bar-row__fill--warn';
  return 'bar-row__fill--bad';
}

export function percentOf(part: number, whole: number): number {
  return whole > 0 ? Math.round((part * 1000) / whole) / 10 : 0;
}

/** Signed one-decimal difference, or null when there is nothing to compare with. */
export function delta(current: number | null | undefined, previous: number | null | undefined): number | null {
  if (current == null || previous == null) {
    return null;
  }
  return Math.round((current - previous) * 10) / 10;
}

/**
 * SVG polyline points for a percentage series drawn into a width × height box, scaled from
 * `floor` to 100. Kept dependency-free so
 * the centre page renders instantly and prints cleanly.
 */
export function sparkline(values: number[], width: number, height: number, floor = 0): string {
  if (values.length === 0) {
    return '';
  }
  const step = values.length > 1 ? width / (values.length - 1) : 0;
  return values
    .map((value, index) => {
      const x = Math.round(index * step * 10) / 10;
      const clamped = Math.max(floor, Math.min(100, value));
      const y = Math.round((height - ((clamped - floor) / (100 - floor)) * height) * 10) / 10;
      return `${x},${y}`;
    })
    .join(' ');
}
