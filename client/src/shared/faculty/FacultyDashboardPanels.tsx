import { EnvelopeIcon } from '@heroicons/react/24/outline';
import { Toast } from '@/shared/Toast.tsx';
import {
  type FacultyAccrualBalance,
  type FacultyDashboardResponse,
  type FacultyLeaveRequest,
} from '@/queries/faculty.ts';

export const reportLeaveButtonClass =
  'btn btn-primary';

export function AccrualBalancePanel({
  balances,
}: {
  balances: FacultyAccrualBalance[];
}) {
  const orderedBalances = [...balances].sort((left, right) => {
    return (
      getLeaveBalanceSortRank(left.typeLabel) -
      getLeaveBalanceSortRank(right.typeLabel)
    );
  });

  return (
    <section className="min-h-72 rounded-lg border border-base-300 bg-base-100 p-6 shadow-sm">
      <div className="mb-6 flex items-center justify-between gap-4">
        <h2 className="font-bold text-primary">Leave Balances</h2>
        <span className="rounded-full bg-base-200 px-3 py-1 text-xs font-semibold text-base-content/50">
          Source: UCPath
        </span>
      </div>

      <div className="space-y-7">
        {orderedBalances.length > 0 ? (
          orderedBalances.map((balance) => (
            <AccrualBalanceBar balance={balance} key={balance.typeLabel} />
          ))
        ) : (
          <EmptyPanelMessage message="No current accrual balances are available." />
        )}
      </div>
    </section>
  );
}

function AccrualBalanceBar({ balance }: { balance: FacultyAccrualBalance }) {
  const percentage = getBalancePercentage(balance);
  const tone = getLeaveTone(balance.typeLabel);

  return (
    <article>
      <div className="mb-2 flex items-center justify-between gap-3 text-sm">
        <div className="font-bold">{balance.typeLabel}</div>
        <div className={`font-bold ${tone.text}`}>
          {formatHours(balance.calculatedBalance)}
        </div>
      </div>
      <div className="h-2 overflow-hidden rounded-full bg-base-200">
        <div
          className={`h-full rounded-full ${tone.bar}`}
          style={{ width: `${percentage}%` }}
        />
      </div>
      {balance.accrualLimit > 0 ? (
        <div className="mt-2 text-right text-xs text-base-content/50">
          Cap: {formatHours(balance.accrualLimit)}
        </div>
      ) : null}
    </article>
  );
}

export function QuickActionsPanel({
  data,
  onReportLeave,
  onViewHistory,
}: {
  data: FacultyDashboardResponse;
  onReportLeave: () => void;
  onViewHistory: () => void;
}) {
  return (
    <section className="rounded-lg border border-base-300 bg-base-100 p-6 shadow-sm">
      <h2 className="mb-5 font-bold text-primary">Quick Actions</h2>
      <button
        className={`${reportLeaveButtonClass} w-full btn-lg`}
        onClick={onReportLeave}
        type="button"
      >
        Report Leave
      </button>
      <div className="mt-4 rounded-lg bg-base-200 px-4 py-3 text-sm text-base-content/70">
        <span className="font-bold text-base-content">Department:</span>{' '}
        {data.faculty.departmentName ?? 'No reporting department'}.
      </div>
      <button
        className="btn btn-outline btn-primary mt-3 w-full"
        onClick={onViewHistory}
        type="button"
      >
        View History
      </button>
    </section>
  );
}

export function RecentRequestsPanel({
  onSelectRequest,
  requests,
}: {
  onSelectRequest: (request: FacultyLeaveRequest) => void;
  requests: FacultyLeaveRequest[];
}) {
  return (
    <section className="rounded-lg border border-base-300 bg-base-100 p-6 shadow-sm">
      <h2 className="mb-5 font-bold text-primary">Recent Requests</h2>
      <div className="divide-y divide-base-300">
        {requests.length > 0 ? (
          requests.map((request) => (
            <RecentRequestRow
              key={request.id}
              onSelectRequest={onSelectRequest}
              request={request}
            />
          ))
        ) : (
          <EmptyPanelMessage message="No recent leave requests are on file." />
        )}
      </div>
    </section>
  );
}

function RecentRequestRow({
  onSelectRequest,
  request,
}: {
  onSelectRequest: (request: FacultyLeaveRequest) => void;
  request: FacultyLeaveRequest;
}) {
  const tone = getLeaveTone(request.leaveType);

  return (
    <button
      className="grid w-full grid-cols-[minmax(0,1fr)_auto_auto] items-center gap-3 py-3 text-left transition first:pt-0 last:pb-0 hover:bg-base-200/60"
      onClick={() => onSelectRequest(request)}
      type="button"
    >
      <div className="flex min-w-0 items-start gap-3">
        <span className={`mt-1 h-2.5 w-2.5 rounded-full ${tone.dot}`} />
        <div className="min-w-0">
          <div className="truncate text-sm font-bold">{request.leaveType}</div>
          <div className="text-xs text-base-content/60">
            {formatDateRange(request.startDate, request.endDate)}
          </div>
        </div>
      </div>
      <div className="text-sm font-bold">{formatHours(request.totalHours)}</div>
      <RequestStatusBadge status={request.status} />
    </button>
  );
}

export function FacultyToast({
  message,
  onDismiss,
}: {
  message: string | null;
  onDismiss: () => void;
}) {
  if (!message) {
    return null;
  }

  return (
    <Toast className="fixed right-6 top-6 z-50 max-w-xl" icon={EnvelopeIcon} onDismiss={onDismiss} tone="success">
      {message}
    </Toast>
  );
}

export function EmptyPanelMessage({ message }: { message: string }) {
  return <p className="py-3 text-sm text-base-content/60">{message}</p>;
}

export function RequestStatusBadge({ status }: { status: string }) {
  const normalized = status.toLowerCase();

  let className = 'bg-base-200 text-base-content/70';
  if (normalized.includes('approved')) {
    className = 'bg-success/15 text-success';
  } else if (normalized.includes('pending')) {
    className = 'bg-warning/15 text-warning';
  } else if (normalized.includes('denied')) {
    className = 'bg-error/15 text-error';
  }

  return (
    <span className={`rounded-full px-3 py-1 text-xs font-semibold ${className}`}>
      {status}
    </span>
  );
}

const numberFormatter = new Intl.NumberFormat(undefined, {
  maximumFractionDigits: 1,
  minimumFractionDigits: 0,
});

const dateFormatter = new Intl.DateTimeFormat(undefined, {
  day: 'numeric',
  month: 'short',
  year: 'numeric',
});

const monthFormatter = new Intl.DateTimeFormat(undefined, {
  month: 'long',
  year: 'numeric',
});

const longDateFormatter = new Intl.DateTimeFormat(undefined, {
  day: 'numeric',
  month: 'long',
  year: 'numeric',
});

export function getLeaveBalanceSortRank(typeLabel: string) {
  if (typeLabel === 'Vacation') {
    return 0;
  }

  if (typeLabel === 'Sick Leave') {
    return 2;
  }

  return 1;
}

export function getBalancePercentage(balance: FacultyAccrualBalance) {
  if (balance.accrualLimit <= 0) {
    return 100;
  }

  return Math.max(
    0,
    Math.min(100, (balance.calculatedBalance / balance.accrualLimit) * 100)
  );
}

export function getLeaveTone(leaveType: string) {
  const normalized = leaveType.toLowerCase();

  if (normalized.includes('sick')) {
    return {
      bar: 'bg-emerald-500',
      dot: 'bg-emerald-500',
      surface: 'border-emerald-500 bg-emerald-100 text-emerald-800',
      text: 'text-emerald-700',
    };
  }

  if (normalized.includes('professional')) {
    return {
      bar: 'bg-violet-500',
      dot: 'bg-violet-500',
      surface: 'border-violet-500 bg-violet-100 text-violet-800',
      text: 'text-violet-700',
    };
  }

  if (normalized.includes('sabbatical')) {
    return {
      bar: 'bg-red-500',
      dot: 'bg-red-500',
      surface: 'border-red-500 bg-red-100 text-red-800',
      text: 'text-red-700',
    };
  }

  if (normalized.includes('family') || normalized.includes('fmla')) {
    return {
      bar: 'bg-orange-500',
      dot: 'bg-orange-500',
      surface: 'border-orange-500 bg-orange-100 text-orange-800',
      text: 'text-orange-700',
    };
  }

  return {
    bar: 'bg-blue-600',
    dot: 'bg-blue-600',
    surface: 'border-blue-500 bg-blue-100 text-blue-800',
    text: 'text-blue-700',
  };
}

export function formatHours(value: number) {
  return `${numberFormatter.format(value)} hrs`;
}

export function formatWorkflowModeLabel(workflowMode: string | null | undefined) {
  if (workflowMode === 'ApprovalRequired') {
    return 'Approval required';
  }

  if (workflowMode === 'DirectSubmission') {
    return 'Auto-approve';
  }

  return 'No reporting department';
}

export function formatCompactHours(value: number) {
  return `${numberFormatter.format(value)}h`;
}

export function formatDate(value: string) {
  return dateFormatter.format(parseIsoDate(value));
}

export function formatLongDate(value: string) {
  return longDateFormatter.format(parseIsoDate(value));
}

export function formatMonth(value: string) {
  return monthFormatter.format(parseIsoDate(value));
}

export function formatDateRange(startDate: string, endDate: string) {
  if (startDate === endDate) {
    return formatDate(startDate);
  }

  return `${formatDate(startDate)} - ${formatDate(endDate)}`;
}

export function formatLongDateRange(startDate: string, endDate: string) {
  if (startDate === endDate) {
    return formatLongDate(startDate);
  }

  return `${formatLongDate(startDate)} through ${formatLongDate(endDate)}`;
}

export function isIsoDate(value: string) {
  return /^\d{4}-\d{2}-\d{2}$/.test(value);
}

function parseIsoDate(value: string) {
  return isIsoDate(value)
    ? new Date(`${value}T00:00:00`)
    : new Date(value);
}
