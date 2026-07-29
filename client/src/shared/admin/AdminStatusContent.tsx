import type { ReactNode } from 'react';
import type { AdminDataSource } from '@/shared/admin/adminData.tsx';
import {
  freshnessStatusBadgeColors,
  issueToneDotColors,
  statusTextColors,
} from '@/shared/statusColors.ts';
import { AdminMetricCard } from './AdminMetricCard.tsx';

type StatusSnapshot = {
  issues: {
    approachingVacationCap: number;
    facultyAtVacationCap: number;
    pendingRequests: number;
  };
};

export function AdminStatusContent({
  clusterCount,
  clustersMissingCaos,
  dataSources,
  departmentCount,
  departmentsMissingChairs,
  statusSnapshot,
}: {
  clusterCount: number;
  clustersMissingCaos: number;
  dataSources: AdminDataSource[];
  departmentCount: number;
  departmentsMissingChairs: number;
  statusSnapshot: StatusSnapshot;
}) {
  return (
    <div className="space-y-6">
      <section className="grid gap-4 lg:grid-cols-2">
        <AdminMetricCard
          accent={departmentsMissingChairs > 0 ? statusTextColors.warning : statusTextColors.success}
          label="Departments Missing Chairs"
          subtitle={`${departmentCount - departmentsMissingChairs} of ${departmentCount} departments assigned`}
          value={String(departmentsMissingChairs)}
          variant="summary"
        />
        <AdminMetricCard
          accent={clustersMissingCaos > 0 ? statusTextColors.warning : statusTextColors.success}
          label="Clusters Missing CAOs"
          subtitle={`${clusterCount - clustersMissingCaos} of ${clusterCount} clusters assigned`}
          value={String(clustersMissingCaos)}
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
          </div>
        </AdminSectionCard>
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
    <section className="card border border-main-border bg-base-100">
      <div className="card-body p-6">
        <h2 className="text-lg font-semibold text-primary">{title}</h2>
        <div className="mt-4">{children}</div>
      </div>
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
    : 'No rows loaded yet';
  const tone = freshnessStatusBadgeColors[status];

  return (
    <div className="flex flex-col gap-3 border-b border-base-300 py-3 last:border-b-0 sm:flex-row sm:items-start sm:justify-between">
      <div>
        <div className="font-semibold text-base-content">{label}</div>
        <div className="mt-1 text-sm leading-6 text-base-content/70">
          {detail}
        </div>
      </div>
      <div className="sm:text-right">
        <span className={`inline-flex rounded-full px-3 py-1 text-xs font-semibold ${tone}`}>
          {status}
        </span>
        <div className="mt-2 text-sm text-base-content/70">
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
  return (
    <div className="flex items-center gap-3 border-b border-base-300 py-3 last:border-b-0">
      <span
        className={`h-2.5 w-2.5 rounded-full ${issueToneDotColors[tone]}`}
      />
      <span className="flex-1 text-sm text-base-content">{label}</span>
      <span className="font-mono text-sm font-semibold text-base-content">
        {count}
      </span>
    </div>
  );
}
