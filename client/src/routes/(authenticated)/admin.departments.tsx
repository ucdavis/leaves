import { useState } from 'react';
import { createFileRoute } from '@tanstack/react-router';
import {
  useAdminData,
} from '@/shared/admin/adminData.tsx';
import { HttpError } from '@/lib/api.ts';
import { DepartmentRow } from '@/shared/admin/DepartmentRow.tsx';
import { DepartmentSettingsModal } from '@/shared/admin/DepartmentSettingsModal.tsx';

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
            Back to departments
          </button>

          <section className="rounded-[1.25rem] border border-[var(--admin-border)] bg-white p-6 shadow-sm">
            <div className="mb-5 flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
              <div>
                <h2 className="text-2xl font-semibold text-[var(--admin-blue)]">
                  {selectedDepartment.name}
                </h2>
                <p className="mt-2 text-sm text-[var(--admin-ink-muted)]">
                  This roster is derived from each user&apos;s latest leave request
                  snapshot in the database.
                </p>
              </div>
              <div className="text-sm text-[var(--admin-ink-muted)]">
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
                          <span className="italic text-rose-700">Missing</span>
                        )}
                      </td>
                      <td>{user.role === 'admin' ? 'Admin' : 'Faculty'}</td>
                      <td className="font-mono text-xs">{user.iamId}</td>
                    </tr>
                  ))}
                  {departmentUsers.length === 0 ? (
                    <tr>
                      <td
                        className="py-6 text-sm text-[var(--admin-ink-muted)]"
                        colSpan={4}
                      >
                        No users currently map to this department from stored leave
                        request snapshots.
                      </td>
                    </tr>
                  ) : null}
                </tbody>
              </table>
            </div>
          </section>
        </div>
      );
    }
  }

  const editingDepartment =
    editingDepartmentId === null
      ? null
      : departments.find((department) => department.id === editingDepartmentId) ??
        null;

  return (
    <div className="space-y-6">
      <section className="rounded-[1.25rem] border border-[var(--admin-border)] bg-white p-6 shadow-sm">
        <h2 className="text-lg font-semibold text-[var(--admin-blue)]">
          Department and cluster management
        </h2>
        <p className="mt-2 max-w-3xl text-sm leading-6 text-[var(--admin-ink-muted)]">
          These cards are now backed by the database. Cluster names, department
          names, approval mode, and routing emails persist to SQL Server.
        </p>
        <p className="mt-3 text-sm text-[var(--admin-ink-muted)]">
          {readonlyReason}
        </p>
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

            <div className="min-w-72 rounded-2xl bg-[var(--admin-sand)] p-4">
              <div className="text-xs font-semibold uppercase tracking-[0.2em] text-[var(--admin-ink-soft)]">
                Persisted fields
              </div>
              <p className="mt-3 text-sm text-[var(--admin-ink-muted)]">
                Cluster names and department assignments are live. Chair and CAO
                assignments are still pending schema support.
              </p>
            </div>
          </div>

          <div className="space-y-3">
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
        <section className="rounded-[1.25rem] border border-dashed border-[var(--admin-border)] bg-white p-6 shadow-sm">
          <h2 className="text-lg font-semibold text-[var(--admin-ink-muted)]">
            Unassigned to cluster
          </h2>
          <div className="mt-4 space-y-3">
            {unassignedDepartments.map((department) => (
              <DepartmentRow
                department={department}
                key={department.id}
                linkedUserCount={users.filter(
                  (user) => user.departmentId === department.id && user.active
                ).length}
                onOpenRoster={() => setViewDepartmentId(department.id)}
                onOpenSettings={() => setEditingDepartmentId(department.id)}
                onRename={(name) => renameDepartment(department.id, name)}
              />
            ))}
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
