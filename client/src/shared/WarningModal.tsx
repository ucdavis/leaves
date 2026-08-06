import { useEffect, useId, useRef, type ReactNode } from 'react';

export function WarningModal({
  children,
  confirmLabel,
  description,
  errorMessage,
  isConfirmDisabled,
  isSaving,
  onCancel,
  onConfirm,
  title,
}: {
  children: ReactNode;
  confirmLabel: string;
  description?: string;
  errorMessage?: string | null;
  isConfirmDisabled?: boolean;
  isSaving: boolean;
  onCancel: () => void;
  onConfirm: () => void;
  title: string;
}) {
  const titleId = useId();
  const descriptionId = useId();
  const cancelButtonRef = useRef<HTMLButtonElement | null>(null);
  const dialogRef = useRef<HTMLDialogElement | null>(null);

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) {
      return undefined;
    }

    if (!dialog.open) {
      dialog.showModal();
    }

    cancelButtonRef.current?.focus({ preventScroll: true });

    function handleCancel(event: Event) {
      if (isSaving) {
        event.preventDefault();
        return;
      }

      event.preventDefault();
      onCancel();
    }

    dialog.addEventListener('cancel', handleCancel);
    return () => {
      dialog.removeEventListener('cancel', handleCancel);
      if (dialog.open) {
        dialog.close();
      }
    };
  }, [isSaving, onCancel]);

  return (
    <dialog
      aria-describedby={description ? descriptionId : undefined}
      aria-labelledby={titleId}
      aria-modal="true"
      className="fixed inset-0 z-50 m-0 flex h-dvh w-dvw max-h-none max-w-none items-center justify-center border-0 bg-transparent p-4 backdrop:bg-slate-950/40"
      onClick={(event) => {
        if (event.target === event.currentTarget && !isSaving) {
          onCancel();
        }
      }}
      ref={dialogRef}
      tabIndex={-1}
    >
      <div className="max-h-[90vh] w-full max-w-xl overflow-y-auto rounded-[1.5rem] border border-base-300 bg-base-100 p-6 shadow-2xl">
        <div className="mb-6">
          <h2 className="text-xl font-semibold text-base-content" id={titleId}>
            {title}
          </h2>
          {description ? (
            <p className="mt-2 text-sm text-base-content/70" id={descriptionId}>
              {description}
            </p>
          ) : null}
        </div>

        <div className="rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
          {children}
        </div>

        {errorMessage ? (
          <div
            className="mt-4 rounded-2xl border border-error/20 bg-error/10 px-4 py-3 text-sm text-error"
            role="alert"
          >
            {errorMessage}
          </div>
        ) : null}

        <div className="mt-6 flex justify-end gap-3">
          <button
            className="btn btn-ghost"
            disabled={isSaving}
            onClick={onCancel}
            ref={cancelButtonRef}
            type="button"
          >
            Cancel
          </button>
          <button
            className="btn border-0 bg-rose-700 text-white hover:bg-rose-800 disabled:border-neutral-200 disabled:bg-neutral-300 disabled:text-neutral-500"
            disabled={isSaving || isConfirmDisabled}
            onClick={onConfirm}
            type="button"
          >
            {isSaving ? 'Saving...' : confirmLabel}
          </button>
        </div>
      </div>
    </dialog>
  );
}
