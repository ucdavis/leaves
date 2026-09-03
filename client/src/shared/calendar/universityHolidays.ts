export type UniversityHoliday = {
  /** ISO date, kept explicit so each academic year's published calendar is easy to review. */
  date: string;
  name: string;
};

/**
 * UC Davis closures and academic breaks published for 2025-26 and 2026-27.
 *
 * Add the next published academic year's dates here when it becomes available.
 */
export const universityHolidays: readonly UniversityHoliday[] = [
  { date: '2025-11-11', name: 'Veterans Day' },
  { date: '2025-11-27', name: 'Thanksgiving Holiday' },
  { date: '2025-11-28', name: 'Thanksgiving Holiday' },
  { date: '2025-12-24', name: 'Winter Holiday' },
  { date: '2025-12-25', name: 'Winter Holiday' },
  { date: '2025-12-31', name: 'Winter Holiday' },
  { date: '2026-01-01', name: 'Winter Holiday' },
  { date: '2026-01-19', name: 'Martin Luther King, Jr. Day' },
  { date: '2026-02-16', name: 'Presidents’ Day' },
  { date: '2026-03-23', name: 'Spring Break' },
  { date: '2026-03-24', name: 'Spring Break' },
  { date: '2026-03-25', name: 'Spring Break' },
  { date: '2026-03-26', name: 'Spring Break' },
  { date: '2026-03-27', name: 'Farmworkers Day' },
  { date: '2026-05-25', name: 'Memorial Day' },
  { date: '2026-06-19', name: 'Juneteenth Holiday' },
  { date: '2026-07-03', name: 'Independence Day' },
  { date: '2026-09-07', name: 'Labor Day' },
  { date: '2026-11-11', name: 'Veterans Day' },
  { date: '2026-11-26', name: 'Thanksgiving Holiday' },
  { date: '2026-11-27', name: 'Thanksgiving Holiday' },
  { date: '2026-12-24', name: 'Winter Holiday' },
  { date: '2026-12-25', name: 'Winter Holiday' },
  { date: '2026-12-31', name: 'Winter Holiday' },
  { date: '2027-01-01', name: 'Winter Holiday' },
  { date: '2027-01-18', name: 'Martin Luther King, Jr. Day' },
  { date: '2027-02-15', name: 'Presidents’ Day' },
  { date: '2027-03-22', name: 'Spring Break' },
  { date: '2027-03-23', name: 'Spring Break' },
  { date: '2027-03-24', name: 'Spring Break' },
  { date: '2027-03-25', name: 'Spring Break' },
  { date: '2027-03-26', name: 'Farmworkers Day' },
  { date: '2027-05-31', name: 'Memorial Day' },
  { date: '2027-06-18', name: 'Juneteenth Holiday' },
  { date: '2027-07-05', name: 'Independence Day' },
  { date: '2027-09-06', name: 'Labor Day' },
];

const holidaysByDate = new Map(
  universityHolidays.map((holiday) => [holiday.date, holiday])
);

export function getUniversityHoliday(date: string) {
  return holidaysByDate.get(date);
}

export function getLeaveDayCount(
  startDate: string,
  endDate: string,
  excludeWeekends: boolean,
  excludeUniversityHolidays: boolean
) {
  const start = parseIsoDate(startDate);
  const end = parseIsoDate(endDate);

  if (!start || !end || end < start) {
    return 0;
  }

  let count = 0;
  const day = new Date(start);

  while (day <= end) {
    const isoDate = formatIsoDate(day);
    const weekend = day.getUTCDay() === 0 || day.getUTCDay() === 6;

    if (
      (!excludeWeekends || !weekend) &&
      (!excludeUniversityHolidays || !getUniversityHoliday(isoDate))
    ) {
      count += 1;
    }

    day.setUTCDate(day.getUTCDate() + 1);
  }

  return count;
}

function parseIsoDate(value: string) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    return undefined;
  }

  const date = new Date(`${value}T00:00:00.000Z`);
  return Number.isNaN(date.getTime()) ? undefined : date;
}

function formatIsoDate(date: Date) {
  return date.toISOString().slice(0, 10);
}
