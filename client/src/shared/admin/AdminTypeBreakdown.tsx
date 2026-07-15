export function AdminTypeBreakdown({
  className,
  items,
}: {
  className?: string;
  items: Array<{
    label: string;
    value: number;
  }>;
}) {
  const total = items.reduce((sum, item) => sum + item.value, 0);
  const tones = [
    {
      bar: 'bg-[var(--admin-blue)]',
      dot: 'bg-[var(--admin-blue)]',
    },
    {
      bar: 'bg-emerald-600',
      dot: 'bg-emerald-600',
    },
    {
      bar: 'bg-amber-600',
      dot: 'bg-amber-600',
    },
    {
      bar: 'bg-violet-600',
      dot: 'bg-violet-600',
    },
  ];

  return (
    <div className={className ?? ''}>
      <div className="mb-3 flex items-center justify-between gap-3">
        <h3 className="text-sm font-semibold uppercase tracking-[0.2em] text-[var(--admin-gold-deep)]">
          By type
        </h3>
        <span className="text-xs text-[var(--admin-ink-muted)]">
          {total} total
        </span>
      </div>
      <div className="space-y-3">
        {items.map((item, index) => {
          const share = total > 0 ? Math.round((item.value / total) * 100) : 0;
          const tone = tones[index % tones.length];

          return (
            <div className="space-y-1.5" key={item.label}>
              <div className="flex items-center justify-between gap-3">
                <div className="flex items-center gap-2">
                  <span className={`h-2.5 w-2.5 rounded-full ${tone.dot}`} />
                  <span className="text-sm font-medium text-[var(--admin-ink)]">
                    {item.label}
                  </span>
                </div>
                <div className="flex items-baseline gap-2">
                  <span className="font-mono text-sm font-semibold text-[var(--admin-ink)]">
                    {item.value}
                  </span>
                  <span className="text-xs text-[var(--admin-ink-muted)]">
                    {share}%
                  </span>
                </div>
              </div>
              <div className="h-2 overflow-hidden rounded-full bg-[var(--admin-sand)]">
                <div
                  className={`h-full rounded-full ${tone.bar}`}
                  style={{ width: `${share}%` }}
                />
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
