import type { ReactNode } from 'react';

export function WarningModal({
  children,
  confirmLabel,
  description,
  isSaving,
  onCancel,
  onConfirm,
  title,
}: {
  children: ReactNode;
  confirmLabel: string;
  description?: string;
  isSaving: boolean;
  onCancel: () => void;
  onConfirm: () => void;
  title: string;
}) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/40 px-4 py-8">
      <div className="max-h-[90vh] w-full max-w-xl overflow-y-auto rounded-[1.5rem] border border-base-300 bg-base-100 p-6 shadow-2xl">
        <div className="mb-6">
          <h2 className="text-xl font-semibold text-base-content">{title}</h2>
          {description ? (
            <p className="mt-2 text-sm text-base-content/70">{description}</p>
          ) : null}
        </div>

        <div className="rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
          {children}
        </div>

        <div className="mt-6 flex justify-end gap-3">
          <button
            className="btn btn-ghost"
            disabled={isSaving}
            onClick={onCancel}
            type="button"
          >
            Cancel
          </button>
          <button
            className="btn border-0 bg-rose-700 text-white hover:bg-rose-800"
            disabled={isSaving}
            onClick={onConfirm}
            type="button"
          >
            {isSaving ? 'Saving...' : confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
