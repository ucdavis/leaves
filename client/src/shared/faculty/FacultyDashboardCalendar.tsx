import { ArrowLeftIcon, ArrowRightIcon } from '@heroicons/react/24/outline';
import {
  addMonths,
  eachDayOfInterval,
  endOfMonth,
  endOfWeek,
  format,
  isSameMonth,
  parseISO,
  startOfMonth,
  startOfWeek,
} from 'date-fns';
import { useMemo, useState } from 'react';
import type {
  FacultyDashboardResponse,
  FacultyLeaveRequest,
} from '@/queries/faculty.ts';
import { RequestDetailModal } from './FacultyDashboardModals.tsx';
import { formatDateRange, getLeaveTone } from './FacultyDashboardPanels.tsx';

const monthFormatter = new Intl.DateTimeFormat(undefined, {
  month: 'long',
  year: 'numeric',
});

export const calendarLegend = [
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

export const weekdayLabels = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

export function LeaveCalendar({
  faculty,
  requests,
}: {
  faculty: FacultyDashboardResponse['faculty'];
  requests: FacultyLeaveRequest[];
}) {
  const initialMonth = useMemo(
    () => getInitialCalendarMonth(requests),
    [requests]
  );
  const [visibleMonth, setVisibleMonth] = useState(initialMonth);
  const [selectedDate, setSelectedDate] = useState<string | null>(null);
  const [selectedRequest, setSelectedRequest] =
    useState<FacultyLeaveRequest | null>(null);
  const calendarDays = useMemo(
    () => buildCalendarDays(visibleMonth),
    [visibleMonth]
  );
  const requestsByDate = useMemo(() => mapRequestsByDate(requests), [requests]);

  return (
    <section className="rounded-lg border border-base-300 bg-base-100 p-6 shadow-sm">
      <div className="mb-5 flex items-center justify-between gap-4">
        <button
          className="btn btn-ghost btn-sm"
          onClick={() => setVisibleMonth((current) => addMonths(current, -1))}
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
          onClick={() => setVisibleMonth((current) => addMonths(current, 1))}
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
            <div
              className={`min-h-24 border-b border-r border-base-300 p-2 text-left align-top transition ${
                day ? 'hover:bg-base-200' : 'bg-base-200/40'
              } ${selectedDate === day?.isoDate ? 'ring-2 ring-primary ring-inset' : ''} ${
                day ? 'cursor-pointer' : ''
              }`}
              key={day?.isoDate ?? `blank-${index}`}
              onClick={() => day && setSelectedDate(day.isoDate)}
              onKeyDown={(event) => {
                if (!day) {
                  return;
                }

                if (event.key === 'Enter' || event.key === ' ') {
                  event.preventDefault();
                  setSelectedDate(day.isoDate);
                }
              }}
              role={day ? 'button' : undefined}
              tabIndex={day ? 0 : -1}
            >
              {day ? (
                <>
                  <div className="text-xs font-semibold">{day.dayOfMonth}</div>
                  <div className="mt-3 space-y-1">
                    {dayRequests.map((request) => {
                      const tone = getLeaveTone(request.leaveType);

                      return (
                        <button
                          aria-label={`View ${request.leaveType} request from ${formatDateRange(
                            request.startDate,
                            request.endDate
                          )}`}
                          className={`block w-full truncate rounded border-l-2 px-1.5 py-0.5 text-left text-[11px] font-semibold transition hover:opacity-90 ${tone.surface}`}
                          key={request.id}
                          onClick={(event) => {
                            event.stopPropagation();
                            setSelectedRequest(request);
                          }}
                          type="button"
                        >
                          {request.leaveType}
                        </button>
                      );
                    })}
                  </div>
                </>
              ) : null}
            </div>
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

      {selectedRequest ? (
        <RequestDetailModal
          faculty={faculty}
          onClose={() => setSelectedRequest(null)}
          request={selectedRequest}
        />
      ) : null}
    </section>
  );
}

function buildCalendarDays(visibleMonth: Date) {
  const days = eachDayOfInterval({
    end: endOfWeek(endOfMonth(visibleMonth)),
    start: startOfWeek(startOfMonth(visibleMonth)),
  });

  return days.map((day) =>
    isSameMonth(day, visibleMonth)
      ? {
          dayOfMonth: day.getDate(),
          isoDate: format(day, 'yyyy-MM-dd'),
        }
      : null
  );
}

function getInitialCalendarMonth(requests: FacultyLeaveRequest[]) {
  const firstRequest = requests[0];
  if (firstRequest) {
    return startOfMonth(parseISO(firstRequest.startDate));
  }

  const today = new Date();
  return startOfMonth(today);
}

function mapRequestsByDate(requests: FacultyLeaveRequest[]) {
  const map = new Map<string, FacultyLeaveRequest[]>();

  for (const request of requests) {
    const dates = eachDayOfInterval({
      end: parseISO(request.endDate),
      start: parseISO(request.startDate),
    });

    for (const date of dates) {
      const key = format(date, 'yyyy-MM-dd');
      map.set(key, [...(map.get(key) ?? []), request]);
    }
  }

  return map;
}
