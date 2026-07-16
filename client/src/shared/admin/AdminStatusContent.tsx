import type { ReactNode } from 'react';
import type { AdminDataSource } from '@/shared/admin/adminData.tsx';
import { AdminMetricCard } from './AdminMetricCard.tsx';
import { AdminTypeBreakdown } from './AdminTypeBreakdown.tsx';

type MetricItem = {
  accent?: string;
  label: string;
  value: string;
};

type StatusSnapshot = {
  departments: {
    clustered: number;
    total: number;
    withFaculty: number;
  };
  issues: {
    approachingVacationCap: number;
    excludedUsers: number;
    facultyAtVacationCap: number;
    missingEmails: number;
    pendingRequests: number;
  };
  requests: {
    bySource: Record<'cognos' | 'manual', number>;
    byType: Record<string, number>;
    pending: number;
    total: number;
  };
  users: {
    admins: number;
    ayFaculty: number;
    caos: number;
    chairs: number;
    fyFaculty: number;
    total: number;
  };
};

export function AdminStatusContent({
  dataSources,
  departmentCount,
  statusSnapshot,
}: {
  dataSources: AdminDataSource[];
  departmentCount: number;
  statusSnapshot: StatusSnapshot;
}) {
  return (
    <div className="space-y-6">
      <section className="grid gap-4 lg:grid-cols-2 xl:grid-cols-4">
        <AdminMetricCard
          label="Active users"
          subtitle={`${statusSnapshot.users.admins} admins, ${statusSnapshot.users.chairs} chairs`}
          value={String(statusSnapshot.users.total)}
          variant="summary"
        />
        <AdminMetricCard
          accent="text-emerald-700"
          label="Faculty split"
          subtitle={`${statusSnapshot.users.fyFaculty} FY and ${statusSnapshot.users.ayFaculty} AY`}
          value={`${statusSnapshot.users.fyFaculty + statusSnapshot.users.ayFaculty}`}
          variant="summary"
        />
        <AdminMetricCard
          accent="text-amber-700"
          label="Pending requests"
          subtitle="Calculated from persisted leave request records"
          value={String(statusSnapshot.requests.pending)}
          variant="summary"
        />
        <AdminMetricCard
          label="Departments"
          subtitle={`${statusSnapshot.departments.clustered} assigned to clusters`}
          value={String(departmentCount)}
          variant="summary"
        />
      </section>

      <section className="grid gap-6 xl:grid-cols-[1.25fr_0.9fr]">
        <AdminSectionCard title="Data freshness">
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
        </AdminSectionCard>

        <AdminSectionCard title="Issues">
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
        </AdminSectionCard>
      </section>

      <section className="grid gap-6 lg:grid-cols-2">
        <MetricSection
          items={[
            {
              label: 'FY faculty',
              value: String(statusSnapshot.users.fyFaculty),
            },
            {
              accent: 'text-emerald-700',
              label: 'AY faculty',
              value: String(statusSnapshot.users.ayFaculty),
            },
            {
              label: 'Chairs',
              value: String(statusSnapshot.users.chairs),
            },
            {
              accent: 'text-violet-700',
              label: 'CAOs',
              value: String(statusSnapshot.users.caos),
            },
          ]}
          title="Users"
        />

        <MetricSection
          items={[
            {
              label: 'Total departments',
              value: String(statusSnapshot.departments.total),
            },
            {
              accent: 'text-emerald-700',
              label: 'With faculty',
              value: String(statusSnapshot.departments.withFaculty),
            },
            {
              label: 'Clustered',
              value: String(statusSnapshot.departments.clustered),
            },
          ]}
          title="Departments"
        />

        <AdminSectionCard title="Leave requests">
          <MetricGrid
            items={[
              {
                label: 'Manual',
                value: String(statusSnapshot.requests.bySource.manual),
              },
              {
                accent: 'text-violet-700',
                label: 'External Cognos',
                value: String(statusSnapshot.requests.bySource.cognos),
              },
              {
                accent: 'text-amber-700',
                label: 'Pending',
                value: String(statusSnapshot.requests.pending),
              },
            ]}
          />
          <AdminTypeBreakdown
            className="mt-5"
            items={Object.entries(statusSnapshot.requests.byType).map(
              ([label, value]) => ({
                label,
                value,
              })
            )}
          />
        </AdminSectionCard>

        <MetricSection
          items={[
            {
              accent: 'text-rose-700',
              label: 'At cap',
              value: String(statusSnapshot.issues.facultyAtVacationCap),
            },
            {
              accent: 'text-amber-700',
              label: 'Approaching cap',
              value: String(statusSnapshot.issues.approachingVacationCap),
            },
          ]}
          title="Vacation cap"
        />
      </section>
    </div>
  );
}

function AdminSectionCard({
  children,
  title,
}: {
  children: ReactNode;
  title: string;
}) {
  return (
    <section className="rounded-[1.25rem] border border-[var(--admin-border)] bg-white p-6 shadow-sm">
      <h2 className="text-lg font-semibold text-[var(--admin-blue)]">{title}</h2>
      <div className="mt-4">{children}</div>
    </section>
  );
}

function MetricGrid({ items }: { items: MetricItem[] }) {
  return (
    <div className="grid gap-3 sm:grid-cols-2">
      {items.map((item) => (
        <AdminMetricCard
          accent={item.accent}
          key={item.label}
          label={item.label}
          value={item.value}
        />
      ))}
    </div>
  );
}

function MetricSection({
  items,
  title,
}: {
  items: MetricItem[];
  title: string;
}) {
  return (
    <AdminSectionCard title={title}>
      <MetricGrid items={items} />
    </AdminSectionCard>
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
    : 'No rows loaded yet';
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
