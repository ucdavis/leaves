import { createFileRoute } from '@tanstack/react-router';
import { useAdminData } from '@/shared/admin/adminData.tsx';

export const Route = createFileRoute('/(authenticated)/admin/status')({
  component: AdminStatusRoute,
});

function AdminStatusRoute() {
  const { dataSources, departments, statusSnapshot } = useAdminData();

  return (
    <div className="space-y-6">
      <section className="grid gap-4 lg:grid-cols-2 xl:grid-cols-4">
        <StatCard
          label="Active users"
          sublabel={`${statusSnapshot.users.admins} admins, ${statusSnapshot.users.chairs} chairs`}
          value={String(statusSnapshot.users.total)}
        />
        <StatCard
          accent="text-emerald-700"
          label="Faculty split"
          sublabel={`${statusSnapshot.users.fyFaculty} FY and ${statusSnapshot.users.ayFaculty} AY`}
          value={`${statusSnapshot.users.fyFaculty + statusSnapshot.users.ayFaculty}`}
        />
        <StatCard
          accent="text-amber-700"
          label="Pending requests"
          sublabel="Demo snapshot mirroring the mockup dashboard"
          value={String(statusSnapshot.requests.pending)}
        />
        <StatCard
          accent="text-[var(--admin-blue)]"
          label="Departments"
          sublabel={`${statusSnapshot.departments.clustered} assigned to clusters`}
          value={String(departments.length)}
        />
      </section>

      <section className="grid gap-6 xl:grid-cols-[1.25fr_0.9fr]">
        <Card title="Data freshness">
          <div className="space-y-1">
            {dataSources.map((source) => (
              <FreshnessRow
                detail={source.detail}
                key={source.id}
                label={source.label}
                status={source.status}
                updatedAt={source.updatedAt}
              />
            ))}
          </div>
        </Card>

        <Card title="Issues">
          <div className="space-y-1">
            <IssueRow
              count={statusSnapshot.issues.missingEmails}
              label="Users missing email addresses"
              tone="error"
            />
            <IssueRow
              count={statusSnapshot.issues.facultyAtVacationCap}
              label="Faculty at the vacation cap"
              tone="error"
            />
            <IssueRow
              count={statusSnapshot.issues.approachingVacationCap}
              label="Faculty approaching the cap"
              tone="warning"
            />
            <IssueRow
              count={statusSnapshot.issues.pendingRequests}
              label="Requests awaiting approval"
              tone="warning"
            />
            <IssueRow
              count={statusSnapshot.issues.excludedUsers}
              label="Excluded users"
              tone="neutral"
            />
          </div>
        </Card>
      </section>

      <section className="grid gap-6 lg:grid-cols-2">
        <Card title="Users">
          <MetricGrid
            items={[
              {
                label: 'FY faculty',
                value: String(statusSnapshot.users.fyFaculty),
              },
              {
                label: 'AY faculty',
                tone: 'emerald',
                value: String(statusSnapshot.users.ayFaculty),
              },
              {
                label: 'Chairs',
                value: String(statusSnapshot.users.chairs),
              },
              {
                label: 'CAOs',
                tone: 'violet',
                value: String(statusSnapshot.users.caos),
              },
            ]}
          />
        </Card>

        <Card title="Departments">
          <MetricGrid
            items={[
              {
                label: 'Total departments',
                value: String(statusSnapshot.departments.total),
              },
              {
                label: 'With faculty',
                tone: 'emerald',
                value: String(statusSnapshot.departments.withFaculty),
              },
              {
                label: 'Clustered',
                value: String(statusSnapshot.departments.clustered),
              },
              {
                label: 'Auto-debit enabled',
                tone: 'amber',
                value: String(statusSnapshot.autoDebit.active),
              },
            ]}
          />
        </Card>

        <Card title="Leave requests">
          <MetricGrid
            items={[
              {
                label: 'Manual',
                value: String(statusSnapshot.requests.bySource.manual),
              },
              {
                label: 'Auto-debit',
                tone: 'emerald',
                value: String(statusSnapshot.requests.bySource['auto-debit']),
              },
              {
                label: 'External Cognos',
                tone: 'violet',
                value: String(statusSnapshot.requests.bySource.cognos),
              },
              {
                label: 'Pending',
                tone: 'amber',
                value: String(statusSnapshot.requests.pending),
              },
            ]}
          />
          <TypeBreakdown
            className="mt-5"
            items={Object.entries(statusSnapshot.requests.byType).map(
              ([label, value]) => ({
                label,
                value,
              })
            )}
          />
        </Card>

        <Card title="Auto-debit and vacation cap">
          <MetricGrid
            items={[
              {
                label: 'Eligible FY/Chair',
                value: String(statusSnapshot.autoDebit.eligible),
              },
              {
                label: 'Auto-debit enabled',
                tone: 'emerald',
                value: String(statusSnapshot.autoDebit.active),
              },
              {
                label: 'At cap',
                tone: 'rose',
                value: String(statusSnapshot.issues.facultyAtVacationCap),
              },
              {
                label: 'Approaching cap',
                tone: 'amber',
                value: String(statusSnapshot.issues.approachingVacationCap),
              },
            ]}
          />
        </Card>
      </section>
    </div>
  );
}

function Card({
  children,
  title,
}: {
  children: React.ReactNode;
  title: string;
}) {
  return (
    <section className="rounded-[1.25rem] border border-[var(--admin-border)] bg-white p-6 shadow-sm">
      <h2 className="text-lg font-semibold text-[var(--admin-blue)]">{title}</h2>
      <div className="mt-4">{children}</div>
    </section>
  );
}

function StatCard({
  accent,
  label,
  sublabel,
  value,
}: {
  accent?: string;
  label: string;
  sublabel: string;
  value: string;
}) {
  return (
    <section className="rounded-[1.25rem] border border-[var(--admin-border)] bg-white p-5 shadow-sm">
      <p className="text-xs font-semibold uppercase tracking-[0.24em] text-[var(--admin-gold-deep)]">
        {label}
      </p>
      <p className={`mt-3 text-3xl font-bold ${accent ?? 'text-[var(--admin-blue)]'}`}>
        {value}
      </p>
      <p className="mt-2 text-sm text-[var(--admin-ink-muted)]">{sublabel}</p>
    </section>
  );
}

function FreshnessRow({
  detail,
  label,
  status,
  updatedAt,
}: {
  detail: string;
  label: string;
  status: 'ready' | 'planned' | 'deferred';
  updatedAt: string | null;
}) {
  const updatedLabel = updatedAt
    ? new Date(updatedAt).toLocaleString()
    : 'Waiting on database tables';
  const tone =
    status === 'ready'
      ? 'text-emerald-700 bg-emerald-50'
      : status === 'planned'
        ? 'text-amber-700 bg-amber-50'
        : 'text-slate-600 bg-slate-100';

  return (
    <div className="flex flex-col gap-3 border-b border-[var(--admin-border)] py-3 last:border-b-0 sm:flex-row sm:items-start sm:justify-between">
      <div>
        <div className="font-semibold text-[var(--admin-ink)]">{label}</div>
        <div className="mt-1 text-sm leading-6 text-[var(--admin-ink-muted)]">
          {detail}
        </div>
      </div>
      <div className="sm:text-right">
        <span className={`inline-flex rounded-full px-3 py-1 text-xs font-semibold ${tone}`}>
          {status}
        </span>
        <div className="mt-2 text-sm text-[var(--admin-ink-muted)]">
          {updatedLabel}
        </div>
      </div>
    </div>
  );
}

function IssueRow({
  count,
  label,
  tone,
}: {
  count: number;
  label: string;
  tone: 'error' | 'warning' | 'neutral';
}) {
  const styles = {
    error: 'bg-rose-600',
    neutral: 'bg-slate-400',
    warning: 'bg-amber-500',
  } as const;

  return (
    <div className="flex items-center gap-3 border-b border-[var(--admin-border)] py-3 last:border-b-0">
      <span className={`h-2.5 w-2.5 rounded-full ${styles[tone]}`} />
      <span className="flex-1 text-sm text-[var(--admin-ink)]">{label}</span>
      <span className="font-mono text-sm font-semibold text-[var(--admin-ink)]">
        {count}
      </span>
    </div>
  );
}

function MetricGrid({
  items,
}: {
  items: Array<{
    label: string;
    tone?: 'amber' | 'emerald' | 'rose' | 'violet';
    value: string;
  }>;
}) {
  const toneClasses = {
    amber: 'text-amber-700',
    emerald: 'text-emerald-700',
    rose: 'text-rose-700',
    violet: 'text-violet-700',
  } as const;

  return (
    <div className="grid gap-3 sm:grid-cols-2">
      {items.map((item) => (
        <div
          className="rounded-2xl bg-[var(--admin-sand)] px-4 py-4"
          key={item.label}
        >
          <div className="text-xs font-semibold uppercase tracking-[0.2em] text-[var(--admin-ink-soft)]">
            {item.label}
          </div>
          <div
            className={`mt-2 text-2xl font-bold ${
              item.tone ? toneClasses[item.tone] : 'text-[var(--admin-blue)]'
            }`}
          >
            {item.value}
          </div>
        </div>
      ))}
    </div>
  );
}

function TypeBreakdown({
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
    <div className={className ? className : ''}>
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
