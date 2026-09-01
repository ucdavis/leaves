import { queryOptions } from '@tanstack/react-query';
import { fetchJson } from '@/lib/api.ts';
import type {
  ApprovalDecision,
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

export type ApprovalDecisionInput = {
  decision: ApprovalDecision;
  requestId: number;
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

export async function submitApprovalDecision({
  decision,
  requestId,
}: ApprovalDecisionInput) {
  await fetchJson<void>(
    `/api/approvalworkspace/requests/${requestId}/decision`,
    {
      body: JSON.stringify({ decision }),
      method: 'POST',
    }
  );
}
