/**
 * The console layer: Tessera's screens are assembled from these, never from raw
 * markup. Each is a thin skin over a vendored shadcn/Base UI primitive, except
 * where the thing has no generic equivalent — a time gutter, a latency track, a
 * service swatch, a listing-shaped skeleton. Those are domain components and
 * say so in their own files.
 */

export {
  Listing,
  ListingHead,
  ListingBody,
  Column,
  Row,
  Cell,
  WhenCell,
  NumCell,
  TrackCell,
} from './listing';
export type { ColumnProps, RowProps } from './listing';

export { Subject, ServiceMark, ServiceDots, InlineList, Tag } from './marks';

export { Blank, BlankText, Code } from './blank';

export { Notice } from './notice';

export { Strip, StripSpacer, Seg, Chip, Meta } from './strip';
export type { SegProps, ChipProps } from './strip';

export { Panel, PanelRow, PanelValue, LoadingRows } from './panel';

export { StatBar } from './stats';
export type { StatItem } from './stats';
