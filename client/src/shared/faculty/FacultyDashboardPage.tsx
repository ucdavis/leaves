import { useNavigate } from '@tanstack/react-router';
import { useState } from 'react';
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
  calendarDate,
  calendarRequests,
  data,
  readOnly = false,
}: {
  calendarDate?: string;
  calendarRequests?: FacultyLeaveRequest[];
  data: FacultyDashboardResponse;
  readOnly?: boolean;
}) {
  const navigate = useNavigate();
  const [reportModalOpen, setReportModalOpen] = useState(false);
  const [calendarDates, setCalendarDates] = useState<{
    endDate: string;
    startDate: string;
  } | null>(null);
  const [selectedRequest, setSelectedRequest] =
    useState<FacultyLeaveRequest | null>(null);
  const recentRequests = data.recentRequests.slice(0, 5);

  return (
    <div className="container py-8 lg:py-10">
      {readOnly ? (
        <div className="mx-auto grid gap-5 lg:grid-cols-[minmax(0,1.1fr)_minmax(0,1fr)] py-8">
          <ReadOnlyFacultyHeader data={data} />
          <RecentRequestsPanel
            onSelectRequest={setSelectedRequest}
            requests={recentRequests}
          />
        </div>
      ) : (
        <div className="mx-auto grid gap-5 lg:grid-cols-2 py-8">
          <div className="space-y-5">
            <QuickActionsPanel
              data={data}
              onReportLeave={() => setReportModalOpen(true)}
            />
            <RecentRequestsPanel
              onSelectRequest={setSelectedRequest}
              onShowInCalendar={(request) =>
                void navigate({
                  search: { calendarDate: request.startDate },
                  to: '/',
                })
              }
              requests={recentRequests}
            />
          </div>
          <AccrualBalancePanel balances={data.accrualBalances} />
        </div>
      )}

      <div className="mx-auto mt-6">
        <LeaveCalendar
          faculty={data.faculty}
          focusDate={calendarDate}
          key={calendarDate ?? 'dashboard-calendar'}
          onReportLeave={
            readOnly
              ? undefined
              : (startDate, endDate) => {
                  setCalendarDates({ endDate, startDate });
                  setReportModalOpen(true);
                }
          }
          requests={calendarRequests ?? data.recentRequests}
        />
      </div>

      {reportModalOpen ? (
        <ReportLeaveModal
          data={data}
          initialEndDate={calendarDates?.endDate}
          initialStartDate={calendarDates?.startDate}
          onClose={() => {
            setCalendarDates(null);
            setReportModalOpen(false);
          }}
          onSent={() => {
            setCalendarDates(null);
            setReportModalOpen(false);
          }}
        />
      ) : null}

      {selectedRequest ? (
        <RequestDetailModal
          faculty={data.faculty}
          onClose={() => setSelectedRequest(null)}
          request={selectedRequest}
        />
      ) : null}
    </div>
  );
}

function ReadOnlyFacultyHeader({ data }: { data: FacultyDashboardResponse }) {
  const balances = [...data.accrualBalances].sort((left, right) => {
    return (
      getLeaveBalanceSortRank(left.typeLabel) -
      getLeaveBalanceSortRank(right.typeLabel)
    );
  });
  return (
    <section className="rounded-[1.1rem] border border-[#d8d2c7] bg-white p-6 shadow-[0_2px_10px_rgba(33,24,14,0.08)]">
      <div className="min-w-0">
        <h1 className="text-[1.7rem] leading-tight font-bold text-[#123a73]">
          {data.faculty.name}
        </h1>
        <p className="text-[0.98rem] text-[#625a4f]">
          {data.faculty.jobTitle ?? 'Faculty'}
          {data.faculty.email ? (
            <>
              {' · '}
              <a
                className="underline decoration-[#625a4f]/50 underline-offset-2 hover:text-[#123a73]"
                href={`mailto:${data.faculty.email}`}
              >
                {data.faculty.email}
              </a>
            </>
          ) : null}
        </p>
        <p className="text-sm text-[#756c61]">
          {data.faculty.employeeClass ??
            data.faculty.departmentCode ??
            data.faculty.departmentName ??
            'Faculty appointment'}
        </p>
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

function ReadOnlyBalanceRow({ balance }: { balance: FacultyAccrualBalance }) {
  const hasAccrualCap = balance.accrualLimit > 0;
  const tone = hasAccrualCap ? getLeaveTone(balance.typeLabel) : undefined;
  const percentage = hasAccrualCap ? getBalancePercentage(balance) : 0;

  return (
    <article>
      <div className="mb-2 flex items-center justify-between gap-3">
        <div className="text-lg font-semibold text-[#1f1a14]">
          {balance.typeLabel}
        </div>
        <div className="text-xl font-bold text-[#123a73]">
          {formatHours(balance.calculatedBalance)}
        </div>
      </div>
      {hasAccrualCap && tone ? (
        <>
          <div className="h-1.5 overflow-hidden rounded-full bg-[#d6e7df]">
            <div
              className={`h-full rounded-full ${tone.bar}`}
              style={{ width: `${percentage}%` }}
            />
          </div>
          <div className="mt-2 text-xs text-[#756c61]">
            {formatHours(balance.calculatedBalance)} of{' '}
            {formatHours(balance.accrualLimit)} · {Math.round(percentage)}% of
            cap
          </div>
        </>
      ) : (
        <div className="text-sm text-[#756c61]">No accrual cap</div>
      )}
    </article>
  );
}
