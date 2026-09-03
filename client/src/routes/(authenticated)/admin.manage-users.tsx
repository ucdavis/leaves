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
          Pulling application admins from the database.
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
  const [iamId, setIamId] = useState('');
  const [personQuery, setPersonQuery] = useState('');
  const [isPersonSearchOpen, setIsPersonSearchOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showInactiveAssignments, setShowInactiveAssignments] = useState(false);
  const [pendingAction, setPendingAction] = useState<PendingRoleAction | null>(
    null
  );

  const refreshRoles = async () => {
    await queryClient.invalidateQueries({
      queryKey: adminRolesQueryOptions().queryKey,
    });
  };

  const addAdminMutation = useMutation({
    mutationFn: addAdminAssignment,
    onSuccess: refreshRoles,
  });
  const removeMutation = useMutation({
    mutationFn: removeRoleAssignment,
    onSuccess: refreshRoles,
  });

  const isSaving =
    addAdminMutation.isPending ||
    removeMutation.isPending;

  const selectedUser = data.users.find((user) => user.iamId === iamId) ?? null;
  const selectedUserName = selectedUser?.name ?? iamId;
  const assignmentRows = data.assignments.filter(
    (assignment) =>
      assignment.type === 'admin' &&
      (showInactiveAssignments || assignment.active)
  );
  const assignedAdminIamIds = useMemo(
    () =>
      new Set(
        data.assignments
          .filter(
            (assignment) => assignment.active && assignment.type === 'admin'
          )
          .map((assignment) => assignment.iamId.toLowerCase())
      ),
    [data.assignments]
  );
  const normalizedPersonQuery = personQuery.trim().toLowerCase();
  const filteredUsers = useMemo(
    () =>
      data.users.filter((user) => {
        if (assignedAdminIamIds.has(user.iamId.toLowerCase())) {
          return false;
        }

        if (!normalizedPersonQuery) {
          return true;
        }

        const searchableText = [user.name, user.email].join(' ').toLowerCase();

        return searchableText.includes(normalizedPersonQuery);
      }),
    [assignedAdminIamIds, data.users, normalizedPersonQuery]
  );

  const resetForm = () => {
    setIamId('');
    setPersonQuery('');
    setIsPersonSearchOpen(false);
  };

  const handleAdd = async () => {
    setError(null);

    try {
      await addAdminMutation.mutateAsync({ iamId });

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
          Remove
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
            Assign Application Admins
          </h2>

          <div className="mt-5 grid gap-4 lg:grid-cols-[minmax(0,1fr)_auto] lg:items-end">
            <label className="form-control">
              <span className="label-text font-medium">Person</span>
              <PersonSearchField
                isOpen={isPersonSearchOpen}
                onChangeOpen={setIsPersonSearchOpen}
                onChangeQuery={(value) => {
                  setPersonQuery(value);
                  setIamId('');
                  setIsPersonSearchOpen(true);
                }}
                onSelectUser={(user) => {
                  setIamId(user.iamId);
                  setPersonQuery(user.name);
                  setIsPersonSearchOpen(false);
                  setError(null);
                }}
                query={personQuery}
                selectedIamId={iamId}
                users={filteredUsers}
              />
            </label>

            <button
              className={`btn btn-primary lg:self-end ${
                !pendingAction && error ? 'opacity-60' : ''
              }`}
              disabled={
                isSaving ||
                !iamId
              }
              onClick={() =>
                setPendingAction({
                  kind: 'add',
                  roleType: 'admin',
                  targetName: null,
                  userName: selectedUserName,
                })
              }
              type="button"
            >
              {isSaving ? 'Adding...' : 'Add admin'}
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
            Application admins
          </h2>
          <div className="mt-5">
            <DataTable
              columns={columns}
              data={assignmentRows}
              filterPlaceholder="Search admin or email..."
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
  isOpen,
  onChangeOpen,
  onChangeQuery,
  onSelectUser,
  query,
  selectedIamId,
  users,
}: {
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
        placeholder="Search name or email"
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
                  {user.departmentName ?? 'No department'}
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
