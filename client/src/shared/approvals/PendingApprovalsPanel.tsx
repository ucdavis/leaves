import { CheckIcon } from '@heroicons/react/24/outline';
import type {
  ApprovalDecision,
  ApprovalRequest,
} from './approvalTypes.ts';
import {
  formatCompactHours,
  formatDateRange,
  getLeaveTone,
} from './approvalDisplay.ts';

export function PendingApprovalsPanel({
  isSubmitting = false,
  onDecision,
  requests,
}: {
  isSubmitting?: boolean;
  onDecision: (request: ApprovalRequest, decision: ApprovalDecision) => void;
  requests: ApprovalRequest[];
}) {
  return (
    <section className="rounded-lg border border-base-300 bg-base-100 p-6 shadow-sm">
      <h1 className="h2 mb-6 text-primary">Pending Approvals</h1>
      {requests.length > 0 ? (
        <div className="space-y-4">
          {requests.map((request) => (
            <ApprovalRequestCard
              disabled={isSubmitting}
              key={request.id}
              onDecision={onDecision}
              request={request}
            />
          ))}
        </div>
      ) : (
        <NoPendingApprovals />
      )}
    </section>
  );
}

function ApprovalRequestCard({
  disabled = false,
  onDecision,
  request,
}: {
  disabled?: boolean;
  onDecision: (request: ApprovalRequest, decision: ApprovalDecision) => void;
  request: ApprovalRequest;
}) {
  const tone = getLeaveTone(request.leaveType);

  return (
    <article className="grid gap-4 rounded-lg border border-base-300 bg-base-200/35 p-4 sm:grid-cols-[auto_minmax(0,1fr)_auto] sm:items-center">
      <div className="flex h-12 w-12 items-center justify-center rounded-full bg-primary text-sm font-bold text-primary-content">
        {request.facultyInitials}
      </div>
      <div className="min-w-0">
        <div className="font-bold text-base-content">{request.facultyName}</div>
        <div className="mt-1 text-sm text-base-content/70">
          <span className={`font-bold ${tone.text}`}>{request.leaveType}</span>
          <span> · {formatDateRange(request.startDate, request.endDate)}</span>
          <span> · {formatCompactHours(request.totalHours)}</span>
        </div>
        <div className="mt-1 text-xs text-base-content/60">
          {request.note ? `Note: ${request.note}` : request.departmentName}
        </div>
      </div>
      <div className="flex flex-wrap gap-2 sm:justify-end">
        <button
          className="btn btn-primary btn-sm"
          disabled={disabled}
          onClick={() => onDecision(request, 'approved')}
          type="button"
        >
          Approve
        </button>
        <button
          className="btn btn-error btn-sm"
          disabled={disabled}
          onClick={() => onDecision(request, 'denied')}
          type="button"
        >
          Deny
        </button>
      </div>
    </article>
  );
}

function NoPendingApprovals() {
  return (
    <div className="flex min-h-52 flex-col items-center justify-center gap-4 text-center text-base-content/65">
      <CheckIcon className="h-12 w-12 stroke-[1.5]" />
      <p className="text-base font-medium">No pending approvals. All caught up!</p>
    </div>
  );
}
