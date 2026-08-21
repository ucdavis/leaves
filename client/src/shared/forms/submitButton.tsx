import type { ReactNode } from 'react';
import { useFormContext } from './formContext.tsx';

export function SubscribeButton({
  className,
  label,
  loadingLabel,
  onClick,
  type,
}: {
  className?: string;
  label: ReactNode;
  loadingLabel?: string;
  onClick?: () => void;
  type?: 'button' | 'submit';
}) {
  const form = useFormContext();
  return (
    <form.Subscribe selector={(state) => state.isSubmitting}>
      {(isSubmitting) => (
        <button
          className={className ?? 'btn btn-primary w-full'}
          disabled={isSubmitting}
          onClick={onClick}
          type={type ?? 'submit'}
        >
          {isSubmitting ? (
            <>
              <span className="loading loading-spinner loading-xs mr-2"></span>
              {loadingLabel ?? 'Submitting...'}
            </>
          ) : (
            label
          )}
        </button>
      )}
    </form.Subscribe>
  );
}
