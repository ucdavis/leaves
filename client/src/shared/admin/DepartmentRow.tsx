import { Cog6ToothIcon, UserGroupIcon } from '@heroicons/react/24/outline';
import type { AdminDepartment } from '@/shared/admin/adminData.tsx';
import { InlineTextEditor } from './InlineTextEditor.tsx';

export function DepartmentRow({
  department,
  linkedUserCount,
  onOpenRoster,
  onOpenSettings,
  onRename,
}: {
  department: AdminDepartment;
  linkedUserCount: number;
  onOpenRoster: () => void;
  onOpenSettings: () => void;
  onRename: (name: string) => Promise<void>;
}) {
  const approvalLabel =
    department.approvalMode === 'approval'
      ? 'Approval required'
      : department.approvalMode === 'auto'
        ? 'Auto-approve'
        : 'Notification only';

  return (
    <div className="card border border-main-border bg-base-100">
      <div className="card-body flex flex-col gap-4 px-5 py-4 lg:flex-row lg:items-start lg:justify-between">
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
            <InlineTextEditor
              initialValue={department.name}
              inputClassName="input input-ghost h-auto min-h-0 w-full max-w-md justify-start px-0 text-lg font-bold uppercase tracking-wide text-primary shadow-none focus:bg-transparent"
              key={`${department.id}:${department.name}`}
              onSave={onRename}
              requiredMessage="Department name is required."
              savingMessage="Saving department name..."
              wrapperClassName="w-full max-w-md"
            />
          </div>
          <div className="mt-1 font-mono text-sm text-base-content/70">
            {department.code}
          </div>
          <div className="mt-2 text-sm text-base-content/70">
            {linkedUserCount} active users · {approvalLabel}
            {department.routingEmails.length > 0
              ? ` · ${department.routingEmails.length} routing emails`
              : ' · No email configured'}
          </div>
        </div>

        <div className="flex flex-col gap-3 items-end">
          <div className="text-xs font-semibold uppercase text-base-content/50">
            Database-backed settings
          </div>
          <div className="flex w-full flex-col gap-2 sm:w-auto sm:flex-row sm:flex-nowrap">
            <button
              className="btn btn-primary"
              onClick={onOpenRoster}
              type="button"
            >
              <UserGroupIcon aria-hidden="true" className="h-5 w-5 shrink-0" />
              Linked users
            </button>
            <button
              className="btn btn-outline"
              onClick={onOpenSettings}
              type="button"
            >
              <Cog6ToothIcon aria-hidden="true" className="h-5 w-5 shrink-0" />
              Settings
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
