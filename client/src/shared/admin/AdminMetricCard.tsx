export function AdminMetricCard({
  accent,
  label,
  subtitle,
  value,
  variant = 'tile',
}: {
  accent?: string;
  label: string;
  subtitle?: string;
  value: string;
  variant?: 'summary' | 'tile';
}) {
  const containerClassName =
    variant === 'summary'
      ? 'rounded-[1.25rem] border border-[var(--admin-border)] bg-white p-5 shadow-sm'
      : 'rounded-2xl bg-[var(--admin-sand)] px-4 py-4';
  const labelClassName =
    variant === 'summary'
      ? 'text-xs font-semibold uppercase tracking-[0.24em] text-[var(--admin-gold-deep)]'
      : 'text-xs font-semibold uppercase tracking-[0.2em] text-[var(--admin-ink-soft)]';
  const valueClassName =
    variant === 'summary'
      ? 'mt-3 text-3xl font-bold'
      : 'mt-2 text-2xl font-bold';
  const subtitleClassName = 'mt-2 text-sm text-[var(--admin-ink-muted)]';

  return (
    <section className={containerClassName}>
      <p className={labelClassName}>{label}</p>
      <p
        className={`${valueClassName} ${
          accent ?? 'text-[var(--admin-blue)]'
        }`}
      >
        {value}
      </p>
      {subtitle ? <p className={subtitleClassName}>{subtitle}</p> : null}
    </section>
  );
}
