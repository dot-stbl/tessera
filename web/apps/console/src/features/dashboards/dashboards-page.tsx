import { useTranslation } from 'react-i18next';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Blank, BlankText, Code } from '@/shared/ui/console';
import { useDocumentTitle } from '@/shared/lib/use-document-title';

export function DashboardsPage() {
  const { t } = useTranslation();
  useDocumentTitle('Dashboards');

  return (
    <PageTemplate title={t('dashboards.title')} subtitle="Defined as code, read from disk">
      {/* Deliberately not a mocked dashboard grid. Dashboards are the one
       * surface whose shape is still being decided (typed YAML, checked into
       * the repo), and a convincing fake here would be the exact lie §6 of the
       * design system warns about — a screen promising something the API
       * cannot do. It says what will exist and where it will come from. */}
      <Blank title="No dashboards are loaded.">
        <BlankText>
          Dashboards are files, not database rows: each is a typed YAML document under{' '}
          <Code>data/dashboards/</Code>, versioned with the service it watches. The console reads
          them, it does not author them.
        </BlankText>
        <BlankText>
          Nothing renders here until the backend serves <Code>GET /api/v1/dashboards</Code>. Until
          then, Services carries rate, errors and latency per service.
        </BlankText>
      </Blank>
    </PageTemplate>
  );
}
