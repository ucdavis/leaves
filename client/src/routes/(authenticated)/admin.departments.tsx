import { useState } from 'react';
import { createFileRoute } from '@tanstack/react-router';
import {
  useMutation,
  useQueryClient,
  useSuspenseQuery,
} from '@tanstack/react-query';
import {
  adminDepartmentsQueryOptions,
  createAdminCluster,
  createAdminDepartment,
  deleteAdminCluster,
  deleteAdminDepartment,
  removeAdminDepartmentRoutingEmail,
  updateAdminCluster,
  updateAdminDepartment,
  upsertAdminDepartmentRoutingEmail,
} from '@/queries/adminDepartments.ts';
import { ClusterSettingsModal } from '@/shared/admin/ClusterSettingsModal.tsx';
import { AdminDepartmentCreationPanel } from '@/shared/admin/AdminDepartmentCreationPanel.tsx';
import { DepartmentRow } from '@/shared/admin/DepartmentRow.tsx';
import { DepartmentSettingsModal } from '@/shared/admin/DepartmentSettingsModal.tsx';
import { getAdminMutationErrorMessage } from '@/shared/admin/adminErrors.ts';
import { WarningModal } from '@/shared/WarningModal.tsx';
import {
  ArrowLeftIcon,
  CheckCircleIcon,
  Cog6ToothIcon,
  PencilSquareIcon,
} from '@heroicons/react/24/outline';
import { statusTextColors } from '@/shared/statusColors.ts';

export const Route = createFileRoute('/(authenticated)/admin/departments')({
  component: AdminDepartmentsRoute,
  loader: ({ context }) =>
    context.queryClient.ensureQueryData(adminDepartmentsQueryOptions()),
  pendingComponent: () => (
    <section className="card border border-main-border bg-base-100">
      <div className="card-body p-6">
        <h2 className="text-lg font-semibold text-primary">
          Loading department data
        </h2>
        <p className="mt-2 text-sm text-base-content/70">
          Pulling the current department and cluster records from the database.
        </p>
      </div>
    </section>
  ),
});

function AdminDepartmentsRoute() {
  const queryClient = useQueryClient();
  const { data } = useSuspenseQuery(adminDepartmentsQueryOptions());
  const { clusters, departments, users } = data;
  const [editingDepartmentId, setEditingDepartmentId] = useState<string | null>(
    null
  );
  const [editingClusterCaoId, setEditingClusterCaoId] = useState<string | null>(
    null
  );
  const [clusterCaoQuery, setClusterCaoQuery] = useState('');
  const [isClusterCaoSearchOpen, setIsClusterCaoSearchOpen] = useState(false);
  const [selectedClusterCaoUserId, setSelectedClusterCaoUserId] = useState('');
  const [pendingCaoChange, setPendingCaoChange] = useState<{
    clusterId: string;
    clusterName: string;
    currentCaoName: string | null;
    nextCaoName: string;
    nextCaoUserId: string;
  } | null>(null);
  const [pendingCaoChangeError, setPendingCaoChangeError] = useState<
    string | null
  >(null);
  const [pendingChairChange, setPendingChairChange] = useState<{
    currentChairName: string | null;
    departmentId: string;
    departmentName: string;
    nextChairName: string;
    nextChairUserId: string;
  } | null>(null);
  const [pendingChairChangeError, setPendingChairChangeError] = useState<
    string | null
  >(null);
  const [viewDepartmentId, setViewDepartmentId] = useState<string | null>(null);
  const [editingClusterSettingsId, setEditingClusterSettingsId] = useState<
    string | null
  >(null);

  const invalidateDepartments = async () => {
    await queryClient.invalidateQueries({
      queryKey: adminDepartmentsQueryOptions().queryKey,
    });
  };

  const createClusterMutation = useMutation({
    mutationFn: createAdminCluster,
    onSuccess: invalidateDepartments,
  });
  const createDepartmentMutation = useMutation({
    mutationFn: createAdminDepartment,
    onSuccess: invalidateDepartments,
  });
  const updateDepartmentMutation = useMutation({
    mutationFn: updateAdminDepartment,
    onSuccess: invalidateDepartments,
  });
  const deleteDepartmentMutation = useMutation({
    mutationFn: deleteAdminDepartment,
    onSuccess: invalidateDepartments,
  });
  const updateClusterMutation = useMutation({
    mutationFn: updateAdminCluster,
    onSuccess: invalidateDepartments,
  });
  const deleteClusterMutation = useMutation({
    mutationFn: deleteAdminCluster,
    onSuccess: invalidateDepartments,
  });
  const upsertRoutingEmailMutation = useMutation({
    mutationFn: upsertAdminDepartmentRoutingEmail,
    onSuccess: invalidateDepartments,
  });
  const removeRoutingEmailMutation = useMutation({
    mutationFn: removeAdminDepartmentRoutingEmail,
    onSuccess: invalidateDepartments,
  });

  const clusterGroups = clusters.map((cluster) => ({
    ...cluster,
    departments: departments.filter(
      (department) => department.clusterId === cluster.id
    ),
  }));
  const unassignedDepartments = departments.filter(
    (department) => !department.clusterId
  );

  if (viewDepartmentId) {
    const selectedDepartment = departments.find(
      (department) => department.id === viewDepartmentId
    );

    if (selectedDepartment) {
      const departmentUsers = users.filter(
        (user) =>
          user.departmentId === selectedDepartment.id &&
          user.designation !== 'nfa' &&
          user.role !== 'cao'
      );

      return (
        <div className="space-y-5">
          <button
            className="btn btn-ghost"
            onClick={() => setViewDepartmentId(null)}
            type="button"
          >
            <ArrowLeftIcon aria-hidden="true" className="h-5 w-5 shrink-0" />
            Back to departments
          </button>

          <section className="card border border-main-border bg-base-100">
            <div className="card-body p-6">
              <div className="mb-5 flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
                <div>
                  <h2 className="text-2xl font-semibold text-primary">
                    {selectedDepartment.name}
                  </h2>
                </div>
              </div>

              <div className="overflow-x-auto">
                <table className="table">
                  <thead>
                    <tr>
                      <th>Name</th>
                      <th>Email</th>
                      <th>Role</th>
                      <th>IAM ID</th>
                      <th>Department chair</th>
                    </tr>
                  </thead>
                  <tbody>
                    {departmentUsers.map((user) => (
                      <tr key={user.id}>
                        <td className="font-semibold">{user.name}</td>
                        <td>
                          {user.email ? (
                            user.email
                          ) : (
                            <span
                              className={`italic ${statusTextColors.danger}`}
                            >
                              Missing
                            </span>
                          )}
                        </td>
                        <td>{user.role === 'chair' ? 'Chair' : 'Faculty'}</td>
                        <td className="font-mono text-xs">{user.iamId}</td>
                        <td>
                          {selectedDepartment.chairUserId === user.id ? (
                            <span className="inline-flex items-center gap-2 text-sm font-semibold text-success">
                              <CheckCircleIcon
                                aria-hidden="true"
                                className="h-4 w-4 shrink-0"
                              />
                              Chair
                            </span>
                          ) : (
                            <button
                              className="btn btn-ghost btn-sm"
                              disabled={updateDepartmentMutation.isPending}
                              onClick={() => {
                                setPendingChairChangeError(null);
                                setPendingChairChange({
                                  currentChairName:
                                    departmentUsers.find(
                                      (departmentUser) =>
                                        departmentUser.id ===
                                        selectedDepartment.chairUserId
                                    )?.name ?? null,
                                  departmentId: selectedDepartment.id,
                                  departmentName: selectedDepartment.name,
                                  nextChairName: user.name,
                                  nextChairUserId: user.id,
                                });
                              }}
                              type="button"
                            >
                              Set chair
                            </button>
                          )}
                        </td>
                      </tr>
                    ))}
                    {departmentUsers.length === 0 ? (
                      <tr>
                        <td
                          className="py-6 text-sm text-base-content/70"
                          colSpan={5}
                        >
                          There are currently no faculty members assigned to
                          this department.
                        </td>
                      </tr>
                    ) : null}
                  </tbody>
                </table>
              </div>
            </div>
          </section>

          {pendingChairChange &&
          pendingChairChange.departmentId === selectedDepartment.id ? (
            <DepartmentChairWarningModal
              action={pendingChairChange}
              errorMessage={pendingChairChangeError}
              isSaving={updateDepartmentMutation.isPending}
              onCancel={() => {
                setPendingChairChange(null);
                setPendingChairChangeError(null);
              }}
              onConfirm={() => {
                void (async () => {
                  setPendingChairChangeError(null);

                  try {
                    await updateDepartmentMutation.mutateAsync({
                      departmentId: pendingChairChange.departmentId,
                      updates: {
                        chairUserId: pendingChairChange.nextChairUserId,
                      },
                    });
                    setPendingChairChange(null);
                  } catch (error) {
                    setPendingChairChangeError(
                      getAdminMutationErrorMessage(error)
                    );
                  }
                })();
              }}
            />
          ) : null}
        </div>
      );
    }
  }

  const editingDepartment =
    editingDepartmentId === null
      ? null
      : (departments.find(
          (department) => department.id === editingDepartmentId
        ) ?? null);
  const editingClusterSettings =
    editingClusterSettingsId === null
      ? null
      : (clusters.find((cluster) => cluster.id === editingClusterSettingsId) ??
        null);

  return (
    <div className="space-y-6">
      <AdminDepartmentCreationPanel
        clusters={clusters}
        formatError={getAdminMutationErrorMessage}
        onCreateCluster={(name) => createClusterMutation.mutateAsync({ name })}
        onCreateDepartment={(input) =>
          createDepartmentMutation.mutateAsync({ input })
        }
      />

      <h2 className="text-2xl font-semibold text-primary">
        Department and cluster management
      </h2>

      {clusterGroups.map((cluster) => (
        <section
          className="card border border-main-border bg-base-100"
          key={cluster.id}
        >
          <div className="card-body p-6">
            <div className="mb-5 flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
              <div>
                <div className="text-lg font-semibold">{cluster.name}</div>
                <ClusterCaoEditor
                  currentCaoName={
                    users.find((user) => user.id === cluster.caoUserId)?.name ??
                    null
                  }
                  isEditing={editingClusterCaoId === cluster.id}
                  isSaving={updateClusterMutation.isPending}
                  isSearchOpen={isClusterCaoSearchOpen}
                  onCancel={() => {
                    setEditingClusterCaoId(null);
                    setClusterCaoQuery('');
                    setIsClusterCaoSearchOpen(false);
                    setSelectedClusterCaoUserId('');
                  }}
                  onChangeQuery={(value) => {
                    setClusterCaoQuery(value);
                    setIsClusterCaoSearchOpen(true);
                  }}
                  onConfirm={() => {
                    const selectedUser = users.find(
                      (user) => user.id === selectedClusterCaoUserId
                    );
                    if (!selectedUser) {
                      return;
                    }

                    setPendingCaoChangeError(null);
                    setPendingCaoChange({
                      clusterId: cluster.id,
                      clusterName: cluster.name,
                      currentCaoName:
                        users.find((user) => user.id === cluster.caoUserId)
                          ?.name ?? null,
                      nextCaoName: selectedUser.name,
                      nextCaoUserId: selectedUser.id,
                    });
                  }}
                  onEdit={() => {
                    setEditingClusterCaoId(cluster.id);
                    setClusterCaoQuery('');
                    setIsClusterCaoSearchOpen(false);
                    setSelectedClusterCaoUserId('');
                  }}
                  onSelectUser={(user) => {
                    setSelectedClusterCaoUserId(user.id);
                    setClusterCaoQuery(user.name);
                    setIsClusterCaoSearchOpen(false);
                  }}
                  query={clusterCaoQuery}
                  selectedUserId={selectedClusterCaoUserId}
                  users={getNonFacultyAssignableUsers(users)}
                />
              </div>

              <div className="flex justify-end">
                <button
                  aria-label={`Open settings for ${cluster.name}`}
                  className="btn btn-outline self-stretch shrink-0 px-4"
                  onClick={() => setEditingClusterSettingsId(cluster.id)}
                  type="button"
                >
                  <Cog6ToothIcon
                    aria-hidden="true"
                    className="h-5 w-5 shrink-0"
                  />
                  Cluster Settings
                </button>
              </div>
            </div>

            <div className="space-y-3">
              {cluster.departments.map((department) => {
                const linkedUserCount = users.filter(
                  (user) => user.departmentId === department.id
                ).length;

                return (
                  <DepartmentRow
                    chairName={
                      users.find((user) => user.id === department.chairUserId)
                        ?.name ?? null
                    }
                    department={department}
                    key={department.id}
                    linkedUserCount={linkedUserCount}
                    onOpenRoster={() => setViewDepartmentId(department.id)}
                    onOpenSettings={() => setEditingDepartmentId(department.id)}
                  />
                );
              })}
            </div>
          </div>
        </section>
      ))}

      {unassignedDepartments.length > 0 ? (
        <section className="card border border-dashed border-main-border bg-base-100">
          <div className="card-body p-6">
            <h2 className="text-lg font-semibold text-base-content/70">
              Unassigned to cluster
            </h2>
            <div className="mt-4 space-y-3">
              {unassignedDepartments.map((department) => (
                <DepartmentRow
                  chairName={
                    users.find((user) => user.id === department.chairUserId)
                      ?.name ?? null
                  }
                  department={department}
                  key={department.id}
                  linkedUserCount={
                    users.filter(
                      (user) =>
                        user.departmentId === department.id && user.active
                    ).length
                  }
                  onOpenRoster={() => setViewDepartmentId(department.id)}
                  onOpenSettings={() => setEditingDepartmentId(department.id)}
                />
              ))}
            </div>
          </div>
        </section>
      ) : null}

      {editingDepartment ? (
        <DepartmentSettingsModal
          clusters={clusters}
          department={editingDepartment}
          formatError={getAdminMutationErrorMessage}
          isDeleting={deleteDepartmentMutation.isPending}
          onClose={() => setEditingDepartmentId(null)}
          onDelete={() =>
            deleteDepartmentMutation
              .mutateAsync({
                departmentId: editingDepartment.id,
              })
              .then(() => {
                setEditingDepartmentId(null);
                if (viewDepartmentId === editingDepartment.id) {
                  setViewDepartmentId(null);
                }
              })
          }
          onRemoveRoutingEmail={(emailId) =>
            removeRoutingEmailMutation.mutateAsync({
              departmentId: editingDepartment.id,
              emailId,
            })
          }
          onSave={(updates) =>
            updateDepartmentMutation
              .mutateAsync({
                departmentId: editingDepartment.id,
                updates,
              })
              .then(() => {
                setEditingDepartmentId(null);
              })
          }
          onUpsertRoutingEmail={(email) =>
            upsertRoutingEmailMutation.mutateAsync({
              departmentId: editingDepartment.id,
              email,
            })
          }
        />
      ) : null}

      {editingClusterSettings ? (
        <ClusterSettingsModal
          cluster={editingClusterSettings}
          departmentCount={
            departments.filter(
              (department) => department.clusterId === editingClusterSettings.id
            ).length
          }
          formatError={getAdminMutationErrorMessage}
          isDeleting={deleteClusterMutation.isPending}
          onClose={() => setEditingClusterSettingsId(null)}
          onDelete={() =>
            deleteClusterMutation
              .mutateAsync({
                clusterId: editingClusterSettings.id,
              })
              .then(() => {
                setEditingClusterSettingsId(null);
                if (editingClusterCaoId === editingClusterSettings.id) {
                  setEditingClusterCaoId(null);
                  setClusterCaoQuery('');
                  setIsClusterCaoSearchOpen(false);
                  setSelectedClusterCaoUserId('');
                }
              })
          }
          onSave={(updates) =>
            updateClusterMutation
              .mutateAsync({
                clusterId: editingClusterSettings.id,
                updates,
              })
              .then(() => {
                setEditingClusterSettingsId(null);
              })
          }
        />
      ) : null}

      {pendingCaoChange ? (
        <ClusterCaoWarningModal
          action={pendingCaoChange}
          errorMessage={pendingCaoChangeError}
          isSaving={updateClusterMutation.isPending}
          onCancel={() => {
            setPendingCaoChange(null);
            setPendingCaoChangeError(null);
          }}
          onConfirm={() => {
            void (async () => {
              setPendingCaoChangeError(null);

              try {
                await updateClusterMutation.mutateAsync({
                  clusterId: pendingCaoChange.clusterId,
                  updates: { caoUserId: pendingCaoChange.nextCaoUserId },
                });
                setPendingCaoChange(null);
                setEditingClusterCaoId(null);
                setClusterCaoQuery('');
                setIsClusterCaoSearchOpen(false);
                setSelectedClusterCaoUserId('');
              } catch (error) {
                setPendingCaoChangeError(getAdminMutationErrorMessage(error));
              }
            })();
          }}
        />
      ) : null}
    </div>
  );
}

function getNonFacultyAssignableUsers(
  users: Array<{
    active: boolean;
    departmentId: string;
    designation: string;
    email: string;
    id: string;
    name: string;
  }>
) {
  return users
    .filter((user) => user.active && user.designation === 'nfa')
    .sort((left, right) => left.name.localeCompare(right.name));
}

function ClusterCaoEditor({
  currentCaoName,
  isEditing,
  isSaving,
  isSearchOpen,
  onCancel,
  onChangeQuery,
  onConfirm,
  onEdit,
  onSelectUser,
  query,
  selectedUserId,
  users,
}: {
  currentCaoName: string | null;
  isEditing: boolean;
  isSaving: boolean;
  isSearchOpen: boolean;
  onCancel: () => void;
  onChangeQuery: (value: string) => void;
  onConfirm: () => void;
  onEdit: () => void;
  onSelectUser: (user: { email: string; id: string; name: string }) => void;
  query: string;
  selectedUserId: string;
  users: Array<{
    email: string;
    id: string;
    name: string;
  }>;
}) {
  const filteredUsers = users.filter((user) => {
    const normalizedQuery = query.trim().toLowerCase();
    if (!normalizedQuery) {
      return true;
    }

    return [user.name, user.email, user.id]
      .join(' ')
      .toLowerCase()
      .includes(normalizedQuery);
  });
  const showResults = isSearchOpen && query.trim().length > 0;

  return (
    <div className="mt-2 max-w-md">
      <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm text-base-content/70">
        <span>CAO: {currentCaoName ?? 'Add CAO'}</span>
        <button
          aria-label="Edit CAO"
          className="inline-flex items-center text-primary hover:text-primary/80"
          onClick={onEdit}
          type="button"
        >
          <PencilSquareIcon aria-hidden="true" className="h-4 w-4 shrink-0" />
        </button>
      </div>

      {isEditing ? (
        <div className="mt-3 space-y-3">
          <div className="relative">
            <input
              className="input input-bordered w-full"
              onChange={(event) => onChangeQuery(event.target.value)}
              onFocus={() => {
                if (query.trim().length > 0) {
                  onChangeQuery(query);
                }
              }}
              placeholder="Search people"
              type="text"
              value={query}
            />
            {showResults ? (
              <div className="absolute left-0 right-0 top-full z-20 mt-2 max-h-52 overflow-y-auto rounded-lg border border-base-300 bg-base-100 shadow-lg">
                {filteredUsers.slice(0, 8).map((user) => (
                  <button
                    className={`flex w-full items-start justify-between gap-4 px-4 py-3 text-left hover:bg-base-200 ${
                      user.id === selectedUserId ? 'bg-base-200' : ''
                    }`}
                    key={user.id}
                    onClick={() => onSelectUser(user)}
                    onMouseDown={(event) => event.preventDefault()}
                    type="button"
                  >
                    <span>
                      <span className="block font-semibold text-base-content">
                        {user.name}
                      </span>
                      <span className="block text-xs text-base-content/70">
                        {user.email || user.id}
                      </span>
                    </span>
                    <span className="text-right text-xs text-base-content/70">
                      {user.id}
                    </span>
                  </button>
                ))}
              </div>
            ) : null}
          </div>

          <div className="flex justify-end gap-3">
            <button
              className="btn btn-ghost btn-sm"
              onClick={onCancel}
              type="button"
            >
              Cancel
            </button>
            <button
              className="btn btn-primary btn-sm"
              disabled={isSaving || !selectedUserId}
              onClick={onConfirm}
              type="button"
            >
              Confirm
            </button>
          </div>
        </div>
      ) : null}
    </div>
  );
}

function DepartmentChairWarningModal({
  action,
  errorMessage,
  isSaving,
  onCancel,
  onConfirm,
}: {
  action: {
    currentChairName: string | null;
    departmentId: string;
    departmentName: string;
    nextChairName: string;
    nextChairUserId: string;
  };
  errorMessage: string | null;
  isSaving: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  const title = action.currentChairName
    ? 'Change department chair?'
    : 'Add department chair?';

  return (
    <WarningModal
      confirmLabel={action.currentChairName ? 'Change chair' : 'Add chair'}
      errorMessage={errorMessage}
      isSaving={isSaving}
      onCancel={onCancel}
      onConfirm={onConfirm}
      title={title}
    >
      {action.currentChairName ? (
        <span>
          This action will replace <strong>{action.currentChairName}</strong>{' '}
          with <strong>{action.nextChairName}</strong> as chair for the{' '}
          <strong>{action.departmentName}</strong> department effective
          immediately.
        </span>
      ) : (
        <span>
          This action will assign <strong>{action.nextChairName}</strong> as
          chair for the <strong>{action.departmentName}</strong> department
          effective immediately.
        </span>
      )}
    </WarningModal>
  );
}

function ClusterCaoWarningModal({
  action,
  errorMessage,
  isSaving,
  onCancel,
  onConfirm,
}: {
  action: {
    clusterId: string;
    clusterName: string;
    currentCaoName: string | null;
    nextCaoName: string;
    nextCaoUserId: string;
  };
  errorMessage: string | null;
  isSaving: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  return (
    <WarningModal
      confirmLabel={action.currentCaoName ? 'Change CAO' : 'Add CAO'}
      errorMessage={errorMessage}
      isSaving={isSaving}
      onCancel={onCancel}
      onConfirm={onConfirm}
      title={action.currentCaoName ? 'Change cluster CAO?' : 'Add cluster CAO?'}
    >
      {action.currentCaoName ? (
        <span>
          This will replace <strong>{action.currentCaoName}</strong> with{' '}
          <strong>{action.nextCaoName}</strong> as CAO for the{' '}
          <strong>{action.clusterName}</strong> cluster effective immediately.
        </span>
      ) : (
        <span>
          This will assign <strong>{action.nextCaoName}</strong> as CAO for the{' '}
          <strong>{action.clusterName}</strong> cluster effective immediately.
        </span>
      )}
    </WarningModal>
  );
}
