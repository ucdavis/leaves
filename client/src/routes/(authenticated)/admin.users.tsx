import { useState } from 'react';
import { createFileRoute } from '@tanstack/react-router';
import type { ColumnDef } from '@tanstack/react-table';
import { HttpError } from '@/lib/api.ts';
import { AdminUserModal } from '@/shared/admin/AdminUserModal.tsx';
import type {
  AdminUser,
} from '@/shared/admin/adminData.tsx';
import {
  AdminDataProvider,
  useAdminData,
} from '@/shared/admin/adminData.tsx';
import { DataTable } from '@/shared/dataTable.tsx';

export const Route = createFileRoute('/(authenticated)/admin/users')({
  component: AdminUsersRoute,
});

type UserRow = AdminUser & {
  departmentName: string;
};

function AdminUsersRoute() {
  return (
    <AdminDataProvider>
      <AdminUsersRouteContent />
    </AdminDataProvider>
  );
}

function AdminUsersRouteContent() {
  const { departments, readonlyReason, updateUser, users } = useAdminData();
  const [filterRole, setFilterRole] = useState('');
  const [filterDepartmentId, setFilterDepartmentId] = useState('');
  const [showExcluded, setShowExcluded] = useState(false);
  const [editingUserId, setEditingUserId] = useState<string | null>(null);

  const departmentNames = Object.fromEntries(
    departments.map((department) => [department.id, department.name])
  );

  const rows: UserRow[] = users
    .filter((user) => (showExcluded ? true : user.active))
    .filter((user) => (filterRole ? user.role === filterRole : true))
    .filter((user) =>
      filterDepartmentId ? user.departmentId === filterDepartmentId : true
    )
    .map((user) => ({
      ...user,
      departmentName: departmentNames[user.departmentId] ?? 'Not mapped',
    }));

  const activeUsers = users.filter((user) => user.active);
  const excludedCount = users.length - activeUsers.length;
  const missingEmailCount = activeUsers.filter((user) => !user.email.trim()).length;

  const columns: ColumnDef<UserRow>[] = [
    {
      accessorKey: 'name',
      cell: ({ row }) => (
        <div>
          <div className="font-semibold text-[var(--admin-ink)]">
            {row.original.name}
          </div>
          <div className="text-xs text-[var(--admin-ink-muted)]">
            {row.original.role === 'admin'
              ? 'Application administrator'
              : 'App user'}
          </div>
        </div>
      ),
      header: 'Name',
    },
    {
      accessorKey: 'email',
      cell: ({ row }) =>
        row.original.email ? (
          <span>{row.original.email}</span>
        ) : (
          <span className="italic text-rose-700">Missing</span>
        ),
      header: 'Email',
    },
    {
      accessorKey: 'employeeId',
      cell: ({ row }) => (
        <span className="font-mono text-xs">{row.original.employeeId}</span>
      ),
      header: 'Emp ID',
    },
    {
      accessorKey: 'departmentName',
      header: 'Department',
    },
    {
      accessorKey: 'role',
      cell: ({ row }) => (
        <span className="inline-flex rounded-full bg-[var(--admin-sand)] px-3 py-1 text-xs font-semibold text-[var(--admin-blue)]">
          {row.original.role === 'admin' ? 'Admin' : 'Faculty'}
        </span>
      ),
      header: 'Role',
    },
    {
      accessorKey: 'active',
      cell: ({ row }) => (
        <UserStatusToggle
          active={row.original.active}
          onToggle={() =>
            updateUser(row.original.id, { active: !row.original.active })
          }
        />
      ),
      header: 'Status',
    },
    {
      cell: ({ row }) => (
        <button
          className="btn btn-ghost btn-sm"
          onClick={() => setEditingUserId(row.original.id)}
          type="button"
        >
          Edit
        </button>
      ),
      header: 'Actions',
      id: 'actions',
    },
  ];

  const editingUser =
    editingUserId === null
      ? null
      : users.find((user) => user.id === editingUserId) ?? null;

  return (
    <div className="space-y-6">
      <section className="grid gap-4 md:grid-cols-3">
        <SummaryCard
          label="Database roster"
          text={`${activeUsers.length} active people are currently loaded from AppUser.`}
          value={String(activeUsers.length)}
        />
        <SummaryCard
          accent="text-rose-700"
          label="Missing emails"
          text="Useful for checking directory and onboarding completeness."
          value={String(missingEmailCount)}
        />
        <SummaryCard
          accent="text-slate-700"
          label="Excluded users"
          text="Backed by the persisted AppUser.IsActive flag."
          value={String(excludedCount)}
        />
      </section>

      <section className="rounded-[1.25rem] border border-[var(--admin-border)] bg-white p-6 shadow-sm">
        <div className="mb-5 flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <h2 className="text-lg font-semibold text-[var(--admin-blue)]">
              User management
            </h2>
            <p className="mt-2 text-sm text-[var(--admin-ink-muted)]">
              This table is now sourced from the database. Department values are
              inferred from the user&apos;s latest leave request snapshot.
            </p>
            <p className="mt-2 text-sm text-[var(--admin-ink-muted)]">
              {readonlyReason}
            </p>
          </div>
        </div>

        <DataTable
          columns={columns}
          data={rows}
          filterPlaceholder="Search name, email, IAM ID, or department..."
          globalFilter="left"
          initialState={{
            pagination: {
              pageSize: 8,
            },
          }}
          tableActions={
            <div className="flex flex-col gap-3 sm:flex-row sm:flex-wrap sm:items-center">
              <select
                className="select select-bordered"
                onChange={(event) => setFilterRole(event.target.value)}
                value={filterRole}
              >
                <option value="">All roles</option>
                <option value="faculty">Faculty</option>
                <option value="admin">Admin</option>
              </select>

              <select
                className="select select-bordered"
                onChange={(event) => setFilterDepartmentId(event.target.value)}
                value={filterDepartmentId}
              >
                <option value="">All departments</option>
                {departments.map((department) => (
                  <option key={department.id} value={department.id}>
                    {department.name}
                  </option>
                ))}
              </select>

              <label className="label cursor-pointer gap-3 rounded-xl border border-[var(--admin-border)] px-4 py-2">
                <span className="label-text text-sm text-[var(--admin-ink)]">
                  Show excluded
                </span>
                <input
                  checked={showExcluded}
                  className="toggle toggle-sm"
                  onChange={(event) => setShowExcluded(event.target.checked)}
                  type="checkbox"
                />
              </label>
            </div>
          }
        />
      </section>

      {editingUser ? (
        <AdminUserModal
          departments={departments}
          initialValues={{
            departmentOverrideEndDate: editingUser.departmentOverrideEndDate,
            departmentOverrideId: editingUser.departmentOverrideId,
            departmentOverrideStartDate: editingUser.departmentOverrideStartDate,
            email: editingUser.email,
            name: editingUser.name,
          }}
          onClose={() => setEditingUserId(null)}
          onSubmit={async (value) => {
            await updateUser(editingUser.id, {
              departmentOverrideEndDate: value.departmentOverrideEndDate,
              departmentOverrideId: value.departmentOverrideId,
              departmentOverrideStartDate: value.departmentOverrideStartDate,
              email: value.email,
              name: value.name,
            });
            setEditingUserId(null);
          }}
          submitErrorMessage={getUserUpdateErrorMessage}
          submitLabel="Save changes"
          submittingLabel="Saving..."
          title={`Edit ${editingUser.name}`}
        />
      ) : null}
    </div>
  );
}

function SummaryCard({
  accent,
  label,
  text,
  value,
}: {
  accent?: string;
  label: string;
  text: string;
  value: string;
}) {
  return (
    <section className="rounded-[1.25rem] border border-[var(--admin-border)] bg-white p-5 shadow-sm">
      <div className="text-xs font-semibold uppercase tracking-[0.24em] text-[var(--admin-gold-deep)]">
        {label}
      </div>
      <div className={`mt-3 text-3xl font-bold ${accent ?? 'text-[var(--admin-blue)]'}`}>
        {value}
      </div>
      <p className="mt-2 text-sm text-[var(--admin-ink-muted)]">{text}</p>
    </section>
  );
}

function UserStatusToggle({
  active,
  onToggle,
}: {
  active: boolean;
  onToggle: () => Promise<void>;
}) {
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const handleToggle = async () => {
    setIsSaving(true);
    setError(null);

    try {
      await onToggle();
    } catch (toggleError) {
      setError(getUserUpdateErrorMessage(toggleError));
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="flex flex-col items-start gap-2">
      <button
        className={`badge border-0 px-3 py-3 text-xs font-semibold ${
          active ? 'bg-emerald-100 text-emerald-800' : 'bg-slate-200 text-slate-700'
        }`}
        disabled={isSaving}
        onClick={() => {
          void handleToggle();
        }}
        type="button"
      >
        {isSaving ? 'Saving...' : active ? 'Included' : 'Excluded'}
      </button>
      {error ? <span className="text-xs text-rose-700">{error}</span> : null}
    </div>
  );
}


function getUserUpdateErrorMessage(error: unknown) {
  return getUserMutationErrorMessage(
    error,
    'Unable to save the user. Please review the fields and try again.'
  );
}

function getUserMutationErrorMessage(error: unknown, fallbackMessage: string) {
  if (error instanceof HttpError) {
    if (typeof error.body === 'string' && error.body.trim()) {
      return error.body;
    }

    if (error.body && typeof error.body === 'object') {
      const body = error.body as {
        detail?: string;
        errors?: Record<string, string[]>;
        title?: string;
      };

      const validationMessage = body.errors
        ? Object.values(body.errors)
            .flat()
            .find(Boolean)
        : null;

      if (validationMessage) {
        return validationMessage;
      }

      if (body.detail) {
        return body.detail;
      }

      if (body.title) {
        return body.title;
      }
    }

    if (error.status === 409) {
      return 'That user update conflicts with an existing record.';
    }

    return fallbackMessage;
  }

  if (error instanceof Error && error.message) {
    return error.message;
  }

  return fallbackMessage;
}
