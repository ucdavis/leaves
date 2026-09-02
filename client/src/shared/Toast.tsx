import { type ComponentType, useEffect, type ReactNode, type SVGProps } from 'react';

type ToastTone = 'success' | 'error';

const toneClasses: Record<
  ToastTone,
  {
    panel: string;
  }
> = {
  error: {
    panel: 'bg-error text-error-content',
  },
  success: {
    panel: 'bg-success text-success-content',
  },
};

export function Toast({
  autoDismissMs,
  children,
  className = 'fixed right-6 top-6 z-50 w-[min(calc(100vw-3rem),28rem)]',
  icon: Icon,
  onDismiss,
  tone,
}: {
  autoDismissMs?: number;
  children: ReactNode;
  className?: string;
  icon: ComponentType<SVGProps<SVGSVGElement>>;
  onDismiss: () => void;
  tone: ToastTone;
}) {
  useEffect(() => {
    if (!autoDismissMs) {
      return;
    }

    const timeoutId = window.setTimeout(onDismiss, autoDismissMs);
    return () => window.clearTimeout(timeoutId);
  }, [autoDismissMs, onDismiss]);

  return (
    <div
      aria-live="polite"
      className={className}
      role="status"
    >
      <div
        className={`flex items-start gap-3 rounded-lg px-5 py-4 text-sm font-semibold shadow-lg ${toneClasses[tone].panel}`}
      >
        <Icon className="mt-0.5 h-5 w-5 shrink-0" />
        <span className="flex-1">{children}</span>
        <button
          aria-label="Dismiss notification"
          className="btn btn-ghost btn-xs -mr-2 -mt-1 text-current hover:bg-black/10"
          onClick={onDismiss}
          type="button"
        >
          <span aria-hidden="true">×</span>
        </button>
      </div>
    </div>
  );
}
