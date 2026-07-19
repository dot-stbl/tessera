import { cn } from '@/shared/lib/utils';
import {
  Combobox,
  ComboboxContent,
  ComboboxEmpty,
  ComboboxInput,
  ComboboxItem,
  ComboboxList,
} from '@/shared/ui/primitives/combobox';
import { serviceColorIndex } from './span-row';

export interface ServiceOption {
  name: string;
  /** Optional span count shown on the right of each item. */
  spanCount?: number;
}

export interface ServiceComboBoxProps {
  services: ServiceOption[];
  /** Selected service name, or null for none. */
  value: string | null;
  onChange: (name: string | null) => void;
  placeholder?: string;
  className?: string;
}

/**
 * Searchable service selector (Kibana/Grafana variable style). Each item has a
 * consistent service colour dot (shared with the waterfall/trace-list) and an
 * optional span count. Built on the Base UI combobox — type to filter,
 * arrow-key navigation and clear come for free.
 *
 * Usage:
 *   const [service, setService] = useState<string | null>(null);
 *   <ServiceComboBox services={data.items} value={service} onChange={setService} />
 */
export function ServiceComboBox({
  services,
  value,
  onChange,
  placeholder = 'All services',
  className,
}: ServiceComboBoxProps) {
  const names = services.map((s) => s.name);
  const counts = new Map(services.map((s) => [s.name, s.spanCount] as const));

  return (
    <Combobox items={names} value={value} onValueChange={(next: string | null) => onChange(next)}>
      <ComboboxInput placeholder={placeholder} showClear className={cn('w-56', className)} />
      <ComboboxContent>
        <ComboboxEmpty>No matching services.</ComboboxEmpty>
        <ComboboxList>
          {(name: string) => {
            const count = counts.get(name);
            return (
              <ComboboxItem key={name} value={name}>
                <span
                  aria-hidden
                  className="size-2 shrink-0 rounded-[2px]"
                  style={{ background: `var(--svc-${serviceColorIndex(name)})` }}
                />
                <span className="truncate">{name}</span>
                {count != null && (
                  <span className="ml-auto pl-3 text-muted-foreground tabular-nums">
                    {count.toLocaleString()} spans
                  </span>
                )}
              </ComboboxItem>
            );
          }}
        </ComboboxList>
      </ComboboxContent>
    </Combobox>
  );
}
