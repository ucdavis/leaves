import {
  CheckCircleIcon,
  XCircleIcon,
} from '@heroicons/react/24/outline';
import { Toast } from '@/shared/Toast.tsx';
import type { ApprovalDecision } from './approvalTypes.ts';

export type ApprovalToastMessage = {
  decision: ApprovalDecision;
  facultyName: string;
  id: number;
};

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

  const isApproval = message.decision === 'approved';
  const Icon = isApproval ? CheckCircleIcon : XCircleIcon;

  return (
    <Toast
      autoDismissMs={3000}
      icon={Icon}
      onDismiss={onDismiss}
      tone={isApproval ? 'success' : 'error'}
    >
      {message.facultyName}&apos;s request was {message.decision}.
    </Toast>
  );
}
