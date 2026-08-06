import { useState } from 'react';
import { createFileRoute } from '@tanstack/react-router';
import type { ColumnDef } from '@tanstack/react-table';
import { HttpError } from '@/lib/api.ts';
import { adminFacultyQueryOptions } from '@/queries/adminFaculty.ts';
import { AdminUserModal } from '@/shared/admin/AdminUserModal.tsx';
import type { AdminUser } from '@/shared/admin/adminData.tsx';
import {
  AdminFacultyDataProvider,
  useAdminFacultyData,
} from '@/shared/admin/adminFacultyData.tsx';
import { DataTable } from '@/shared/dataTable.tsx';
import { statusTextColors } from '@/shared/statusColors.ts';

export const Route = createFileRoute('/(authenticated)/admin/faculty')({
  component: AdminPeopleRoute,
  loader: ({ context }) =>
    context.queryClient.ensureQueryData(adminFacultyQueryOptions()),

  pendingComponent: () => (
    <section className="rounded-[1.25rem] border border-[var(--admin-border)] bg-white p-6 shadow-sm">
      <h2 className="text-lg font-semibold text-[var(--admin-blue)]">
        Loading faculty data
      </h2>
      <p className="mt-2 text-sm text-[var(--admin-ink-muted)]">
        Pulling the current faculty records from the database.
      </p>
    </section>
  ),
});

type UserRow = AdminUser & {
  departmentName: string;
};

function AdminPeopleRoute() {
  return (
    <AdminFacultyDataProvider>
      <AdminPeopleRouteContent />
    </AdminFacultyDataProvider>
  );
}

function AdminPeopleRouteContent() {
  const { departments, facultyUsers, updateUser } = useAdminFacultyData();
  const [filterDepartmentId, setFilterDepartmentId] = useState('');
  const [editingUserId, setEditingUserId] = useState<string | null>(null);
  const [pendingExcludeChange, setPendingExcludeChange] = useState<{
    nextActive: boolean;
    userId: string;
  } | null>(null);
  const [showExcluded, setShowExcluded] = useState(false);

  const departmentNames = Object.fromEntries(
    departments.map((department) => [department.id, department.name])
  );

  const rows: UserRow[] = facultyUsers
    .filter((user) => {
      const effectiveActive =
        pendingExcludeChange?.userId === user.id
          ? pendingExcludeChange.nextActive
          : user.active;
      const isPendingExcluded =
        pendingExcludeChange?.userId === user.id && effectiveActive === false;

      return showExcluded ? true : effectiveActive || isPendingExcluded;
    })
    .filter((user) =>
      filterDepartmentId ? user.departmentId === filterDepartmentId : true
    )
    .map((user) => ({
      ...user,
      departmentName: departmentNames[user.departmentId] ?? 'Not mapped',
    }));

  const columns: ColumnDef<UserRow>[] = [
    {
      accessorKey: 'name',
      cell: ({ row }) => (
        <div>
          <div className="font-semibold text-base-content">
            {row.original.name}
          </div>
          <div className="text-xs text-base-content/70">
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
          <span className={`italic ${statusTextColors.danger}`}>Missing</span>
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
        <span className="inline-flex rounded-full bg-base-200 px-3 py-1 text-xs font-semibold text-primary">
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
    {
      cell: ({ row }) => {
        const isSaving = pendingExcludeChange?.userId === row.original.id;
        const effectiveActive =
          pendingExcludeChange?.userId === row.original.id
            ? pendingExcludeChange.nextActive
            : row.original.active;
        const inputId = `exclude-${row.original.id}`;

        return (
          <div className="flex w-full justify-center">
            <label
              className={isSaving ? 'cursor-progress' : 'cursor-pointer'}
              htmlFor={inputId}
              onClick={(event) => event.stopPropagation()}
            >
              <input
                aria-label={`Exclude ${row.original.name}`}
                checked={!effectiveActive}
                className="checkbox checkbox-sm"
                disabled={pendingExcludeChange !== null}
                id={inputId}
                onChange={async (event) => {
                  if (pendingExcludeChange !== null) {
                    return;
                  }

                  const nextActive = !event.target.checked;
                  setPendingExcludeChange({
                    nextActive,
                    userId: row.original.id,
                  });

                  try {
                    await updateUser(row.original.id, {
                      active: nextActive,
                    });
                  } finally {
                    setPendingExcludeChange((currentChange) =>
                      currentChange?.userId === row.original.id
                        ? null
                        : currentChange
                    );
                  }
                }}
                onClick={(event) => event.stopPropagation()}
                type="checkbox"
              />
            </label>
          </div>
        );
      },
      header: () => <div className="text-center">Exclude</div>,
      id: 'exclude',
    },
  ];

  const editingUser =
    editingUserId === null
      ? null
      : (facultyUsers.find((user) => user.id === editingUserId) ?? null);

  return (
    <div className="space-y-6">
      <section className="card border border-main-border bg-base-100">
        <div className="card-body p-6">
          <div className="mb-5 flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
            <div className="space-y-2 max-w-3xl">
              <h2 className="text-lg font-semibold text-primary">
                Faculty management
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
              <div className="flex flex-col gap-3 sm:flex-row sm:flex-nowrap sm:items-center">
                <select
                  className="select select-bordered w-full sm:w-64"
                  onChange={(event) =>
                    setFilterDepartmentId(event.target.value)
                  }
                  value={filterDepartmentId}
                >
                  <option value="">All departments</option>
                  {departments.map((department) => (
                    <option key={department.id} value={department.id}>
                      {department.name}
                    </option>
                  ))}
                </select>

                <label className="label w-full cursor-pointer gap-3 rounded-xl border border-base-300 px-4 py-2 sm:w-auto sm:flex-none">
                  <span className="label-text text-sm text-base-content">
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
        </div>
      </section>

      {editingUser ? (
        <AdminUserModal
          departments={departments}
          initialValues={{
            departmentOverrideEndDate: editingUser.departmentOverrideEndDate,
            departmentOverrideId: editingUser.departmentOverrideId,
            departmentOverrideStartDate:
              editingUser.departmentOverrideStartDate,
          }}
          onClose={() => setEditingUserId(null)}
          onSubmit={async (value) => {
            await updateUser(editingUser.id, {
              departmentOverrideEndDate: value.departmentOverrideEndDate,
              departmentOverrideId: value.departmentOverrideId,
              departmentOverrideStartDate: value.departmentOverrideStartDate,
            });
            setEditingUserId(null);
          }}
          submitErrorMessage={getUserUpdateErrorMessage}
          submitLabel="Save changes"
          submittingLabel="Saving..."
          title={`Change ${editingUser.name}'s Department`}
        />
      ) : null}
    </div>
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
        ? Object.values(body.errors).flat().find(Boolean)
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
