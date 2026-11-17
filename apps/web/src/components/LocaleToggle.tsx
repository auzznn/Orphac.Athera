"use client";

import { useLocale } from "@/lib/i18n/LocaleProvider";
import type { SupportedLocale } from "@/lib/i18n/messages";

export function LocaleToggle() {
  const { locale, setLocale, t } = useLocale();

  const options: { value: SupportedLocale; short: string; label: string }[] = [
    { value: "id-ID", short: "ID", label: "Bahasa Indonesia" },
    { value: "en-US", short: "EN", label: "English" },
  ];

  return (
    <div
      className="inline-flex items-center rounded-[var(--radius-sm)] border border-[var(--border-hairline)] bg-[var(--bg-surface)] p-0.5"
      role="group"
      aria-label={t.localeLabel}
    >
      {options.map((opt) => {
        const active = locale === opt.value;
        return (
          <button
            key={opt.value}
            type="button"
            onClick={() => setLocale(opt.value)}
            aria-label={opt.label}
            aria-pressed={active}
            className={`min-h-9 min-w-10 rounded-[3px] px-2.5 text-xs font-semibold tracking-wide transition-colors ${
              active
                ? "bg-[var(--accent-primary)] text-[var(--accent-primary-ink)]"
                : "text-[var(--text-secondary)] hover:bg-[var(--bg-chrome)]"
            }`}
          >
            {opt.short}
          </button>
        );
      })}
    </div>
  );
}
