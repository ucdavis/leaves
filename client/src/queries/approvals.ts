import { queryOptions } from '@tanstack/react-query';
import { fetchJson } from '@/lib/api.ts';
import type {
  ApprovalRequest,
  ApprovalScope,
  CalendarFaculty,
  CalendarLeave,
} from '@/shared/approvals/approvalTypes.ts';

export type ApprovalWorkspaceResponse = {
  faculty: CalendarFaculty[];
  leaves: CalendarLeave[];
  pendingRequests: ApprovalRequest[];
  scope: ApprovalScope;
};

export const approvalWorkspaceQueryOptions = () =>
  queryOptions({
    queryFn: async ({ signal }: { signal: AbortSignal }) => {
      return await fetchJson<ApprovalWorkspaceResponse>(
        '/api/approvalworkspace',
        {},
        signal
      );
    },
    queryKey: ['approval-workspace'] as const,
    retry: false,
  });
