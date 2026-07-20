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

export const Route = createFileRoute('/(authenticated)/admin/people')({
  component: AdminPeopleRoute,
});

type UserRow = AdminUser & {
  departmentName: string;
};

function AdminPeopleRoute() {
  return (
    <AdminDataProvider>
      <AdminPeopleRouteContent />
    </AdminDataProvider>
  );
}

function AdminPeopleRouteContent() {
  const { departments, updateUser, users } = useAdminData();
  const [filterRole, setFilterRole] = useState('');
  const [filterDepartmentId, setFilterDepartmentId] = useState('');
  const [editingUserId, setEditingUserId] = useState<string | null>(null);

  const departmentNames = Object.fromEntries(
    departments.map((department) => [department.id, department.name])
  );

  const rows: UserRow[] = users
    .filter((user) => (filterRole ? user.role === filterRole : true))
    .filter((user) =>
      filterDepartmentId ? user.departmentId === filterDepartmentId : true
    )
    .map((user) => ({
      ...user,
      departmentName: departmentNames[user.departmentId] ?? 'Not mapped',
    }));

  const missingEmailCount = users.filter((user) => !user.email.trim()).length;

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
              : 'Person'}
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
          {getRoleLabel(row.original.role)}
        </span>
      ),
      header: 'Role',
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
          label="People roster"
          text={`${users.length} people are currently loaded from People.`}
          value={String(users.length)}
        />
        <SummaryCard
          accent="text-rose-700"
          label="Missing emails"
          text="Useful for checking directory and onboarding completeness."
          value={String(missingEmailCount)}
        />
        <SummaryCard
          accent="text-slate-700"
          label="Departments"
          text="Mapped from reporting data and department overrides."
          value={String(departments.length)}
        />
      </section>

      <section className="rounded-[1.25rem] border border-[var(--admin-border)] bg-white p-6 shadow-sm">
        <div className="mb-5 flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <h2 className="text-lg font-semibold text-[var(--admin-blue)]">
              People management
            </h2>
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
                <option value="chair">Chair</option>
                <option value="cao">CAO</option>
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

function getRoleLabel(role: AdminUser['role']) {
  if (role === 'admin') {
    return 'Admin';
  }

  if (role === 'chair') {
    return 'Chair';
  }

  if (role === 'cao') {
    return 'CAO';
  }

  return 'Faculty';
}

function getUserUpdateErrorMessage(error: unknown) {
  return getUserMutationErrorMessage(
    error,
    'Unable to save the person. Please review the fields and try again.'
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
      return 'That person update conflicts with an existing record.';
    }

    return fallbackMessage;
  }

  if (error instanceof Error && error.message) {
    return error.message;
  }

  return fallbackMessage;
}
