import { useTranslation } from 'react-i18next';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/shared/ui/primitives/dialog';
import { Button } from '@/shared/ui/primitives/button';
import { Field, FieldDescription, FieldGroup, FieldLabel } from '@/shared/ui/primitives/field';
import { DarkMode, LightMode, Refresh } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { cn } from '@/lib/utils';
import { usePreferences, type FontSize, type Theme } from '@/shared/lib/preferences-provider';

interface PreferencesDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

const FONT_SIZES: Array<{ value: FontSize; label: string }> = [
  { value: 'small',  label: '' },
  { value: 'medium', label: '' },
  { value: 'large',  label: '' },
];

const THEMES: Array<{ value: Theme; label: string; Icon: typeof LightMode }> = [
  { value: 'light',  label: '', Icon: LightMode },
  { value: 'dark',   label: '',  Icon: DarkMode },
  { value: 'system', label: '', Icon: LightMode }, // Material Symbols — no half-circle; LightMode icon re-used
];

/**
 * User visual preferences. Two sections:
 *   1. Theme — light / dark / system
 *   2. Font size — small / medium / large (rem-based scale on <html>)
 *
 * A "Reset" button restores both to PREFERENCES_DEFAULT. Every change is
 * persisted to localStorage and applied to the document immediately by
 * PreferencesProvider.
 *
 * Accent color picker is stretch — MVP uses default ink only.
 */
export function PreferencesDialog({ open, onOpenChange }: PreferencesDialogProps) {
  const { preferences, update, reset } = usePreferences();
  const { t } = useTranslation();

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle className="text-sm">{t('preferences.title')}</DialogTitle>
          <DialogDescription>
            {t('preferences.description')}
          </DialogDescription>
        </DialogHeader>

        <FieldGroup>
          <Field>
            <FieldLabel>{t('preferences.theme')}</FieldLabel>
            <div role="radiogroup" className="grid grid-cols-3 gap-2">
              {THEMES.map((t) => {
                const isActive = preferences.theme === t.value;
                const Icon = t.Icon;
                return (
                  <button
                    key={t.value}
                    type="button"
                    role="radio"
                    aria-checked={isActive}
                    onClick={() => update('theme', t.value)}
                    className={cn(
                      'flex flex-col items-center gap-1.5 rounded-md border bg-background p-2.5 text-xs transition-colors hover:border-foreground/30',
                      isActive
                        ? 'border-foreground/60 ring-1 ring-foreground/40'
                        : 'border-border',
                    )}
                  >
                    <Icon className="size-4" />
                    <span>{t.label}</span>
                  </button>
                );
              })}
            </div>
          </Field>

          <Field>
            <FieldLabel>{t('preferences.fontSize')}</FieldLabel>
            <div role="radiogroup" className="grid grid-cols-3 gap-2">
              {FONT_SIZES.map((f) => {
                const isActive = preferences.fontSize === f.value;
                return (
                  <button
                    key={f.value}
                    type="button"
                    role="radio"
                    aria-checked={isActive}
                    onClick={() => update('fontSize', f.value)}
                    className={cn(
                      'rounded-md border bg-background p-2.5 transition-colors hover:border-foreground/30',
                      isActive
                        ? 'border-foreground/60 ring-1 ring-foreground/40'
                        : 'border-border',
                    )}
                  >
                    <span className={f.value === 'small' ? 'text-xs' : f.value === 'large' ? 'text-base font-medium' : 'text-sm'}>
                      Aa
                    </span>
                    <span className="mt-1 block text-[10px] text-muted-foreground">{f.label}</span>
                  </button>
                );
              })}
            </div>
            <FieldDescription>{t('preferences.fontSizeDescription')}</FieldDescription>
          </Field>
        </FieldGroup>

        <DialogFooter>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => reset()}
          >
            <Refresh className="size-3.5" />
            {t('preferences.reset')}
          </Button>
          <Button onClick={() => onOpenChange(false)}>{t('common.done')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
