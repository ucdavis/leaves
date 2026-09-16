export type UniversityHoliday = {
  date: string;
  name: string;
};

export function getUniversityHoliday(
  holidays: readonly UniversityHoliday[],
  date: string
) {
  return holidays.find((holiday) => holiday.date === date);
}

export function getLeaveDayCount(
  holidays: readonly UniversityHoliday[],
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
      (!excludeUniversityHolidays || !getUniversityHoliday(holidays, isoDate))
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
