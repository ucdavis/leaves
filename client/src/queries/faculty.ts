import { queryOptions } from '@tanstack/react-query';
import { fetchJson } from '@/lib/api.ts';

const dashboardRequestTimeoutMs = 12_000;

export interface FacultyDashboardResponse {
  accrualBalances: FacultyAccrualBalance[];
  faculty: FacultyProfile;
  leaveTypes: FacultyLeaveType[];
  recentRequests: FacultyLeaveRequest[];
  snapshot: FacultyDashboardSnapshot;
}

export interface FacultyProfile {
  departmentCode?: string | null;
  departmentName?: string | null;
  email?: string | null;
  employeeClass?: string | null;
  employeeId?: string | null;
  iamId: string;
  jobTitle?: string | null;
  latestSnapshotDate?: string | null;
  name: string;
}

export interface FacultyDashboardSnapshot {
  accrualsApproachingCap: number;
  approvedRequests: number;
  availableBalanceHours: number;
  pendingRequests: number;
}

export interface FacultyAccrualBalance {
  accrualLimit: number;
  accrualPercentage: number;
  approachingMax: string;
  calculatedBalance: number;
  hasDivergentPositionBalances: boolean;
  latestAsOfDate: string;
  typeLabel: string;
}

export interface FacultyLeaveRequest {
  departmentName: string;
  endDate: string;
  id: number;
  leaveType: string;
  note?: string | null;
  payLeaveType?: string | null;
  startDate: string;
  status: 'Approved' | 'Denied' | 'PendingApproval' | string;
  submittedAt: string;
  totalHours: number;
  workflowMode: string;
}

export interface FacultyLeaveType {
  displayName: string;
  hasAccrualBalance: boolean;
  id: number;
}

export interface CreateFacultyLeaveRequest {
  coveragePlan?: string | null;
  endDate: string;
  leaveTypeId: number;
  note?: string | null;
  payLeaveTypeId?: number | null;
  startDate: string;
  totalHours: number;
}

export const facultyDashboardQueryOptions = () =>
  queryOptions({
    queryFn: ({ signal }) =>
      fetchJsonWithTimeout<FacultyDashboardResponse>(
        '/api/faculty/dashboard',
        dashboardRequestTimeoutMs,
        signal
      ),
    queryKey: ['faculty', 'dashboard'] as const,
    retry: false,
  });

export const facultyHistoryQueryOptions = () =>
  queryOptions({
    queryFn: ({ signal }) =>
      fetchJsonWithTimeout<FacultyDashboardResponse>(
        '/api/faculty/history',
        dashboardRequestTimeoutMs,
        signal
      ),
    queryKey: ['faculty', 'history'] as const,
    retry: false,
  });

export async function createFacultyLeaveRequest(
  request: CreateFacultyLeaveRequest
) {
  return await fetchJson<{ id: number }>('/api/faculty/requests', {
    body: JSON.stringify(request),
    method: 'POST',
  });
}

async function fetchJsonWithTimeout<T>(
  url: string,
  timeoutMs: number,
  signal?: AbortSignal
) {
  const controller = new AbortController();
  const timeoutId = window.setTimeout(() => controller.abort(), timeoutMs);
  const abortFromCaller = () => controller.abort();

  if (signal?.aborted) {
    controller.abort();
  } else {
    signal?.addEventListener('abort', abortFromCaller, { once: true });
  }

  try {
    return await fetchJson<T>(
      url,
      {},
      controller.signal
    );
  } finally {
    window.clearTimeout(timeoutId);
    signal?.removeEventListener('abort', abortFromCaller);
  }
}
