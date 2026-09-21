import { queryOptions } from '@tanstack/react-query';
import { fetchJson } from '@/lib/api.ts';
import type { UniversityHoliday } from '@/shared/calendar/universityHolidays.ts';

export const universityHolidaysQueryOptions = () =>
  queryOptions({
    queryFn: ({ signal }) =>
      fetchJson<UniversityHoliday[]>('/api/universityholidays', {}, signal),
    queryKey: ['university-holidays'] as const,
    retry: 2,
    staleTime: 60 * 60 * 1000,
  });
