import { createFileRoute } from '@tanstack/react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { HttpError } from '@/lib/api.ts';
import type { RouterContext } from '@/main.tsx';
import {
  approvalWorkspaceQueryOptions,
  submitApprovalDecision,
} from '@/queries/approvals.ts';
import { meQueryOptions } from '@/queries/user.ts';
import { ApprovalToast } from '@/shared/approvals/ApprovalToast.tsx';
import type { ApprovalToastMessage } from '@/shared/approvals/ApprovalToast.tsx';
import type {
  ApprovalDecision,
  ApprovalRequest,
} from '@/shared/approvals/approvalTypes.ts';
import { PendingApprovalsPanel } from '@/shared/approvals/PendingApprovalsPanel.tsx';
import { canAccessApprovalWorkspace } from '@/shared/auth/roleAccess.ts';

export const Route = createFileRoute('/(authenticated)/approvals')({
  beforeLoad: async ({ context }: { context: RouterContext }) => {
    const user = await context.queryClient.ensureQueryData(meQueryOptions());

    if (!canAccessApprovalWorkspace(user.roles)) {
      throw new HttpError(403, '/approvals');
    }
  },
  component: RouteComponent,
});

function RouteComponent() {
  const queryClient = useQueryClient();
  const workspaceQuery = useQuery(approvalWorkspaceQueryOptions());
  const [toastMessage, setToastMessage] = useState<ApprovalToastMessage | null>(
    null
  );
  const decisionMutation = useMutation({
    mutationFn: submitApprovalDecision,
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: approvalWorkspaceQueryOptions().queryKey,
      });
    },
  });

  if (workspaceQuery.isLoading) {
    return (
      <div className="container py-8 lg:py-10">
        <div className="mx-auto max-w-5xl rounded-lg border border-base-300 bg-base-100 p-8 text-center shadow-sm">
          <span className="loading loading-spinner loading-lg text-primary" />
          <p className="mt-4 text-sm font-semibold text-base-content/70">
            Loading pending approvals.
          </p>
        </div>
      </div>
    );
  }

  if (workspaceQuery.isError || !workspaceQuery.data) {
    return (
      <div className="container py-8 lg:py-10">
        <div className="mx-auto max-w-5xl rounded-lg border border-base-300 bg-base-100 p-8 text-center shadow-sm">
          <h2 className="text-lg font-bold text-primary">
            Pending approvals unavailable
          </h2>
          <p className="mt-2 text-sm text-base-content/70">
            We could not load your approval queue from the database.
          </p>
        </div>
      </div>
    );
  }

  const handleDecision = (
    request: ApprovalRequest,
    decision: ApprovalDecision
  ) =>
    void (async () => {
      try {
        await decisionMutation.mutateAsync({
          decision,
          requestId: request.id,
        });
        setToastMessage({
          decision,
          facultyName: request.facultyName,
          id: request.id,
        });
      } catch {
        // Let the panel remain visible so the user can retry.
      }
    })();

  return (
    <div className="container py-8 lg:py-10">
      <div className="mx-auto max-w-5xl">
        <PendingApprovalsPanel
          disabledRequestId={decisionMutation.variables?.requestId ?? null}
          onDecision={handleDecision}
          requests={workspaceQuery.data.pendingRequests}
        />
      </div>
      <ApprovalToast
        message={toastMessage}
        onDismiss={() => setToastMessage(null)}
      />
    </div>
  );
}
