import { useState } from 'react';
import {
  useMutation,
  useQueryClient,
  useSuspenseQuery,
} from '@tanstack/react-query';
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
import { WarningModal } from '@/shared/WarningModal.tsx';

export const Route = createFileRoute('/(authenticated)/admin/roles')({
  component: AdminRolesRoute,
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
});

const roleLabels: Record<AdminAssignableRoleType, string> = {
  admin: 'Admin',
  cao: 'CAO',
  chair: 'Department chair',
};

type PendingRoleAction =
  | {
      kind: 'add';
      roleType: AdminAssignableRoleType;
      targetName: string | null;
      userName: string;
    }
  | {
      assignment: AdminRoleAssignment;
      kind: 'remove';
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
  const [showInactiveAssignments, setShowInactiveAssignments] = useState(false);
  const [pendingAction, setPendingAction] = useState<PendingRoleAction | null>(
    null
  );

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
    addChairMutation.isPending ||
    removeMutation.isPending;

  const targetOptions = type === 'cao' ? data.clusters : data.departments;
  const selectedTargetName =
    type === 'admin'
      ? null
      : (targetOptions.find((option) => option.id === targetId)?.name ?? null);
  const selectedUserName =
    data.users.find((user) => user.iamId === iamId)?.name ?? iamId;
  const assignmentRows = data.assignments.filter((assignment) =>
    showInactiveAssignments ? true : assignment.active
  );

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
      return true;
    } catch (addError) {
      setError(getAdminMutationErrorMessage(addError));
      return false;
    }
  };

  const handleConfirmAction = async () => {
    if (!pendingAction) {
      return;
    }

    if (pendingAction.kind === 'add') {
      const succeeded = await handleAdd();
      if (succeeded) {
        setPendingAction(null);
      }

      return;
    } else {
      setError(null);

      try {
        await removeMutation.mutateAsync({
          id: pendingAction.assignment.id,
          type: pendingAction.assignment.type,
        });
      } catch (removeError) {
        setError(getAdminMutationErrorMessage(removeError));
        return;
      }
    }

    setPendingAction(null);
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
          className={`btn btn-ghost btn-sm ${
            row.original.active
              ? 'text-rose-700'
              : 'cursor-not-allowed text-slate-400'
          }`}
          disabled={removeMutation.isPending || !row.original.active}
          onClick={() =>
            setPendingAction({
              assignment: row.original,
              kind: 'remove',
            })
          }
          type="button"
        >
          {row.original.type === 'admin' ? 'Remove' : 'Close out assignment'}
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
          Assign CAO, department chair, or application admin roles
        </h2>

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
                  onChange={(event) =>
                    setEffectiveStartDate(event.target.value)
                  }
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
            onClick={() =>
              setPendingAction({
                kind: 'add',
                roleType: type,
                targetName: selectedTargetName,
                userName: selectedUserName,
              })
            }
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
          data={assignmentRows}
          filterPlaceholder="Search role, person, IAM ID, or scope..."
          globalFilter="left"
          initialState={{
            pagination: {
              pageSize: 10,
            },
          }}
          tableActions={
            <label className="label cursor-pointer gap-3 rounded-xl border border-[var(--admin-border)] px-4 py-2">
              <span className="label-text text-sm text-[var(--admin-ink)]">
                Show inactive
              </span>
              <input
                checked={showInactiveAssignments}
                className="toggle toggle-sm"
                onChange={(event) =>
                  setShowInactiveAssignments(event.target.checked)
                }
                type="checkbox"
              />
            </label>
          }
        />
      </section>

      {pendingAction ? (
        <RoleWarningModal
          action={pendingAction}
          isSaving={isSaving}
          onCancel={() => setPendingAction(null)}
          onConfirm={() => {
            void handleConfirmAction();
          }}
        />
      ) : null}
    </div>
  );
}

function getRoleWarningModalText(action: PendingRoleAction) {
  const isAdd = action.kind === 'add';
  const roleType = isAdd ? action.roleType : action.assignment.type;
  const title = isAdd
    ? `Add ${roleLabels[roleType]} assignment?`
    : roleType === 'admin'
      ? 'Remove admin assignment?'
      : `Close out ${roleLabels[roleType]} assignment?`;
  const confirmLabel = isAdd
    ? 'Add role'
    : roleType === 'admin'
      ? 'Remove'
      : 'Close out assignment';
  const personName = isAdd ? action.userName : action.assignment.name;
  const scopeName = isAdd
    ? (action.targetName ?? 'Application-wide')
    : (action.assignment.targetName ?? 'Application-wide');
  const message = isAdd ? (
    <span>
      This will grant {roleLabels[roleType]} access to {personName}
      {scopeName === 'Application-wide' ? '' : ` for ${scopeName}`}.
    </span>
  ) : roleType === 'admin' ? (
    <span>This will remove application admin access for {personName}.</span>
  ) : (
    <span>
      This will close the active {roleLabels[roleType]} assignment for{' '}
      {personName} in {scopeName}. The database will record the closing admin
      and closing timestamp.
    </span>
  );

  return {
    confirmLabel,
    message,
    title,
  };
}

function RoleWarningModal({
  action,
  isSaving,
  onCancel,
  onConfirm,
}: {
  action: PendingRoleAction;
  isSaving: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  const { confirmLabel, message, title } = getRoleWarningModalText(action);

  return (
    <WarningModal
      confirmLabel={confirmLabel}
      description="Please confirm this role assignment change before it is saved."
      isSaving={isSaving}
      onCancel={onCancel}
      onConfirm={onConfirm}
      title={title}
    >
      {message}
    </WarningModal>
  );
}
