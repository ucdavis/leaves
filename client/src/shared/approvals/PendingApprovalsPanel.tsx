import { CheckIcon } from '@heroicons/react/24/outline';
import { useState } from 'react';
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
  onDecision: (
    request: ApprovalRequest,
    decision: ApprovalDecision,
    comment: string
  ) => void;
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
  onDecision: (
    request: ApprovalRequest,
    decision: ApprovalDecision,
    comment: string
  ) => void;
  request: ApprovalRequest;
}) {
  const tone = getLeaveTone(request.leaveType);
  const [comment, setComment] = useState('');
  const [commentError, setCommentError] = useState<string | null>(null);

  const denyRequest = () => {
    if (!comment.trim()) {
      setCommentError('A comment is required when denying a request.');
      return;
    }

    setCommentError(null);
    onDecision(request, 'denied', comment);
  };

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
      <div className="sm:col-start-2">
        <label className="form-control w-full">
          <span className="label pb-1 text-xs font-semibold text-base-content/70">
            Comment
          </span>
          <textarea
            aria-describedby={commentError ? `comment-error-${request.id}` : undefined}
            aria-invalid={Boolean(commentError)}
            className="textarea textarea-bordered h-8 min-h-8 w-full resize-none overflow-y-hidden py-1 text-sm"
            disabled={disabled}
            maxLength={2000}
            onChange={(event) => {
              event.currentTarget.style.height = 'auto';
              event.currentTarget.style.height = `${event.currentTarget.scrollHeight}px`;
              setComment(event.target.value);
              if (commentError) {
                setCommentError(null);
              }
            }}
            placeholder="Add context for the faculty member and leave administrators"
            value={comment}
          />
          <div className="mt-1">
            <div className="text-right text-xs text-base-content/55">
              {comment.length}/2000
            </div>
            {commentError ? (
              <div className="mt-1 text-sm text-error" id={`comment-error-${request.id}`}>
                {commentError}
              </div>
            ) : null}
          </div>
        </label>
      </div>
      <div
        className={`flex flex-wrap gap-2 sm:col-start-3 sm:justify-end ${
          commentError ? 'sm:-translate-y-3' : ''
        }`}
      >
        <button
          className="btn btn-primary btn-sm"
          disabled={disabled}
          onClick={() => onDecision(request, 'approved', comment)}
          type="button"
        >
          Approve
        </button>
        <button
          className="btn btn-error btn-sm"
          disabled={disabled}
          onClick={denyRequest}
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
