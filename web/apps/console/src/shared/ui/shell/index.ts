/**
 * The shell: bar, spine, sidebar, main. Screens import `usePublishSpine` (from
 * shared/lib/time-window) and `usePublishFacets` to feed the chrome; they never
 * render it themselves.
 */
export { Shell } from './shell';
export { Mark } from './mark';
export { Spine } from './spine';
export { Side, SideProvider, usePublishFacets } from './side';
export type { Facet, FacetGroup } from './side';
