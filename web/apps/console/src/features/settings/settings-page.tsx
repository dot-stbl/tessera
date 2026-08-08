import { useQuery } from '@tanstack/react-query';
import { api, usingMockData } from '@/shared/api';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Panel, PanelRow, PanelValue, Seg, Tag } from '@/shared/ui/console';
import { Button } from '@/shared/ui/primitives/button';
import { useDocumentTitle } from '@/shared/lib/use-document-title';
import {
  usePreferences,
  type FontSize,
  type Language,
  type Theme,
} from '@/shared/lib/preferences-provider';

const THEMES: readonly Theme[] = ['dark', 'light', 'system'];
const FONT_SIZES: readonly FontSize[] = ['small', 'medium', 'large'];
const LANGUAGES: readonly Language[] = ['en', 'ru'];

/**
 * Settings is deliberately small and entirely real: three preferences that take
 * effect on click, and an honest statement of where the data on every other
 * screen came from. Nothing here is a placeholder for a future control — a
 * settings screen full of dead switches is worse than a short one.
 */
export function SettingsPage() {
  useDocumentTitle('Settings');
  const { preferences, update, reset } = usePreferences();

  return (
    <PageTemplate title="Settings" subtitle="Appearance and data source">
      <Panel legend="Appearance">
        <PanelRow label="Theme" hint="Dark is the default; the console is designed for it.">
          <Seg
            label="Theme"
            value={preferences.theme}
            options={THEMES}
            onChange={(next) => update('theme', next)}
          />
        </PanelRow>

        <PanelRow label="Text size" hint="Scales the whole interface, rows included.">
          <Seg
            label="Text size"
            value={preferences.fontSize}
            options={FONT_SIZES}
            onChange={(next) => update('fontSize', next)}
          />
        </PanelRow>

        <PanelRow label="Language" hint="Applies to interface copy only, never to your data.">
          <Seg
            label="Language"
            value={preferences.language}
            options={LANGUAGES}
            onChange={(next) => update('language', next)}
          />
        </PanelRow>

        <PanelRow hint="Returns theme, text size and language to their defaults.">
          <Button variant="outline" className="settings-reset" onClick={reset}>
            Reset preferences
          </Button>
        </PanelRow>
      </Panel>

      <Panel legend="Data source">
        <SourceRows />
      </Panel>
    </PageTemplate>
  );
}

function SourceRows() {
  // Only ask the backend when there is one. In mock mode the query would
  // resolve against fixtures and report a healthy provider that does not exist.
  const health = useQuery({
    queryKey: ['health'],
    queryFn: ({ signal }) => api.getHealth(signal),
    enabled: !usingMockData,
    refetchInterval: 30_000,
  });

  if (usingMockData) {
    return (
      <>
        <PanelRow
          label="Mode"
          hint="Every screen is running on bundled fixtures. No request leaves the browser."
        >
          <Tag tone="unset">fixtures</Tag>
        </PanelRow>
        <PanelRow label="Backend" hint="Set VITE_API_BASE_URL and reload to connect to a host.">
          <PanelValue>not configured</PanelValue>
        </PanelRow>
      </>
    );
  }

  const status = health.data?.status;
  const tone = status === 'healthy' ? 'ok' : status === undefined ? 'unset' : 'error';

  return (
    <>
      <PanelRow label="Mode" hint="Reading live data over HTTP.">
        <Tag tone="ok">live</Tag>
      </PanelRow>
      <PanelRow label="Provider" hint="The backend the console is pointed at.">
        <PanelValue>{health.data?.provider ?? '—'}</PanelValue>
      </PanelRow>
      <PanelRow label="Status" hint={health.data?.detail ?? 'Re-checked every 30 seconds.'}>
        <Tag tone={health.isError ? 'error' : tone}>
          {health.isError ? 'unreachable' : (status ?? 'checking')}
        </Tag>
      </PanelRow>
    </>
  );
}
