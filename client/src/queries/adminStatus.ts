import { fetchJson } from '@/lib/api.ts';
import type { AdminDataSource } from '@/shared/admin/adminData.tsx';

export type AdminStatusSnapshot = {
  issues: {
    approachingVacationCap: number;
    facultyAtVacationCap: number;
    pendingRequests: number;
  };
};

export type AdminStatusPageData = {
  clusterCount: number;
  clustersMissingCaos: number;
  dataSources: AdminDataSource[];
  departmentCount: number;
  departmentsMissingChairs: number;
  statusSnapshot: AdminStatusSnapshot;
};

export const adminStatusQueryOptions = () => ({
  queryFn: async ({
    signal,
  }: {
    signal: AbortSignal;
  }): Promise<AdminStatusPageData> => {
    return await fetchJson<AdminStatusPageData>('/api/admin/status', {}, signal);
  },
  queryKey: ['admin', 'status'] as const,
});
