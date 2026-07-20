import { useState } from 'react';
import { useMutation, useQueryClient, useSuspenseQuery } from '@tanstack/react-query';
import { createFileRoute } from '@tanstack/react-router';
import type { ColumnDef } from '@tanstack/react-table';
import {
  addAdminAssignment,
  addCaoAssignment,
  addChairAssignment,
  adminRolesQueryOptions,
  removeRoleAssignment,
  type AdminAssignableRoleType,
  type AdminRoleAssignment,
} from '@/queries/adminRoles.ts';
import { getAdminMutationErrorMessage } from '@/shared/admin/adminErrors.ts';
import { DataTable } from '@/shared/dataTable.tsx';

export const Route = createFileRoute('/(authenticated)/admin/roles')({
  loader: ({ context }) =>
    context.queryClient.ensureQueryData(adminRolesQueryOptions()),
  pendingComponent: () => (
    <section className="rounded-[1.25rem] border border-[var(--admin-border)] bg-white p-6 shadow-sm">
      <h2 className="text-lg font-semibold text-[var(--admin-blue)]">
        Loading role assignments
      </h2>
      <p className="mt-2 text-sm text-[var(--admin-ink-muted)]">
        Pulling admins, CAOs, and department chairs from the database.
      </p>
    </section>
  ),
  component: AdminRolesRoute,
});

const roleLabels: Record<AdminAssignableRoleType, string> = {
  admin: 'Admin',
  cao: 'CAO',
  chair: 'Department chair',
};

function getToday() {
  return new Date().toISOString().slice(0, 10);
}

function AdminRolesRoute() {
  const queryClient = useQueryClient();
  const { data } = useSuspenseQuery(adminRolesQueryOptions());
  const [type, setType] = useState<AdminAssignableRoleType>('admin');
  const [iamId, setIamId] = useState('');
  const [targetId, setTargetId] = useState('');
  const [effectiveStartDate, setEffectiveStartDate] = useState(getToday());
  const [effectiveEndDate, setEffectiveEndDate] = useState('');
  const [error, setError] = useState<string | null>(null);

  const invalidateRoles = async () => {
    await queryClient.invalidateQueries({ queryKey: ['admin', 'roles'] });
  };

  const addAdminMutation = useMutation({
    mutationFn: addAdminAssignment,
    onSuccess: invalidateRoles,
  });
  const addCaoMutation = useMutation({
    mutationFn: addCaoAssignment,
    onSuccess: invalidateRoles,
  });
  const addChairMutation = useMutation({
    mutationFn: addChairAssignment,
    onSuccess: invalidateRoles,
  });
  const removeMutation = useMutation({
    mutationFn: removeRoleAssignment,
    onSuccess: invalidateRoles,
  });

  const isSaving =
    addAdminMutation.isPending ||
    addCaoMutation.isPending ||
    addChairMutation.isPending;

  const targetOptions = type === 'cao' ? data.clusters : data.departments;
  const selectedUser = data.users.find((user) => user.iamId === iamId);

  const resetForm = () => {
    setIamId('');
    setTargetId('');
    setEffectiveStartDate(getToday());
    setEffectiveEndDate('');
  };

  const handleAdd = async () => {
    setError(null);

    try {
      if (type === 'admin') {
        await addAdminMutation.mutateAsync({ iamId });
      } else if (type === 'cao') {
        await addCaoMutation.mutateAsync({
          clusterId: targetId,
          effectiveEndDate,
          effectiveStartDate,
          iamId,
        });
      } else {
        await addChairMutation.mutateAsync({
          departmentCode: targetId,
          effectiveEndDate,
          effectiveStartDate,
          iamId,
        });
      }

      resetForm();
    } catch (addError) {
      setError(getAdminMutationErrorMessage(addError));
    }
  };

  const columns: ColumnDef<AdminRoleAssignment>[] = [
    {
      accessorKey: 'type',
      cell: ({ row }) => roleLabels[row.original.type],
      header: 'Role',
    },
    {
      accessorKey: 'name',
      cell: ({ row }) => (
        <div>
          <div className="font-semibold text-[var(--admin-ink)]">
            {row.original.name}
          </div>
          <div className="text-xs text-[var(--admin-ink-muted)]">
            {row.original.email || row.original.iamId}
          </div>
        </div>
      ),
      header: 'Person',
    },
    {
      accessorKey: 'targetName',
      cell: ({ row }) => row.original.targetName ?? 'Application-wide',
      header: 'Scope',
    },
    {
      accessorKey: 'effectiveStartDate',
      cell: ({ row }) => row.original.effectiveStartDate ?? 'Immediate',
      header: 'Start',
    },
    {
      accessorKey: 'effectiveEndDate',
      cell: ({ row }) => row.original.effectiveEndDate ?? 'Open',
      header: 'End',
    },
    {
      accessorKey: 'active',
      cell: ({ row }) => (
        <span
          className={`badge border-0 px-3 py-3 text-xs font-semibold ${
            row.original.active
              ? 'bg-emerald-100 text-emerald-800'
              : 'bg-slate-200 text-slate-700'
          }`}
        >
          {row.original.active ? 'Active' : 'Inactive'}
        </span>
      ),
      header: 'Status',
    },
    {
      cell: ({ row }) => (
        <button
          className="btn btn-ghost btn-sm text-rose-700"
          disabled={removeMutation.isPending || !row.original.active}
          onClick={() => {
            setError(null);
            removeMutation
              .mutateAsync({
                id: row.original.id,
                type: row.original.type,
              })
              .catch((removeError: unknown) => {
                setError(getAdminMutationErrorMessage(removeError));
              });
          }}
          type="button"
        >
          Remove
        </button>
      ),
      header: 'Actions',
      id: 'actions',
    },
  ];

  return (
    <div className="space-y-6">
      <section className="rounded-[1.25rem] border border-[var(--admin-border)] bg-white p-6 shadow-sm">
        <h2 className="text-lg font-semibold text-[var(--admin-blue)]">
          Role assignments
        </h2>
        <p className="mt-2 max-w-3xl text-sm leading-6 text-[var(--admin-ink-muted)]">
          Manage application admins, cluster CAOs, and department chairs from
          their assignment tables.
        </p>

        <div className="mt-5 grid gap-4 lg:grid-cols-[1fr_1.4fr_1fr_1fr_1fr_auto] lg:items-end">
          <label className="form-control">
            <span className="label-text font-medium">Role</span>
            <select
              className="select select-bordered mt-2 w-full"
              onChange={(event) => {
                setType(event.target.value as AdminAssignableRoleType);
                setTargetId('');
              }}
              value={type}
            >
              <option value="admin">Admin</option>
              <option value="cao">CAO</option>
              <option value="chair">Department chair</option>
            </select>
          </label>

          <label className="form-control">
            <span className="label-text font-medium">Person</span>
            <select
              className="select select-bordered mt-2 w-full"
              onChange={(event) => setIamId(event.target.value)}
              value={iamId}
            >
              <option value="">Select a person</option>
              {data.users.map((user) => (
                <option key={user.iamId} value={user.iamId}>
                  {user.name} ({user.iamId})
                </option>
              ))}
            </select>
            {selectedUser?.email ? (
              <span className="label-text-alt mt-1 text-[var(--admin-ink-muted)]">
                {selectedUser.email}
              </span>
            ) : null}
          </label>

          {type !== 'admin' ? (
            <label className="form-control">
              <span className="label-text font-medium">
                {type === 'cao' ? 'Cluster' : 'Department'}
              </span>
              <select
                className="select select-bordered mt-2 w-full"
                onChange={(event) => setTargetId(event.target.value)}
                value={targetId}
              >
                <option value="">Select scope</option>
                {targetOptions.map((option) => (
                  <option key={option.id} value={option.id}>
                    {option.name}
                  </option>
                ))}
              </select>
            </label>
          ) : (
            <div className="hidden lg:block"></div>
          )}

          {type !== 'admin' ? (
            <>
              <label className="form-control">
                <span className="label-text font-medium">Start date</span>
                <input
                  className="input input-bordered mt-2 w-full"
                  onChange={(event) => setEffectiveStartDate(event.target.value)}
                  type="date"
                  value={effectiveStartDate}
                />
              </label>
              <label className="form-control">
                <span className="label-text font-medium">End date</span>
                <input
                  className="input input-bordered mt-2 w-full"
                  onChange={(event) => setEffectiveEndDate(event.target.value)}
                  type="date"
                  value={effectiveEndDate}
                />
              </label>
            </>
          ) : (
            <>
              <div className="hidden lg:block"></div>
              <div className="hidden lg:block"></div>
            </>
          )}

          <button
            className="btn border-0 bg-[var(--admin-gold)] text-[var(--admin-blue)] hover:bg-[var(--admin-gold)]/85"
            disabled={
              isSaving ||
              !iamId ||
              (type !== 'admin' && (!targetId || !effectiveStartDate))
            }
            onClick={() => {
              void handleAdd();
            }}
            type="button"
          >
            {isSaving ? 'Adding...' : 'Add role'}
          </button>
        </div>

        {error ? (
          <div className="mt-4 rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">
            {error}
          </div>
        ) : null}
      </section>

      <section className="rounded-[1.25rem] border border-[var(--admin-border)] bg-white p-6 shadow-sm">
        <DataTable
          columns={columns}
          data={data.assignments}
          filterPlaceholder="Search role, person, IAM ID, or scope..."
          globalFilter="left"
          initialState={{
            pagination: {
              pageSize: 10,
            },
          }}
        />
      </section>
    </div>
  );
}
