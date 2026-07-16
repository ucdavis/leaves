import type { ReactNode } from 'react';

export function AdminModalFrame({
  children,
  description,
  maxWidthClassName,
  title,
}: {
  children: ReactNode;
  description?: string;
  maxWidthClassName?: string;
  title: string;
}) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/40 px-4 py-8">
      <div
        className={`max-h-[90vh] w-full overflow-y-auto rounded-[1.5rem] border border-[var(--admin-border)] bg-white p-6 shadow-2xl ${
          maxWidthClassName ?? 'max-w-2xl'
        }`}
      >
        <div className="mb-6">
          <h2 className="text-xl font-semibold text-[var(--admin-blue)]">
            {title}
          </h2>
          {description ? (
            <p className="mt-2 text-sm text-[var(--admin-ink-muted)]">
              {description}
            </p>
          ) : null}
        </div>
        {children}
      </div>
    </div>
  );
}
