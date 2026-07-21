import { useState } from 'react';
import { createFileRoute } from '@tanstack/react-router';
import { ArrowLeftIcon } from '@heroicons/react/24/outline';
import { useAdminData } from '@/shared/admin/adminData.tsx';
import { HttpError } from '@/lib/api.ts';
import { DepartmentRow } from '@/shared/admin/DepartmentRow.tsx';
import { DepartmentSettingsModal } from '@/shared/admin/DepartmentSettingsModal.tsx';
import { statusTextColors } from '@/shared/statusColors.ts';

export const Route = createFileRoute('/(authenticated)/admin/departments')({
  component: AdminDepartmentsRoute,
});

function AdminDepartmentsRoute() {
  const {
    clusters,
    departments,
    readonlyReason,
    removeRoutingEmail,
    renameDepartment,
    updateDepartment,
    upsertRoutingEmail,
    users,
  } = useAdminData();
  const [editingDepartmentId, setEditingDepartmentId] = useState<string | null>(
    null
  );
  const [viewDepartmentId, setViewDepartmentId] = useState<string | null>(null);

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
    <div className="space-y-2">
      <div className="max-w-3xl space-y-2">
        <h2 className="text-lg font-semibold text-primary">
          Department and cluster management
        </h2>
        <p>
          These cards are now backed by the database. Cluster names, department
          names, approval mode, and routing emails persist to SQL Server.
        </p>
        <p>{readonlyReason}</p>
        <p>
          Cluster names and department assignments are live. Chair and CAO
          assignments are still pending schema support.
        </p>
      </div>

      {clusterGroups.map((cluster) => (
        <section className="my-8" key={cluster.id}>
          <div className="flex flex-col lg:items-start lg:justify-between">
            <h3 className="h2">{cluster.name}</h3>
          </div>

          <div className="space-y-3 ps-5 border-l-5 border-primary/20 mt-5">
            {cluster.departments.map((department) => {
              const linkedUserCount = users.filter(
                (user) => user.departmentId === department.id && user.active
              ).length;

              return (
                <DepartmentRow
                  department={department}
                  key={department.id}
                  linkedUserCount={linkedUserCount}
                  onOpenRoster={() => setViewDepartmentId(department.id)}
                  onOpenSettings={() => setEditingDepartmentId(department.id)}
                  onRename={(name) => renameDepartment(department.id, name)}
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
                  onRename={(name) => renameDepartment(department.id, name)}
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
            removeRoutingEmail(editingDepartment.id, emailId)
          }
          onSave={(updates) =>
            updateDepartment(editingDepartment.id, updates).then(() => {
              setEditingDepartmentId(null);
            })
          }
          onUpsertRoutingEmail={(email) =>
            upsertRoutingEmail(editingDepartment.id, email)
          }
        />
      ) : null}
    </div>
  );
}

function getAdminMutationErrorMessage(error: unknown) {
  if (error instanceof HttpError) {
    if (typeof error.body === 'string' && error.body.trim()) {
      return error.body;
    }

    if (error.body && typeof error.body === 'object') {
      const body = error.body as {
        detail?: string;
        title?: string;
      };

      if (body.detail) {
        return body.detail;
      }

      if (body.title) {
        return body.title;
      }
    }

    return 'Unable to save the change. Please try again.';
  }

  if (error instanceof Error && error.message) {
    return error.message;
  }

  return 'Unable to save the change. Please try again.';
}
