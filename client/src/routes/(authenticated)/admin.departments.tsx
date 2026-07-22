import { useState } from 'react';
import { createFileRoute } from '@tanstack/react-router';
import { useMutation, useQueryClient, useSuspenseQuery } from '@tanstack/react-query';
import {
  adminDepartmentsQueryOptions,
  createAdminCluster,
  createAdminDepartment,
  removeAdminDepartmentRoutingEmail,
  updateAdminDepartment,
  upsertAdminDepartmentRoutingEmail,
} from '@/queries/adminDepartments.ts';
import { AdminDepartmentCreationPanel } from '@/shared/admin/AdminDepartmentCreationPanel.tsx';
import { DepartmentRow } from '@/shared/admin/DepartmentRow.tsx';
import { DepartmentSettingsModal } from '@/shared/admin/DepartmentSettingsModal.tsx';
import { getAdminMutationErrorMessage } from '@/shared/admin/adminErrors.ts';
import { ArrowLeftIcon } from '@heroicons/react/24/outline';
import { statusTextColors } from '@/shared/statusColors.ts';

export const Route = createFileRoute('/(authenticated)/admin/departments')({
  component: AdminDepartmentsRoute,
  loader: ({ context }) =>
    context.queryClient.ensureQueryData(adminDepartmentsQueryOptions()),
  pendingComponent: () => (
    <section className="rounded-[1.25rem] border border-[var(--admin-border)] bg-white p-6 shadow-sm">
      <h2 className="text-lg font-semibold text-[var(--admin-blue)]">
        Loading department data
      </h2>
      <p className="mt-2 text-sm text-[var(--admin-ink-muted)]">
        Pulling the current department and cluster records from the database.
      </p>
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
  const [viewDepartmentId, setViewDepartmentId] = useState<string | null>(null);

  const invalidateDepartments = async () => {
    await queryClient.invalidateQueries({ queryKey: ['admin', 'departments'] });
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
        (user) => user.departmentId === selectedDepartment.id
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
                  <p className="mt-2 text-sm text-base-content/70">
                    This roster is derived from each user&apos;s latest leave
                    request snapshot in the database.
                  </p>
                </div>
                <div className="text-sm text-base-content/70">
                  {departmentUsers.length} people linked by request history
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
                        <td>{user.role === 'admin' ? 'Admin' : 'Faculty'}</td>
                        <td className="font-mono text-xs">{user.iamId}</td>
                      </tr>
                    ))}
                    {departmentUsers.length === 0 ? (
                      <tr>
                        <td
                          className="py-6 text-sm text-base-content/70"
                          colSpan={4}
                        >
                          No users currently map to this department from stored
                          leave request snapshots.
                        </td>
                      </tr>
                    ) : null}
                  </tbody>
                </table>
              </div>
            </div>
          </section>
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

      <section className="rounded-[1.25rem] border border-[var(--admin-border)] bg-white p-6 shadow-sm">
        <div className="max-w-3xl space-y-2">
          <h2 className="text-lg font-semibold text-[var(--admin-blue)]">
            Department and cluster management
          </h2>
          <p className="text-sm text-[var(--admin-ink-muted)]">
            These records are now backed by SQL Server. Cluster names,
            department names, approval mode, and routing emails persist to the
            database.
          </p>
          <p className="text-sm text-[var(--admin-ink-muted)]">
            Cluster names and department assignments are live. Chair and CAO
            assignments are still pending schema support.
          </p>
        </div>
      </section>

      {clusterGroups.map((cluster) => (
        <section
          className="rounded-[1.25rem] border border-[var(--admin-border)] bg-white p-6 shadow-sm"
          key={cluster.id}
        >
          <div className="mb-5 flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
            <div>
              <label className="text-xs font-semibold uppercase tracking-[0.24em] text-[var(--admin-gold-deep)]">
                Cluster
              </label>
              <div className="mt-2 w-full max-w-md rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-sand)] px-4 py-3 text-[var(--admin-blue)]">
                {cluster.name}
              </div>
            </div>
          </div>

          <div className="space-y-3 border-l-4 border-[var(--admin-border-strong)] pl-5">
            {cluster.departments.map((department) => {
              const linkedUserCount = users.filter(
                (user) => user.departmentId === department.id
              ).length;

              return (
                <DepartmentRow
                  department={department}
                  key={department.id}
                  linkedUserCount={linkedUserCount}
                  onOpenRoster={() => setViewDepartmentId(department.id)}
                  onOpenSettings={() => setEditingDepartmentId(department.id)}
                />
              );
            })}
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
          onClose={() => setEditingDepartmentId(null)}
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
    </div>
  );
}
