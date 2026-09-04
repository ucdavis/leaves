import {
  CheckCircleIcon,
  XCircleIcon,
} from '@heroicons/react/24/outline';
import { Toast } from '@/shared/Toast.tsx';
import type { ApprovalDecision } from './approvalTypes.ts';

export type ApprovalToastMessage = {
  facultyName: string;
  id: number;
} & (
  | { decision: ApprovalDecision; kind: 'decision' }
  | { kind: 'error' }
  | { kind: 'alreadyHandled' }
);

export function ApprovalToast({
  message,
  onDismiss,
}: {
  message: ApprovalToastMessage | null;
  onDismiss: () => void;
}) {
  if (!message) {
    return null;
  }

  const isAlreadyHandled = message.kind === 'alreadyHandled';
  const isApproval = message.kind === 'decision' && message.decision === 'approved';
  const Icon = isApproval ? CheckCircleIcon : XCircleIcon;

  return (
    <Toast
      autoDismissMs={3000}
      icon={Icon}
      onDismiss={onDismiss}
      tone={isAlreadyHandled || isApproval ? 'success' : 'error'}
    >
      {isAlreadyHandled
        ? `${message.facultyName}'s request was already handled by another approver.`
        : message.kind === 'error'
          ? `We could not update ${message.facultyName}'s request. Please try again.`
          : `${message.facultyName}'s request was ${message.decision}.`}
    </Toast>
  );
}
