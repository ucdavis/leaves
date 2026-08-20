import {
  Cog6ToothIcon,
  PencilSquareIcon,
  UserGroupIcon,
} from '@heroicons/react/24/outline';
import type { AdminDepartment } from '@/shared/admin/adminData.ts';

export function DepartmentRow({
  chairName,
  department,
  linkedUserCount,
  onOpenRoster,
  onOpenSettings,
}: {
  chairName: string | null;
  department: AdminDepartment;
  linkedUserCount: number;
  onOpenRoster: () => void;
  onOpenSettings: () => void;
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
            <div className="w-full max-w-md text-lg font-bold uppercase tracking-wide text-primary">
              {department.name}
            </div>
          </div>
          <div className="mt-1 font-mono text-sm text-base-content/70">
            {department.code}
          </div>
          <div className="mt-2 flex flex-wrap items-center gap-x-2 gap-y-1 text-sm text-base-content/70">
            <span>{linkedUserCount} active users</span>
            <span>·</span>
            <span>{approvalLabel}</span>
            <span>·</span>
            <span>
              {department.routingEmails.length > 0
                ? `${department.routingEmails.length} routing emails`
                : 'No email configured'}
            </span>
            <span>·</span>
            <span>{chairName ? `Chair: ${chairName}` : 'Add chair'}</span>
            <button
              className="inline-flex items-center text-[var(--admin-blue)] hover:text-[var(--admin-gold-deep)]"
              onClick={onOpenRoster}
              type="button"
            >
              <PencilSquareIcon
                aria-hidden="true"
                aria-label="Edit ${department.name} chair"
                className="h-4 w-4 shrink-0"
              />
            </button>
          </div>
        </div>

        <div className="flex flex-col gap-3 items-end">
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
