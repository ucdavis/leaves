import { createFileRoute } from '@tanstack/react-router';
import { HttpError } from '@/lib/api.ts';
import type { RouterContext } from '@/main.tsx';
import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
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
  const [dismissedRequestIds, setDismissedRequestIds] = useState<number[]>([]);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [toastMessage, setToastMessage] =
    useState<ApprovalToastMessage | null>(null);

  const decisionMutation = useMutation({
    mutationFn: submitApprovalDecision,
    onError: () => {
      setErrorMessage('We could not save that decision. Please try again.');
    },
    onSuccess: async (_data, variables) => {
      setDismissedRequestIds((current) => [...current, variables.requestId]);
      setToastMessage({
        decision: variables.decision,
        facultyName: variables.facultyName,
        id: variables.requestId,
      });
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

  const requests = workspaceQuery.data.pendingRequests.filter(
    (request) => !dismissedRequestIds.includes(request.id)
  );

  const handleDecision = (
    request: ApprovalRequest,
    decision: ApprovalDecision
  ) => {
    setErrorMessage(null);
    decisionMutation.mutate({
      decision,
      facultyName: request.facultyName,
      requestId: request.id,
    });
  };

  return (
    <div className="container py-8 lg:py-10">
      <div className="mx-auto max-w-5xl">
        {errorMessage ? (
          <div className="alert alert-error mb-4" role="alert">
            {errorMessage}
          </div>
        ) : null}
        <PendingApprovalsPanel
          isSubmitting={decisionMutation.isPending}
          onDecision={handleDecision}
          requests={requests}
        />
      </div>
      <ApprovalToast
        message={toastMessage}
        onDismiss={() => setToastMessage(null)}
      />
    </div>
  );
}
