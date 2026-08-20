import { XMarkIcon } from '@heroicons/react/24/outline';
import { type ReactNode } from 'react';

export function Modal({
  children,
  onClose,
  title,
}: {
  children: ReactNode;
  onClose: () => void;
  title: string;
}) {
  return (
    <div
      aria-modal="true"
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 px-4 py-6"
      role="dialog"
    >
      <div className="w-full max-w-2xl overflow-hidden rounded-2xl bg-base-100 shadow-2xl">
        <div className="flex items-center justify-between border-b border-base-300 px-6 py-4">
          <h2 className="text-lg font-bold text-primary">{title}</h2>
          <button
            aria-label="Close"
            className="btn btn-ghost btn-sm btn-circle"
            onClick={onClose}
            type="button"
          >
            <XMarkIcon className="h-5 w-5" />
          </button>
        </div>
        <div className="max-h-[85vh] overflow-y-auto p-6">{children}</div>
      </div>
      <button
        aria-label="Close modal"
        className="absolute inset-0 -z-10 cursor-default"
        onClick={onClose}
        type="button"
      />
    </div>
  );
}
