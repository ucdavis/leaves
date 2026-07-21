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
        className={`max-h-[90vh] w-full overflow-y-auto rounded-[1.5rem] border border-base-300 bg-base-100 p-6 shadow-2xl ${
          maxWidthClassName ?? 'max-w-2xl'
        }`}
      >
        <div className="mb-6">
          <h2 className="text-xl font-semibold text-primary">
            {title}
          </h2>
          {description ? (
            <p className="mt-2 text-sm text-base-content/70">
              {description}
            </p>
          ) : null}
        </div>
        {children}
      </div>
    </div>
  );
}
