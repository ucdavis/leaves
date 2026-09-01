import {
  ArrowLeftIcon,
  ArrowRightIcon,
} from '@heroicons/react/24/outline';
import { useNavigate } from '@tanstack/react-router';
import {
  addMonths,
  eachDayOfInterval,
  endOfDay,
  endOfMonth,
  format,
  isWithinInterval,
  parseISO,
  startOfMonth,
} from 'date-fns';
import { useMemo, useState } from 'react';
import type {
  ApprovalScope,
  CalendarFaculty,
  CalendarLeave,
  LeaveCategory,
} from './approvalTypes.ts';
import { formatDateRange, getLeaveTone } from './approvalDisplay.ts';

const leaveLegend: LeaveCategory[] = [
  'Compensatory Time',
  'Vacation',
  'Sick Leave',
  'Professional Development',
  'Sabbatical',
  'FMLA',
];

const monthFormatter = new Intl.DateTimeFormat(undefined, {
  month: 'long',
  year: 'numeric',
});

export function LeaveOverviewCalendar({
  faculty,
  leaves,
  scope,
}: {
  faculty: CalendarFaculty[];
  leaves: CalendarLeave[];
  scope: ApprovalScope;
}) {
  const navigate = useNavigate();
  const initialMonth = useMemo(() => getInitialCalendarMonth(leaves), [leaves]);
  const [visibleMonth, setVisibleMonth] = useState(initialMonth);
  const days = useMemo(() => buildMonthDays(visibleMonth), [visibleMonth]);

  return (
    <section className="rounded-lg border border-base-300 bg-base-100 p-6 shadow-sm">
      <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="h2 text-primary">
            {scope === 'cluster' ? 'BFTV Cluster' : 'Team'}: Leave Overview
          </h1>
        </div>
        <span className="rounded-full bg-secondary/70 px-4 py-1 text-sm font-bold text-primary">
          {faculty.length} faculty
        </span>
      </div>

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

      <div className="overflow-x-auto rounded-lg border border-base-300">
        <table className="table-fixed border-collapse text-sm">
          <thead>
            <tr>
              <th className="sticky left-0 z-10 w-52 border-r border-base-300 bg-base-200 px-3 py-3 text-left text-xs font-bold uppercase text-base-content/65">
                Faculty
              </th>
              {days.map((day) => (
                <th
                  className="w-9 border-r border-base-300 bg-base-200 px-0 py-3 text-center text-xs font-medium text-base-content/60 last:border-r-0"
                  key={day.isoDate}
                >
                  {day.dayOfMonth}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {faculty.map((facultyMember) => (
              <CalendarFacultyRow
                days={days}
                facultyMember={facultyMember}
                key={facultyMember.id}
                leaves={leaves}
                onSelectFaculty={(facultyId) =>
                  void navigate({
                    params: { iamId: facultyId },
                    to: '/faculty/$iamId',
                  })
                }
              />
            ))}
          </tbody>
        </table>
      </div>

      <CalendarLegend />

      <p className="mt-5 text-sm text-base-content/65">
        Click a faculty member&apos;s name to open their dashboard.
      </p>
    </section>
  );
}

function CalendarFacultyRow({
  days,
  facultyMember,
  leaves,
  onSelectFaculty,
}: {
  days: MonthDay[];
  facultyMember: CalendarFaculty;
  leaves: CalendarLeave[];
  onSelectFaculty: (facultyId: string) => void;
}) {
  const facultyLeaves = leaves.filter(
    (leave) => leave.facultyId === facultyMember.id
  );

  return (
    <tr>
      <th className="sticky left-0 z-10 border-r border-t border-base-300 bg-base-100 px-3 py-3 text-left">
        <button
          className="font-bold text-primary underline-offset-4 hover:underline"
          onClick={() => onSelectFaculty(facultyMember.id)}
          type="button"
        >
          {facultyMember.name}
        </button>
      </th>
      {days.map((day) => {
        const leave = facultyLeaves.find((item) => leaveIncludesDay(item, day));

        return (
          <CalendarDayCell
            day={day}
            facultyId={facultyMember.id}
            facultyName={facultyMember.name}
            key={day.isoDate}
            leave={leave}
            onSelectFaculty={onSelectFaculty}
          />
        );
      })}
    </tr>
  );
}

function CalendarDayCell({
  day,
  facultyId,
  facultyName,
  leave,
  onSelectFaculty,
}: {
  day: MonthDay;
  facultyId: string;
  facultyName: string;
  leave?: CalendarLeave;
  onSelectFaculty: (facultyId: string) => void;
}) {
  if (!leave) {
    return (
      <td className="h-11 border-r border-t border-base-300 bg-base-100 last:border-r-0" />
    );
  }

  const tone = getCalendarTone(leave.leaveType);
  const startsOnDay = leave.startDate === day.isoDate;
  const endsOnDay = leave.endDate === day.isoDate;

  return (
    <td className="border-r border-t border-base-300 last:border-r-0">
      <button
        aria-label={`View ${leave.leaveType} for ${facultyName}`}
        className={`h-11 w-full ${tone.background} text-left transition hover:brightness-[0.98] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/60 focus-visible:ring-inset ${
          startsOnDay ? `border-l-4 ${tone.border}` : ''
        } ${endsOnDay ? 'rounded-r-sm' : ''}`}
        onClick={() => onSelectFaculty(facultyId)}
        style={
          leave.status === 'PendingApproval'
            ? {
                backgroundImage:
                  'repeating-linear-gradient(45deg, rgb(255 255 255 / 0.45) 0 3px, transparent 3px 7px)',
              }
            : undefined
        }
        title={`${leave.leaveType}: ${formatDateRange(
          leave.startDate,
          leave.endDate
        )}`}
        type="button"
      />
    </td>
  );
}

function CalendarLegend() {
  return (
    <div className="mt-4 flex flex-wrap gap-4 text-xs text-base-content/70">
      {leaveLegend.map((leaveType) => {
        const tone = getCalendarTone(leaveType);

        return (
          <div className="flex items-center gap-2" key={leaveType}>
            <span className={`h-3 w-3 rounded-sm border ${tone.legend}`} />
            {leaveType}
          </div>
        );
      })}
      <div className="flex items-center gap-2">
        <span
          className="h-3 w-3 rounded-sm border border-primary/60 bg-blue-100"
          style={{
            backgroundImage:
              'repeating-linear-gradient(45deg, rgb(255 255 255 / 0.45) 0 3px, transparent 3px 7px)',
          }}
        />
        Pending
      </div>
    </div>
  );
}

function buildMonthDays(month: Date) {
  return eachDayOfInterval({
    end: endOfMonth(month),
    start: startOfMonth(month),
  }).map((day) => ({
    date: day,
    dayOfMonth: format(day, 'd'),
    isoDate: format(day, 'yyyy-MM-dd'),
  }));
}

function leaveIncludesDay(leave: CalendarLeave, day: MonthDay) {
  return isWithinInterval(day.date, {
    end: parseISO(leave.endDate),
    start: parseISO(leave.startDate),
  });
}

export function getInitialCalendarMonth(leaves: CalendarLeave[]) {
  const today = new Date();
  const currentMonth = startOfMonth(today);
  const currentMonthInterval = {
    end: endOfDay(endOfMonth(currentMonth)),
    start: currentMonth,
  };

  const leaveInCurrentMonth = leaves.find((leave) =>
    intervalsOverlap(
      {
        end: parseISO(leave.endDate),
        start: parseISO(leave.startDate),
      },
      currentMonthInterval
    )
  );
  if (leaveInCurrentMonth) {
    return currentMonth;
  }

  const upcomingLeave = [...leaves]
    .map((leave) => parseISO(leave.startDate))
    .filter((date) => date >= currentMonth)
    .sort((left, right) => left.getTime() - right.getTime())[0];
  if (upcomingLeave) {
    return startOfMonth(upcomingLeave);
  }

  const mostRecentPastLeave = [...leaves]
    .map((leave) => parseISO(leave.endDate))
    .sort((left, right) => right.getTime() - left.getTime())[0];
  if (mostRecentPastLeave) {
    return startOfMonth(mostRecentPastLeave);
  }

  return currentMonth;
}

function intervalsOverlap(
  left: { end: Date; start: Date },
  right: { end: Date; start: Date }
) {
  return left.start <= right.end && left.end >= right.start;
}

function getCalendarTone(leaveType: LeaveCategory) {
  return getLeaveTone(leaveType);
}

type MonthDay = ReturnType<typeof buildMonthDays>[number];
