import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';

/**
 * The facet sidebar.
 *
 * It carries what narrows the current screen — services, levels, statuses —
 * and nothing else. That is the entire justification for its 200px: a second
 * copy of the section list would not earn them, and the previous shell spent
 * exactly that much on one.
 *
 * Like the spine, the chrome renders and the screen supplies. A screen with
 * nothing to facet says so, rather than the sidebar guessing.
 */

export interface Facet {
  /** The value written into the filter, e.g. a service name. */
  value: string;
  /** How many rows in the current window carry it. */
  count: number;
}

export interface FacetGroup {
  /** The field being narrowed: "service", "level", "status". */
  field: string;
  facets: Facet[];
  selected: readonly string[];
  onToggle: (value: string) => void;
  onClear?: () => void;
}

interface SideValue {
  groups: FacetGroup[] | null;
  publish: (groups: FacetGroup[] | null) => void;
}

const SideContext = createContext<SideValue | null>(null);

export function SideProvider({ children }: { children: ReactNode }) {
  const [groups, setGroups] = useState<FacetGroup[] | null>(null);
  return (
    <SideContext.Provider value={{ groups, publish: setGroups }}>{children}</SideContext.Provider>
  );
}

function useSide(): SideValue {
  const ctx = useContext(SideContext);
  if (ctx === null) throw new Error('useSide must be used within a SideProvider');
  return ctx;
}

/** Hand the sidebar this screen's facets. Cleared on unmount. */
export function usePublishFacets(groups: FacetGroup[] | null): void {
  const { publish } = useSide();
  useEffect(() => {
    publish(groups);
    return () => publish(null);
  }, [publish, groups]);
}

export function Side() {
  const { groups } = useSide();

  return (
    <nav className="side" aria-label="Filters">
      {groups === null || groups.length === 0 ? (
        <p className="side-empty prose">Nothing to narrow on this screen.</p>
      ) : (
        groups.map((group) => <FacetList key={group.field} group={group} />)
      )}
    </nav>
  );
}

function FacetList({ group }: { group: FacetGroup }) {
  const hasSelection = group.selected.length > 0;

  return (
    <section className="side-group">
      <header className="side-legend">
        <span className="label">{group.field}</span>
        {hasSelection && group.onClear && (
          <button type="button" className="side-clear" onClick={group.onClear}>
            clear
          </button>
        )}
      </header>

      {group.facets.length === 0 ? (
        <p className="side-empty prose">No values in this window.</p>
      ) : (
        group.facets.map((facet) => {
          const on = group.selected.includes(facet.value);
          return (
            <button
              key={facet.value}
              type="button"
              className="side-item"
              aria-pressed={on}
              onClick={() => group.onToggle(facet.value)}
              title={`${facet.value} · ${facet.count}`}
            >
              <span className="side-item-name">{facet.value}</span>
              <span className="side-item-count">{facet.count.toLocaleString()}</span>
            </button>
          );
        })
      )}
    </section>
  );
}
