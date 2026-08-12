import {
  ArrowLeftIcon,
  ArrowRightIcon,
  CalendarDaysIcon,
  PaperAirplaneIcon,
  XMarkIcon,
} from '@heroicons/react/24/outline';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { type ReactNode, useMemo, useState } from 'react';
import { z } from 'zod';
import {
  createFacultyLeaveRequest,
  facultyDashboardQueryOptions,
  type FacultyAccrualBalance,
  type FacultyDashboardResponse,
  type FacultyLeaveRequest,
} from '@/queries/faculty.ts';
import { useAppForm } from '@/shared/forms/formContext.tsx';
import { PageErrorState } from '@/shared/errors/PageErrorState.tsx';
import { statusSurfaceColors } from '@/shared/statusColors.ts';

const leaveRequestSchema = z
  .object({
    coveragePlan: z.string().trim().max(2000, 'Coverage plan is too long.'),
    endDate: z
      .string()
      .min(1, 'End date is required.')
      .regex(/^\d{4}-\d{2}-\d{2}$/, 'Use YYYY-MM-DD.'),
    leaveTypeId: z.string().min(1, 'Select a leave type.'),
    note: z.string().trim().max(1000, 'Note is too long.'),
    payLeaveTypeId: z.string(),
    startDate: z
      .string()
      .min(1, 'Start date is required.')
      .regex(/^\d{4}-\d{2}-\d{2}$/, 'Use YYYY-MM-DD.'),
    totalHours: z
      .string()
      .min(1, 'Total hours are required.')
      .refine((value) => Number(value) > 0, 'Hours must be greater than zero.')
      .refine((value) => Number(value) <= 240, 'Hours must be 240 or fewer.'),
  })
  .refine((value) => value.endDate >= value.startDate, {
    message: 'End date must be on or after the start date.',
    path: ['endDate'],
  });

type LeaveRequestFormValues = z.infer<typeof leaveRequestSchema>;

const calendarLegend = [
  { className: 'border-blue-500 bg-blue-100 text-blue-800', label: 'Vacation' },
  {
    className: 'border-emerald-500 bg-emerald-100 text-emerald-800',
    label: 'Sick Leave',
  },
  {
    className: 'border-violet-500 bg-violet-100 text-violet-800',
    label: 'Professional Development',
  },
  {
    className: 'border-red-500 bg-red-100 text-red-800',
    label: 'Sabbatical',
  },
  {
    className: 'border-orange-500 bg-orange-100 text-orange-800',
    label: 'FMLA',
  },
  {
    className:
      'border-dashed border-base-content/50 bg-base-200 text-base-content/70',
    label: 'Pending',
  },
] as const;

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

const weekdayLabels = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

export function FacultyDashboard() {
  const dashboardQuery = useQuery(facultyDashboardQueryOptions());

  if (dashboardQuery.isLoading) {
    return (
      <div className="container py-10">
        <div className="rounded-lg border border-base-300 bg-base-100 p-8 text-center shadow-sm">
          <span className="loading loading-spinner loading-lg text-primary"></span>
          <p className="mt-4 text-sm font-semibold text-base-content/70">
            Loading your leave dashboard.
          </p>
        </div>
      </div>
    );
  }

  if (dashboardQuery.isError || !dashboardQuery.data) {
    return (
      <div className="container py-10">
        <PageErrorState
          badge="Faculty dashboard"
          code="500"
          description="We could not load your faculty dashboard right now."
          title="Dashboard unavailable"
        />
      </div>
    );
  }

  return <FacultyDashboardContent data={dashboardQuery.data} />;
}

function FacultyDashboardContent({ data }: { data: FacultyDashboardResponse }) {
  const [reportModalOpen, setReportModalOpen] = useState(false);
  const [historyModalOpen, setHistoryModalOpen] = useState(false);

  return (
    <div className="container py-8 lg:py-10">
      <div className="mx-auto grid max-w-6xl gap-5 lg:grid-cols-2">
        <AccrualBalancePanel balances={data.accrualBalances} />
        <div className="space-y-5">
          <QuickActionsPanel
            data={data}
            onReportLeave={() => setReportModalOpen(true)}
            onViewHistory={() => setHistoryModalOpen(true)}
          />
          <RecentRequestsPanel requests={data.recentRequests.slice(0, 5)} />
        </div>
      </div>

      <div className="mx-auto mt-6 max-w-6xl">
        <LeaveCalendar requests={data.recentRequests} />
      </div>

      {reportModalOpen ? (
        <ReportLeaveModal
          data={data}
          onClose={() => setReportModalOpen(false)}
        />
      ) : null}
      {historyModalOpen ? (
        <HistoryModal
          onClose={() => setHistoryModalOpen(false)}
          requests={data.recentRequests}
        />
      ) : null}
    </div>
  );
}

function AccrualBalancePanel({
  balances,
}: {
  balances: FacultyAccrualBalance[];
}) {
  return (
    <section className="min-h-72 rounded-lg border border-base-300 bg-base-100 p-6 shadow-sm">
      <div className="mb-6 flex items-center justify-between gap-4">
        <h2 className="font-bold text-primary">Leave Balances</h2>
        <span className="rounded-full bg-base-200 px-3 py-1 text-xs font-semibold text-base-content/50">
          Source: UCPath
        </span>
      </div>

      <div className="space-y-7">
        {balances.length > 0 ? (
          balances.map((balance) => (
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

function QuickActionsPanel({
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
      <div className="grid gap-3 sm:grid-cols-2">
        <button
          className="btn btn-secondary"
          onClick={onReportLeave}
          type="button"
        >
          + Report Leave
        </button>
        <button
          className="btn btn-outline btn-primary"
          onClick={onViewHistory}
          type="button"
        >
          View History
        </button>
      </div>
      <div className="mt-4 rounded-lg bg-base-200 px-4 py-3 text-sm text-base-content/70">
        <span className="font-bold text-base-content">Dept mode:</span>{' '}
        {data.faculty.departmentName ?? 'No reporting department'}.
      </div>
    </section>
  );
}

function RecentRequestsPanel({
  requests,
}: {
  requests: FacultyLeaveRequest[];
}) {
  return (
    <section className="rounded-lg border border-base-300 bg-base-100 p-6 shadow-sm">
      <h2 className="mb-5 font-bold text-primary">Recent Requests</h2>
      <div className="divide-y divide-base-300">
        {requests.length > 0 ? (
          requests.map((request) => (
            <RecentRequestRow key={request.id} request={request} />
          ))
        ) : (
          <EmptyPanelMessage message="No recent leave requests are on file." />
        )}
      </div>
    </section>
  );
}

function RecentRequestRow({ request }: { request: FacultyLeaveRequest }) {
  const tone = getLeaveTone(request.leaveType);

  return (
    <article className="grid grid-cols-[minmax(0,1fr)_auto_auto] items-center gap-3 py-3 first:pt-0 last:pb-0">
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
    </article>
  );
}

function LeaveCalendar({ requests }: { requests: FacultyLeaveRequest[] }) {
  const initialMonth = useMemo(
    () => getInitialCalendarMonth(requests),
    [requests]
  );
  const [visibleMonth, setVisibleMonth] = useState(initialMonth);
  const [selectedDate, setSelectedDate] = useState<string | null>(null);
  const calendarDays = useMemo(
    () => buildCalendarDays(visibleMonth),
    [visibleMonth]
  );
  const requestsByDate = useMemo(() => mapRequestsByDate(requests), [requests]);
  const selectedRequests = selectedDate
    ? (requestsByDate.get(selectedDate) ?? [])
    : [];

  return (
    <section className="rounded-lg border border-base-300 bg-base-100 p-6 shadow-sm">
      <div className="mb-5 flex items-center justify-between gap-4">
        <button
          className="btn btn-ghost btn-sm"
          onClick={() => setVisibleMonth(addMonths(visibleMonth, -1))}
          type="button"
        >
          <ArrowLeftIcon className="h-4 w-4" />
          Prev
        </button>
        <h2 className="font-bold text-primary">
          {monthFormatter.format(visibleMonth)}
        </h2>
        <button
          className="btn btn-ghost btn-sm"
          onClick={() => setVisibleMonth(addMonths(visibleMonth, 1))}
          type="button"
        >
          Next
          <ArrowRightIcon className="h-4 w-4" />
        </button>
      </div>

      <div className="grid grid-cols-7 border-l border-t border-base-300">
        {weekdayLabels.map((label) => (
          <div
            className="border-b border-r border-base-300 bg-base-200 py-2 text-center text-xs font-bold uppercase tracking-[0.12em] text-base-content/60"
            key={label}
          >
            {label}
          </div>
        ))}
        {calendarDays.map((day, index) => {
          const dayRequests = day
            ? (requestsByDate.get(day.isoDate) ?? [])
            : [];

          return (
            <button
              className={`min-h-24 border-b border-r border-base-300 p-2 text-left align-top transition ${
                day ? 'hover:bg-base-200' : 'bg-base-200/40'
              } ${selectedDate === day?.isoDate ? 'ring-2 ring-primary ring-inset' : ''}`}
              disabled={!day}
              key={day?.isoDate ?? `blank-${index}`}
              onClick={() => day && setSelectedDate(day.isoDate)}
              type="button"
            >
              {day ? (
                <>
                  <div className="text-xs font-semibold">{day.dayOfMonth}</div>
                  <div className="mt-3 space-y-1">
                    {dayRequests.slice(0, 2).map((request) => {
                      const tone = getLeaveTone(request.leaveType);

                      return (
                        <div
                          className={`truncate rounded border-l-2 px-1.5 py-0.5 text-[11px] font-semibold ${tone.surface}`}
                          key={request.id}
                        >
                          {request.leaveType}
                        </div>
                      );
                    })}
                  </div>
                </>
              ) : null}
            </button>
          );
        })}
      </div>

      <div className="mt-4 flex flex-wrap gap-4 text-xs text-base-content/70">
        {calendarLegend.map((item) => (
          <div className="flex items-center gap-2" key={item.label}>
            <span className={`h-3 w-3 rounded-sm border ${item.className}`} />
            {item.label}
          </div>
        ))}
      </div>

      {selectedDate ? (
        <div className="mt-4 rounded-lg bg-base-200 p-4">
          <div className="mb-2 flex items-center gap-2 text-sm font-bold">
            <CalendarDaysIcon className="h-4 w-4" />
            {formatDate(selectedDate)}
          </div>
          {selectedRequests.length > 0 ? (
            <div className="space-y-2">
              {selectedRequests.map((request) => (
                <div
                  className="flex items-center justify-between gap-3 text-sm"
                  key={request.id}
                >
                  <span>{request.leaveType}</span>
                  <RequestStatusBadge status={request.status} />
                </div>
              ))}
            </div>
          ) : (
            <p className="text-sm text-base-content/70">
              No leave requests on this day.
            </p>
          )}
        </div>
      ) : null}
    </section>
  );
}

function ReportLeaveModal({
  data,
  onClose,
}: {
  data: FacultyDashboardResponse;
  onClose: () => void;
}) {
  return (
    <ModalFrame onClose={onClose} title="Report Leave">
      <LeaveRequestForm data={data} onSubmitted={onClose} />
    </ModalFrame>
  );
}

function HistoryModal({
  onClose,
  requests,
}: {
  onClose: () => void;
  requests: FacultyLeaveRequest[];
}) {
  return (
    <ModalFrame onClose={onClose} title="Leave History">
      <div className="max-h-[65vh] overflow-y-auto">
        <div className="divide-y divide-base-300">
          {requests.length > 0 ? (
            requests.map((request) => (
              <RecentRequestRow key={request.id} request={request} />
            ))
          ) : (
            <EmptyPanelMessage message="No leave history is on file." />
          )}
        </div>
      </div>
    </ModalFrame>
  );
}

function ModalFrame({
  children,
  onClose,
  title,
}: {
  children: ReactNode;
  onClose: () => void;
  title: string;
}) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 p-4">
      <section className="w-full max-w-2xl rounded-lg bg-base-100 shadow-xl">
        <header className="flex items-center justify-between border-b border-base-300 px-6 py-4">
          <h2 className="text-lg font-bold text-primary">{title}</h2>
          <button
            aria-label="Close"
            className="btn btn-ghost btn-sm btn-circle"
            onClick={onClose}
            type="button"
          >
            <XMarkIcon className="h-5 w-5" />
          </button>
        </header>
        <div className="p-6">{children}</div>
      </section>
    </div>
  );
}

function LeaveRequestForm({
  data,
  onSubmitted,
}: {
  data: FacultyDashboardResponse;
  onSubmitted: () => void;
}) {
  const queryClient = useQueryClient();
  const requestMutation = useMutation({
    mutationFn: createFacultyLeaveRequest,
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: facultyDashboardQueryOptions().queryKey,
      });
      onSubmitted();
    },
  });

  const leaveTypeOptions = data.leaveTypes.map((type) => ({
    label: type.displayName,
    value: String(type.id),
  }));

  const accrualTypeOptions = data.leaveTypes
    .filter((type) => type.hasAccrualBalance)
    .map((type) => ({
      label: type.displayName,
      value: String(type.id),
    }));

  const form = useAppForm({
    defaultValues: {
      coveragePlan: '',
      endDate: '',
      leaveTypeId: '',
      note: '',
      payLeaveTypeId: '',
      startDate: '',
      totalHours: '',
    } satisfies LeaveRequestFormValues,
    onSubmit: async ({ value }) => {
      await requestMutation.mutateAsync({
        coveragePlan: value.coveragePlan.trim() || null,
        endDate: value.endDate,
        leaveTypeId: Number(value.leaveTypeId),
        note: value.note.trim() || null,
        payLeaveTypeId: value.payLeaveTypeId
          ? Number(value.payLeaveTypeId)
          : null,
        startDate: value.startDate,
        totalHours: Number(value.totalHours),
      });
      form.reset();
    },
    validators: {
      onChange: leaveRequestSchema,
    },
  });

  return (
    <form
      onSubmit={(event) => {
        event.preventDefault();
        void form.handleSubmit();
      }}
    >
      <form.AppForm>
        <div className="grid gap-4 sm:grid-cols-2">
          <form.AppField name="leaveTypeId">
            {(field) => (
              <field.SelectField
                label="Leave type"
                options={leaveTypeOptions}
                placeholder="Select leave"
              />
            )}
          </form.AppField>
          <form.AppField name="payLeaveTypeId">
            {(field) => (
              <field.SelectField
                helperText="Optional"
                label="Pay with"
                options={accrualTypeOptions}
                placeholder="No pay balance"
              />
            )}
          </form.AppField>
          <form.AppField name="startDate">
            {(field) => (
              <field.TextField
                helperText="YYYY-MM-DD"
                label="Start date"
                placeholder="2026-08-17"
              />
            )}
          </form.AppField>
          <form.AppField name="endDate">
            {(field) => (
              <field.TextField
                helperText="YYYY-MM-DD"
                label="End date"
                placeholder="2026-08-18"
              />
            )}
          </form.AppField>
          <form.AppField name="totalHours">
            {(field) => (
              <field.TextField
                helperText="Use decimal hours when needed"
                label="Total hours"
                placeholder="8"
              />
            )}
          </form.AppField>
        </div>

        <div className="mt-4 grid gap-4">
          <form.AppField name="coveragePlan">
            {(field) => (
              <field.TextField
                helperText="Optional"
                label="Coverage plan"
                placeholder="Coverage arranged with..."
              />
            )}
          </form.AppField>
          <form.AppField name="note">
            {(field) => (
              <field.TextField
                helperText="Optional"
                label="Note"
                placeholder="Add context for reviewers"
              />
            )}
          </form.AppField>
        </div>

        {requestMutation.isError ? (
          <div
            className={`mt-4 rounded-lg px-4 py-3 text-sm ${statusSurfaceColors.danger}`}
          >
            The request could not be submitted. Check the fields and try again.
          </div>
        ) : null}

        <div className="mt-6 flex justify-end">
          <form.SubscribeButton
            className="btn btn-primary"
            label={
              <span className="inline-flex items-center gap-2">
                <PaperAirplaneIcon className="h-4 w-4" />
                Submit request
              </span>
            }
            loadingLabel="Submitting"
          />
        </div>
      </form.AppForm>
    </form>
  );
}

function EmptyPanelMessage({ message }: { message: string }) {
  return <p className="text-sm text-base-content/70">{message}</p>;
}

function RequestStatusBadge({ status }: { status: string }) {
  if (status === 'Approved') {
    return <span className="badge badge-success badge-sm">Submitted</span>;
  }

  if (status === 'Denied') {
    return <span className="badge badge-error badge-sm">Denied</span>;
  }

  return <span className="badge badge-warning badge-sm">Pending</span>;
}

function getBalancePercentage(balance: FacultyAccrualBalance) {
  if (balance.accrualLimit <= 0) {
    return 100;
  }

  return Math.max(
    0,
    Math.min(100, (balance.calculatedBalance / balance.accrualLimit) * 100)
  );
}

function getLeaveTone(leaveType: string) {
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

function buildCalendarDays(visibleMonth: Date) {
  const year = visibleMonth.getFullYear();
  const month = visibleMonth.getMonth();
  const firstDay = new Date(year, month, 1);
  const daysInMonth = new Date(year, month + 1, 0).getDate();
  const days: Array<{ dayOfMonth: number; isoDate: string } | null> = [];

  for (let i = 0; i < firstDay.getDay(); i += 1) {
    days.push(null);
  }

  for (let day = 1; day <= daysInMonth; day += 1) {
    days.push({
      dayOfMonth: day,
      isoDate: toIsoDate(new Date(year, month, day)),
    });
  }

  while (days.length % 7 !== 0) {
    days.push(null);
  }

  return days;
}

function getInitialCalendarMonth(requests: FacultyLeaveRequest[]) {
  const firstRequest = requests[0];
  if (firstRequest) {
    const date = parseIsoDate(firstRequest.startDate);
    return new Date(date.getFullYear(), date.getMonth(), 1);
  }

  const today = new Date();
  return new Date(today.getFullYear(), today.getMonth(), 1);
}

function mapRequestsByDate(requests: FacultyLeaveRequest[]) {
  const map = new Map<string, FacultyLeaveRequest[]>();

  for (const request of requests) {
    const start = parseIsoDate(request.startDate);
    const end = parseIsoDate(request.endDate);

    for (
      let date = new Date(start);
      date <= end;
      date = new Date(date.getFullYear(), date.getMonth(), date.getDate() + 1)
    ) {
      const key = toIsoDate(date);
      map.set(key, [...(map.get(key) ?? []), request]);
    }
  }

  return map;
}

function addMonths(date: Date, offset: number) {
  return new Date(date.getFullYear(), date.getMonth() + offset, 1);
}

function parseIsoDate(value: string) {
  return new Date(`${value}T00:00:00`);
}

function toIsoDate(date: Date) {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');

  return `${year}-${month}-${day}`;
}

function formatHours(value: number) {
  return `${numberFormatter.format(value)} hrs`;
}

function formatDate(value: string) {
  return dateFormatter.format(parseIsoDate(value));
}

function formatDateRange(startDate: string, endDate: string) {
  if (startDate === endDate) {
    return formatDate(startDate);
  }

  return `${formatDate(startDate)} - ${formatDate(endDate)}`;
}
