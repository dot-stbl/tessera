import { useTranslation } from 'react-i18next';
import { PageTemplate } from '@/shared/ui/app-shell';
import { useDocumentTitle } from '@/shared/lib/use-document-title';

export function DashboardsPage() {
  const { t } = useTranslation();
  useDocumentTitle('Dashboards');

  return (
    <PageTemplate
      title={t('dashboards.title')}
      subtitle="Custom Grafana-style dashboards backed by Victoria"
    >
      <div className="rounded-md border border-border bg-card p-8 text-center text-sm text-muted-foreground">
        {t('dashboards.empty.title')}
        <p className="mt-2 text-xs text-muted-foreground">
          Dashboard list endpoint not yet implemented.
          Backend will expose <code className="rounded bg-surface-2 px-1 font-mono text-xs">GET /api/dashboards</code>{' '}
          returning dashboards from <code className="rounded bg-surface-2 px-1 font-mono text-xs">data/dashboards/</code>.
        </p>
      </div>
    </PageTemplate>
  );
}