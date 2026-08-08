/**
 * The Tessera mark: four tiles, one of them the event you were looking for.
 *
 * This is the canonical mark from `assets/mark-tessera.svg`, not a new one. Its
 * recorded meaning is the whole design system in one sentence — a field of
 * identical tiles and one that is different — so the console is built to look
 * like its own logo rather than the logo being decoration on top of a console.
 *
 * The odd tile is bound to `--found`, the same token that colours a failed
 * span, a red timestamp and the error band in the spine. That is deliberate:
 * the red in the mark is not a brand colour, it is what "found it" looks like,
 * and it should change with the thing it names.
 */
export function Mark({ size = 16 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 32 32" aria-hidden="true">
      <rect x="9" y="3" width="6" height="6" fill="currentColor" />
      <rect x="21" y="14" width="6" height="6" fill="currentColor" />
      <rect x="5" y="22" width="6" height="6" fill="currentColor" />
      <rect x="12" y="22" width="6" height="6" fill="var(--found)" />
    </svg>
  );
}
