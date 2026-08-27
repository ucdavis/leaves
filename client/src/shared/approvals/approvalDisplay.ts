import type { LeaveCategory } from './approvalTypes.ts';

type Tone = {
  background: string;
  border: string;
  legend: string;
  text: string;
};

export function formatDateRange(startDate: string, endDate: string) {
  const start = formatDate(startDate);
  const end = formatDate(endDate);

  return start === end ? start : `${start} - ${end}`;
}

export function formatCompactHours(hours: number) {
  return hours === 1 ? '1 hour' : `${hours} hours`;
}

export function getLeaveTone(leaveType: LeaveCategory): Tone {
  switch (leaveType) {
    case 'Compensatory Time':
      return {
        background: 'bg-slate-100',
        border: 'border-slate-500',
        legend: 'border-slate-500 bg-slate-100',
        text: 'text-slate-700',
      };
    case 'Sick Leave':
      return {
        background: 'bg-emerald-100',
        border: 'border-emerald-500',
        legend: 'border-emerald-500 bg-emerald-100',
        text: 'text-emerald-700',
      };
    case 'Professional Development':
      return {
        background: 'bg-violet-100',
        border: 'border-violet-500',
        legend: 'border-violet-500 bg-violet-100',
        text: 'text-violet-700',
      };
    case 'Sabbatical':
      return {
        background: 'bg-red-100',
        border: 'border-red-500',
        legend: 'border-red-500 bg-red-100',
        text: 'text-red-700',
      };
    case 'FMLA':
      return {
        background: 'bg-orange-100',
        border: 'border-orange-500',
        legend: 'border-orange-500 bg-orange-100',
        text: 'text-orange-700',
      };
    case 'Vacation':
      return {
        background: 'bg-blue-100',
        border: 'border-blue-600',
        legend: 'border-blue-600 bg-blue-100',
        text: 'text-blue-700',
      };
  }
}

function formatDate(date: string) {
  return new Intl.DateTimeFormat(undefined, {
    day: 'numeric',
    month: 'numeric',
    year: 'numeric',
  }).format(new Date(`${date}T00:00:00`));
}
