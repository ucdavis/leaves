import { useState } from 'react';
import { useNavigate } from '@tanstack/react-router';
import type {
  FacultyAccrualBalance,
  FacultyDashboardResponse,
  FacultyLeaveRequest,
} from '@/queries/faculty.ts';
import { LeaveCalendar } from '@/shared/faculty/FacultyDashboardCalendar.tsx';
import {
  AccrualBalancePanel,
  formatHours,
  getBalancePercentage,
  getLeaveBalanceSortRank,
  getLeaveTone,
  QuickActionsPanel,
  RecentRequestsPanel,
} from '@/shared/faculty/FacultyDashboardPanels.tsx';
import {
  ReportLeaveModal,
  RequestDetailModal,
} from '@/shared/faculty/FacultyDashboardModals.tsx';

export function FacultyDashboardPage({
  data,
  readOnly = false,
}: {
  data: FacultyDashboardResponse;
  readOnly?: boolean;
}) {
  const navigate = useNavigate();
  const [reportModalOpen, setReportModalOpen] = useState(false);
  const [selectedRequest, setSelectedRequest] =
    useState<FacultyLeaveRequest | null>(null);

  return (
    <div className="container py-8 lg:py-10">
      <div className="mx-auto max-w-6xl space-y-6"> 

        {readOnly ? (
          <div className="grid gap-6 lg:grid-cols-[minmax(0,1.1fr)_minmax(0,1fr)]">
            <ReadOnlyFacultyHeader data={data} />
            <RecentRequestsPanel
              onSelectRequest={setSelectedRequest}
              requests={data.recentRequests}
            />
          </div>
        ) : (
          <div className="grid gap-6 lg:grid-cols-[minmax(0,1.3fr)_minmax(20rem,0.9fr)]">
            <AccrualBalancePanel balances={data.accrualBalances} />
            <QuickActionsPanel
              data={data}
              onReportLeave={() => setReportModalOpen(true)}
              onViewHistory={() => void navigate({ to: '/history' })}
            />
          </div>
        )}

        {!readOnly ? (
          <RecentRequestsPanel
            onSelectRequest={setSelectedRequest}
            requests={data.recentRequests}
          />
        ) : null}

        <LeaveCalendar
          allowEmailPreview={!readOnly}
          faculty={data.faculty}
          requests={data.recentRequests}
        />
      </div>

      {reportModalOpen ? (
        <ReportLeaveModal
          data={data}
          onClose={() => setReportModalOpen(false)}
          onSent={() => setReportModalOpen(false)}
        />
      ) : null}

      {selectedRequest ? (
        <RequestDetailModal
          allowEmailPreview={!readOnly}
          faculty={data.faculty}
          onClose={() => setSelectedRequest(null)}
          request={selectedRequest}
        />
      ) : null}
    </div>
  );
}

function ReadOnlyFacultyHeader({
  data,
}: {
  data: FacultyDashboardResponse;
}) {
  const balances = [...data.accrualBalances].sort((left, right) => {
    return (
      getLeaveBalanceSortRank(left.typeLabel) -
      getLeaveBalanceSortRank(right.typeLabel)
    );
  });
  const initials = getInitials(data.faculty.name);

  return (
    <section className="rounded-[1.1rem] border border-[#d8d2c7] bg-white p-6 shadow-[0_2px_10px_rgba(33,24,14,0.08)]">
      <div className="flex items-start gap-4">
        <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-[#123a73] text-sm font-bold text-white">
          {initials}
        </div>
        <div className="min-w-0">
          <h1 className="text-[1.7rem] leading-tight font-bold text-[#123a73]">
            {data.faculty.name}
          </h1>
          <p className="text-[0.98rem] text-[#625a4f]">
            {data.faculty.jobTitle ?? 'Faculty'}
            {data.faculty.email ? ` · ${data.faculty.email}` : ''}
          </p>
          <p className="text-sm text-[#756c61]">
            {data.faculty.employeeClass ??
              data.faculty.departmentCode ??
              data.faculty.departmentName ??
              'Faculty appointment'}
          </p>
        </div>
      </div>

      <div className="mt-5 space-y-5">
        {balances.length > 0 ? (
          balances.map((balance) => (
            <ReadOnlyBalanceRow balance={balance} key={balance.typeLabel} />
          ))
        ) : (
          <div className="text-sm text-base-content/60">
            No current accrual balances are available.
          </div>
        )}
      </div>
    </section>
  );
}

function ReadOnlyBalanceRow({
  balance,
}: {
  balance: FacultyAccrualBalance;
}) {
  const tone = getLeaveTone(balance.typeLabel);

  return (
    <article>
      <div className="mb-2 flex items-center justify-between gap-3">
        <div className="text-lg font-semibold text-[#1f1a14]">
          {balance.typeLabel}
        </div>
        <div className={`text-xl font-bold ${tone.text}`}>
          {formatHours(balance.calculatedBalance)}
        </div>
      </div>
      <div className="h-1.5 overflow-hidden rounded-full bg-[#d6e7df]">
        <div
          className={`h-full rounded-full ${tone.bar}`}
          style={{ width: `${getBalancePercentage(balance)}%` }}
        />
      </div>
    </article>
  );
}

function getInitials(name: string) {
  const initials = name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('');

  return initials || 'F';
}
