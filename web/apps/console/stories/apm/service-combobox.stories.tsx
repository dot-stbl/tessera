import { useState } from 'react';
import type { Story, StoryDefault } from '@ladle/react';
import { ServiceComboBox, type ServiceOption } from '@/shared/ui/apm/service-combobox';

export default {
  title: 'APM / ServiceComboBox',
} satisfies StoryDefault;

const services: ServiceOption[] = [
  { name: 'checkout-api', spanCount: 8421 },
  { name: 'postgres', spanCount: 12043 },
  { name: 'redis', spanCount: 5310 },
  { name: 'stripe', spanCount: 1290 },
  { name: 'kafka', spanCount: 3877 },
  { name: 'user-svc', spanCount: 6620 },
  { name: 'feed-svc', spanCount: 4110 },
  { name: 'search-svc', spanCount: 980 },
  { name: 'inventory-svc', spanCount: 2210 },
  { name: 'notify-svc', spanCount: 640 },
];

/** Searchable service selector with span counts. Type to filter. */
export const Default: Story = () => {
  const [value, setValue] = useState<string | null>(null);
  return (
    <div className="flex flex-col gap-3 p-6">
      <ServiceComboBox services={services} value={value} onChange={setValue} />
      <p className="text-xs text-muted-foreground">
        Selected: <code className="font-mono text-foreground">{value ?? '—'}</code>
      </p>
    </div>
  );
};

/** Without span counts — just names. */
export const NoCounts: Story = () => {
  const [value, setValue] = useState<string | null>('postgres');
  return (
    <div className="p-6">
      <ServiceComboBox
        services={services.map((s) => ({ name: s.name }))}
        value={value}
        onChange={setValue}
      />
    </div>
  );
};
