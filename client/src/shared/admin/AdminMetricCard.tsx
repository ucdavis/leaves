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
      ? 'card border border-main-border bg-base-100'
      : 'card bg-base-200';
  const bodyClassName = variant === 'summary' ? 'card-body p-5' : 'card-body px-4 py-4';
  const labelClassName =
    variant === 'summary'
      ? 'card-stat-label'
      : 'text-xs font-semibold uppercase tracking-[0.2em] text-base-content/50';
  const valueClassName =
    variant === 'summary'
      ? 'card-stat-value'
      : 'mt-2 text-2xl font-bold';
  const subtitleClassName =
    variant === 'summary' ? 'card-stat-details' : 'mt-2 text-sm text-base-content/70';

  return (
    <section className={containerClassName}>
      <div className={bodyClassName}>
        <p className={labelClassName}>{label}</p>
        <p
          className={`${valueClassName} ${
            accent ?? 'text-primary'
          }`}
        >
          {value}
        </p>
        {subtitle ? <p className={subtitleClassName}>{subtitle}</p> : null}
      </div>
    </section>
  );
}
