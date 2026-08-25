import { useId, useMemo, useState } from 'react';
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

export const Route = createFileRoute('/(authenticated)/admin/manage-users')({
  component: AdminUsersRoute,
  loader: ({ context }) =>
    context.queryClient.ensureQueryData(adminRolesQueryOptions()),
  pendingComponent: () => (
    <section className="card border border-main-border bg-base-100">
      <div className="card-body p-6">
        <h2 className="text-lg font-semibold text-primary">
          Loading role assignments
        </h2>
        <p className="mt-2 text-sm text-base-content/70">
          Pulling admins, CAOs, and department chairs from the database.
        </p>
      </div>
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

export type AdminRolePersonOption = {
  departmentId: string | null;
  departmentName: string | null;
  departmentOptions: Array<{
    active: boolean;
    id: string;
    name: string;
  }>;
  email: string;
  iamId: string;
  name: string;
};

function AdminUsersRoute() {
  const queryClient = useQueryClient();
  const { data } = useSuspenseQuery(adminRolesQueryOptions());
  const [type, setType] = useState<AdminAssignableRoleType>('admin');
  const [iamId, setIamId] = useState('');
  const [personQuery, setPersonQuery] = useState('');
  const [isPersonSearchOpen, setIsPersonSearchOpen] = useState(false);
  const [targetId, setTargetId] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [showInactiveAssignments, setShowInactiveAssignments] = useState(false);
  const [pendingAction, setPendingAction] = useState<PendingRoleAction | null>(
    null
  );

  const refreshRoles = async () => {
    await queryClient.invalidateQueries({ queryKey: ['admin', 'roles'] });
  };

  const addAdminMutation = useMutation({
    mutationFn: addAdminAssignment,
    onSuccess: refreshRoles,
  });
  const addCaoMutation = useMutation({
    mutationFn: addCaoAssignment,
    onSuccess: refreshRoles,
  });
  const addChairMutation = useMutation({
    mutationFn: addChairAssignment,
    onSuccess: refreshRoles,
  });
  const removeMutation = useMutation({
    mutationFn: removeRoleAssignment,
    onSuccess: refreshRoles,
  });

  const isSaving =
    addAdminMutation.isPending ||
    addCaoMutation.isPending ||
    addChairMutation.isPending ||
    removeMutation.isPending;

  const selectedUser = data.users.find((user) => user.iamId === iamId) ?? null;
  const chairDepartmentOptions = selectedUser?.departmentOptions ?? [];
  const selectedChairDepartment =
    type === 'chair' && targetId
      ? (chairDepartmentOptions.find(
          (department) => department.id === targetId
        ) ?? null)
      : null;
  const targetOptions = type === 'cao' ? data.clusters : data.departments;
  const selectedTarget =
    type === 'admin'
      ? null
      : type === 'chair'
        ? selectedChairDepartment
        : (targetOptions.find((option) => option.id === targetId) ?? null);
  const selectedTargetName =
    type === 'admin'
      ? null
      : type === 'chair'
        ? (selectedChairDepartment?.name ?? null)
        : (selectedTarget?.name ?? null);
  const selectedUserName = selectedUser?.name ?? iamId;
  const assignmentRows = data.assignments.filter((assignment) =>
    showInactiveAssignments ? true : assignment.active
  );
  const normalizedPersonQuery = personQuery.trim().toLowerCase();
  const filteredUsers = useMemo(
    () =>
      data.users.filter((user) => {
        if (!normalizedPersonQuery) {
          return true;
        }

        const searchableText = [
          user.name,
          user.email,
          user.iamId,
          user.departmentName ?? '',
        ]
          .join(' ')
          .toLowerCase();

        return searchableText.includes(normalizedPersonQuery);
      }),
    [data.users, normalizedPersonQuery]
  );

  const resetForm = () => {
    setIamId('');
    setPersonQuery('');
    setIsPersonSearchOpen(false);
    setTargetId('');
  };

  const handleAdd = async () => {
    setError(null);

    if (type !== 'admin' && selectedTarget && !selectedTarget.active) {
      setError('Selected scope is inactive.');
      return false;
    }

    try {
      if (type === 'admin') {
        await addAdminMutation.mutateAsync({ iamId });
      } else if (type === 'cao') {
        await addCaoMutation.mutateAsync({
          clusterId: targetId,
          iamId,
        });
      } else {
        await addChairMutation.mutateAsync({
          departmentCode: targetId,
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
          <div className="font-semibold text-base-content">
            {row.original.name}
          </div>
          <div className="text-xs text-base-content/70">
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
            row.original.active ? 'badge-success' : 'badge-neutral'
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
      <section className="card border border-main-border bg-base-100">
        <div className="card-body p-6">
          <h2 className="text-2xl font-semibold text-primary">
            Assign CAO, department chair, or application admin roles
          </h2>

          <div className="mt-5 grid gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.4fr)_minmax(0,1fr)_auto] lg:items-end">
            <label className="form-control">
              <span className="label-text font-medium">Role</span>
              <select
                className="select select-bordered mt-2 w-full"
                onChange={(event) => {
                  const nextType = event.target
                    .value as AdminAssignableRoleType;
                  setType(nextType);
                  setTargetId(
                    nextType === 'chair' && selectedUser
                      ? selectedUser.departmentOptions.length === 1
                        ? (selectedUser.departmentOptions[0]?.id ?? '')
                        : ''
                      : ''
                  );
                  setError(null);
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
              <PersonSearchField
                allUsers={data.users}
                isOpen={isPersonSearchOpen}
                onChangeOpen={setIsPersonSearchOpen}
                onChangeQuery={(value) => {
                  setPersonQuery(value);
                  setIamId('');
                  if (type === 'chair') {
                    setTargetId('');
                  }
                  setIsPersonSearchOpen(true);
                }}
                onSelectUser={(user) => {
                  setIamId(user.iamId);
                  setPersonQuery(user.name);
                  setIsPersonSearchOpen(false);
                  setTargetId(
                    type === 'chair'
                      ? user.departmentOptions.length === 1
                        ? (user.departmentOptions[0]?.id ?? '')
                        : ''
                      : ''
                  );
                  setError(null);
                }}
                query={personQuery}
                selectedIamId={iamId}
                users={filteredUsers}
              />
            </label>

            {type !== 'admin' ? (
              type === 'cao' ? (
                <label className="form-control">
                  <span className="label-text font-medium">Cluster</span>
                  <select
                    className="select select-bordered mt-2 w-full"
                    onChange={(event) => setTargetId(event.target.value)}
                    value={targetId}
                  >
                    <option value="">Select scope</option>
                    {targetOptions.map((option) => (
                      <option
                        disabled={!option.active}
                        key={option.id}
                        value={option.id}
                      >
                        {option.name}
                        {!option.active ? ' (inactive)' : ''}
                      </option>
                    ))}
                  </select>
                </label>
              ) : (
                <label className="form-control">
                  <span className="label-text font-medium">Department</span>
                  <select
                    className="select select-bordered mt-2 w-full"
                    disabled={
                      !selectedUser || chairDepartmentOptions.length === 0
                    }
                    onChange={(event) => setTargetId(event.target.value)}
                    value={targetId}
                  >
                    <option value="">
                      {!selectedUser
                        ? 'Select a person first'
                        : chairDepartmentOptions.length === 0
                          ? 'No current departments available'
                          : 'Select department'}
                    </option>
                    {chairDepartmentOptions.map((department) => (
                      <option
                        disabled={!department.active}
                        key={department.id}
                        value={department.id}
                      >
                        {department.name}
                        {!department.active ? ' (inactive)' : ''}
                      </option>
                    ))}
                  </select>
                </label>
              )
            ) : (
              <div className="hidden lg:block" />
            )}

            <button
              className={`btn btn-primary lg:self-end ${
                !pendingAction && error ? 'opacity-60' : ''
              }`}
              disabled={
                isSaving ||
                !iamId ||
                (type === 'cao' && !targetId) ||
                (type === 'chair' && !targetId) ||
                (type !== 'admin' &&
                  selectedTarget !== null &&
                  !selectedTarget.active)
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

          {!pendingAction && error ? (
            <div className="mt-4 rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">
              {error}
            </div>
          ) : null}
        </div>
      </section>

      <section className="card border border-main-border bg-base-100">
        <div className="card-body p-6">
          <h2 className="text-lg font-semibold text-primary">
            Role assignments
          </h2>
          <div className="mt-5">
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
                <label className="label cursor-pointer gap-3 rounded-lg border border-base-300 px-4 py-2">
                  <span className="label-text text-sm text-base-content">
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
          </div>
        </div>
      </section>

      {pendingAction ? (
        <RoleWarningModal
          action={pendingAction}
          errorMessage={error}
          isSaving={isSaving}
          onCancel={() => {
            setPendingAction(null);
            setError(null);
          }}
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
      This will grant {roleLabels[roleType]} access to{' '}
      <strong>{personName}</strong>
      {scopeName === 'Application-wide' ? (
        ''
      ) : (
        <>
          {' '}
          for <strong>{scopeName}</strong>
        </>
      )}
      .
    </span>
  ) : roleType === 'admin' ? (
    <span>
      This will remove application admin access for{' '}
      <strong>{personName}</strong>.
    </span>
  ) : (
    <span>
      This will close the active {roleLabels[roleType]} assignment for{' '}
      <strong>{personName}</strong> in <strong>{scopeName}</strong>.
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
  errorMessage,
  isSaving,
  onCancel,
  onConfirm,
}: {
  action: PendingRoleAction;
  errorMessage: string | null;
  isSaving: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  const { confirmLabel, message, title } = getRoleWarningModalText(action);

  return (
    <WarningModal
      confirmLabel={confirmLabel}
      errorMessage={errorMessage}
      isConfirmDisabled={Boolean(errorMessage)}
      isSaving={isSaving}
      onCancel={onCancel}
      onConfirm={onConfirm}
      title={title}
    >
      {message}
    </WarningModal>
  );
}

export function PersonSearchField({
  allUsers,
  isOpen,
  onChangeOpen,
  onChangeQuery,
  onSelectUser,
  query,
  selectedIamId,
  users,
}: {
  allUsers: AdminRolePersonOption[];
  isOpen: boolean;
  onChangeOpen: (value: boolean) => void;
  onChangeQuery: (value: string) => void;
  onSelectUser: (user: AdminRolePersonOption) => void;
  query: string;
  selectedIamId: string;
  users: AdminRolePersonOption[];
}) {
  const showResults = isOpen && query.trim().length > 0;
  const resultsId = useId();
  const [activeIndex, setActiveIndex] = useState(0);
  const visibleUsers = users.slice(0, 8);

  return (
    <div
      className="relative mt-2 space-y-2"
      onBlur={(event) => {
        const nextFocusedElement = event.relatedTarget;

        if (
          !isNode(nextFocusedElement) ||
          !event.currentTarget.contains(nextFocusedElement)
        ) {
          onChangeOpen(false);
        }
      }}
    >
      <input
        aria-autocomplete="list"
        aria-controls={resultsId}
        aria-expanded={showResults}
        className="input input-bordered w-full"
        onChange={(event) => {
          onChangeQuery(event.target.value);
          onChangeOpen(true);
          setActiveIndex(0);
        }}
        onFocus={() => {
          onChangeOpen(true);
          setActiveIndex(0);
        }}
        onKeyDown={(event) => {
          if (event.key === 'Escape') {
            event.preventDefault();
            onChangeOpen(false);
          }
        }}
        placeholder="Search name, email, IAM ID, or department"
        role="combobox"
        type="text"
        value={query}
      />

      {showResults ? (
        <div
          className="absolute left-0 right-0 top-full z-20 mt-2 max-h-52 overflow-y-auto rounded-lg border border-base-300 bg-base-100 shadow-lg"
          id={resultsId}
          role="listbox"
        >
          {visibleUsers.map((user, index) => {
            const isSelected = user.iamId === selectedIamId;
            const isActive = index === activeIndex;

            return (
              <button
                aria-selected={isSelected}
                className={`flex w-full items-start justify-between gap-4 px-4 py-3 text-left hover:bg-base-200 ${
                  isSelected || isActive ? 'bg-base-200' : ''
                }`}
                id={`person-search-option-${user.iamId}`}
                key={user.iamId}
                onClick={() => onSelectUser(user)}
                onFocus={() => setActiveIndex(index)}
                onMouseDown={(event) => event.preventDefault()}
                role="option"
                tabIndex={0}
                type="button"
              >
                <span>
                  <span className="block font-semibold text-base-content">
                    {user.name}
                  </span>
                  <span className="block text-xs text-base-content/70">
                    {user.email || user.iamId}
                  </span>
                </span>
                <span className="text-right text-xs text-base-content/70">
                  <span className="block font-mono">{user.iamId}</span>
                  <span className="block">
                    {user.departmentName ?? 'No department'}
                  </span>
                </span>
              </button>
            );
          })}

          {visibleUsers.length === 0 ? (
            <div className="px-4 py-3 text-sm text-base-content/70">
              No people match that search.
            </div>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}

function isNode(value: EventTarget | null): value is Node {
  return value !== null && typeof value === 'object' && 'nodeType' in value;
}
