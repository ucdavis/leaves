import { useState } from 'react';
import { createFileRoute } from '@tanstack/react-router';
import {
  type AdminDepartment,
  useAdminData,
} from '@/shared/admin/adminData.tsx';

export const Route = createFileRoute('/(authenticated)/admin/departments')({
  component: AdminDepartmentsRoute,
});

function AdminDepartmentsRoute() {
  const {
    clusters,
    departments,
    removeRoutingEmail,
    renameCluster,
    renameDepartment,
    setClusterCao,
    setDepartmentChair,
    updateDepartment,
    updateUser,
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
                  Adjust designations and department placement in preview mode.
                </p>
              </div>
              <div className="text-sm text-[var(--admin-ink-muted)]">
                {departmentUsers.length} rostered people
              </div>
            </div>

            <div className="overflow-x-auto">
              <table className="table">
                <thead>
                  <tr>
                    <th>Name</th>
                    <th>Email</th>
                    <th>Designation</th>
                    <th>Department</th>
                  </tr>
                </thead>
                <tbody>
                  {departmentUsers.map((user) => (
                    <tr key={user.id}>
                      <td>
                        <div className="font-semibold">{user.name}</div>
                        <div className="text-xs text-[var(--admin-ink-muted)]">
                          {user.position}
                        </div>
                      </td>
                      <td>
                        {user.email ? (
                          user.email
                        ) : (
                          <span className="italic text-rose-700">Missing</span>
                        )}
                      </td>
                      <td>
                        <select
                          className="select select-bordered select-sm"
                          onChange={(event) =>
                            updateUser(user.id, {
                              designation: event.target.value as
                                | 'fy'
                                | 'ay'
                                | 'nfa'
                                | 'chair'
                                | 'cao'
                                | 'admin',
                            })
                          }
                          value={user.designation}
                        >
                          <option value="fy">FY Faculty</option>
                          <option value="ay">AY Faculty</option>
                          <option value="nfa">Non-Faculty Academic</option>
                          <option value="chair">Chair</option>
                          <option value="cao">CAO</option>
                          <option value="admin">Admin</option>
                        </select>
                      </td>
                      <td>
                        <select
                          className="select select-bordered select-sm"
                          onChange={(event) =>
                            updateUser(user.id, {
                              departmentId: event.target.value,
                            })
                          }
                          value={user.departmentId}
                        >
                          {departments.map((department) => (
                            <option key={department.id} value={department.id}>
                              {department.name}
                            </option>
                          ))}
                        </select>
                      </td>
                    </tr>
                  ))}
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
          The structure below follows the mockup’s grouped department cards,
          chair assignment flow, and routing settings while keeping everything
          client-side until the real admin tables are ready.
        </p>
      </section>

      {clusterGroups.map((cluster) => {
        const currentCao =
          users.find((user) => user.id === cluster.caoUserId) ?? null;

        return (
          <section
            className="rounded-[1.25rem] border border-[var(--admin-border)] bg-white p-6 shadow-sm"
            key={cluster.id}
          >
            <div className="mb-5 flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
              <div>
                <label className="text-xs font-semibold uppercase tracking-[0.24em] text-[var(--admin-gold-deep)]">
                  Cluster
                </label>
                <input
                  className="input mt-2 w-full max-w-md border-[var(--admin-border)] bg-[var(--admin-sand)] text-[var(--admin-blue)]"
                  onChange={(event) => renameCluster(cluster.id, event.target.value)}
                  value={cluster.name}
                />
              </div>

              <div className="min-w-72 rounded-2xl bg-[var(--admin-sand)] p-4">
                <div className="text-xs font-semibold uppercase tracking-[0.2em] text-[var(--admin-ink-soft)]">
                  CAO
                </div>
                <select
                  className="select select-bordered mt-3 w-full bg-white"
                  onChange={(event) =>
                    setClusterCao(
                      cluster.id,
                      event.target.value ? event.target.value : null
                    )
                  }
                  value={currentCao?.id ?? ''}
                >
                  <option value="">Select CAO</option>
                  {users
                    .filter((user) => user.active)
                    .map((user) => (
                      <option key={user.id} value={user.id}>
                        {user.name}
                      </option>
                    ))}
                </select>
                <p className="mt-2 text-xs text-[var(--admin-ink-muted)]">
                  {currentCao?.email || 'No CAO email assigned yet.'}
                </p>
              </div>
            </div>

            <div className="space-y-3">
              {cluster.departments.map((department) => {
                const chair =
                  users.find((user) => user.id === department.chairUserId) ?? null;
                const facultyCount = users.filter(
                  (user) =>
                    user.departmentId === department.id &&
                    ['fy', 'ay', 'nfa'].includes(user.designation)
                ).length;

                return (
                  <DepartmentRow
                    chairName={chair?.name ?? 'Assign chair'}
                    department={department}
                    facultyCount={facultyCount}
                    key={department.id}
                    onAssignChair={(userId) =>
                      setDepartmentChair(department.id, userId)
                    }
                    onOpenFaculty={() => setViewDepartmentId(department.id)}
                    onOpenSettings={() => setEditingDepartmentId(department.id)}
                    onRename={(name) => renameDepartment(department.id, name)}
                    users={users.filter(
                      (user) =>
                        user.active && user.departmentId === department.id
                    )}
                  />
                );
              })}
            </div>
          </section>
        );
      })}

      {unassignedDepartments.length > 0 ? (
        <section className="rounded-[1.25rem] border border-dashed border-[var(--admin-border)] bg-white p-6 shadow-sm">
          <h2 className="text-lg font-semibold text-[var(--admin-ink-muted)]">
            Unassigned to cluster
          </h2>
          <div className="mt-4 space-y-3">
            {unassignedDepartments.map((department) => (
              <DepartmentRow
                chairName={
                  users.find((user) => user.id === department.chairUserId)?.name ??
                  'Assign chair'
                }
                department={department}
                facultyCount={
                  users.filter(
                    (user) =>
                      user.departmentId === department.id &&
                      ['fy', 'ay', 'nfa'].includes(user.designation)
                  ).length
                }
                key={department.id}
                onAssignChair={(userId) => setDepartmentChair(department.id, userId)}
                onOpenFaculty={() => setViewDepartmentId(department.id)}
                onOpenSettings={() => setEditingDepartmentId(department.id)}
                onRename={(name) => renameDepartment(department.id, name)}
                users={users.filter(
                  (user) => user.active && user.departmentId === department.id
                )}
              />
            ))}
          </div>
        </section>
      ) : null}

      {editingDepartment ? (
        <DepartmentSettingsModal
          clusters={clusters}
          department={editingDepartment}
          onClose={() => setEditingDepartmentId(null)}
          onRemoveRoutingEmail={(emailId) =>
            removeRoutingEmail(editingDepartment.id, emailId)
          }
          onSave={(updates) => {
            updateDepartment(editingDepartment.id, updates);
            setEditingDepartmentId(null);
          }}
          onUpsertRoutingEmail={(email) =>
            upsertRoutingEmail(editingDepartment.id, email)
          }
        />
      ) : null}
    </div>
  );
}

function DepartmentRow({
  chairName,
  department,
  facultyCount,
  onAssignChair,
  onOpenFaculty,
  onOpenSettings,
  onRename,
  users,
}: {
  chairName: string;
  department: AdminDepartment;
  facultyCount: number;
  onAssignChair: (userId: string | null) => void;
  onOpenFaculty: () => void;
  onOpenSettings: () => void;
  onRename: (name: string) => void;
  users: Array<{ id: string; name: string }>;
}) {
  const approvalLabel =
    department.approvalMode === 'approval'
      ? 'Approval required'
      : department.approvalMode === 'auto'
        ? 'Auto-approve'
        : 'Notification only';
  const chairUser = users.find((user) => user.id === department.chairUserId) ?? null;

  return (
    <div className="rounded-2xl border border-[var(--admin-border)] bg-white px-5 py-4 shadow-sm transition hover:shadow-md">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
            <button
              className="text-left text-lg font-bold uppercase tracking-wide text-[var(--admin-blue)]"
              onClick={() => {
                const nextName = window.prompt(
                  'Rename department',
                  department.name
                );
                if (nextName?.trim()) {
                  onRename(nextName.trim());
                }
              }}
              type="button"
            >
              {department.name}
            </button>
            <button
              className="text-sm font-medium text-[var(--admin-blue)] underline decoration-[var(--admin-gold)] decoration-2 underline-offset-4"
              onClick={onOpenFaculty}
              type="button"
            >
              View faculty
            </button>
          </div>
          <div className="mt-1 font-mono text-sm text-[var(--admin-ink-muted)]">
            {department.code}
          </div>
          <div className="mt-2 text-sm text-[var(--admin-ink-muted)]">
            {facultyCount} faculty · {approvalLabel}
            {department.autoDebitEnabled ? ' · Auto-debit on' : ''}
            {department.routingEmails.length > 0
              ? ` · ${department.routingEmails.length} routing emails`
              : ' · No email configured'}
          </div>
        </div>

        <div className="flex flex-col items-start gap-3 lg:w-72 lg:items-end lg:text-right">
          <div className="text-xs font-semibold uppercase tracking-[0.2em] text-[var(--admin-ink-soft)]">
            Chair
          </div>
          <select
            className="select select-bordered w-full max-w-xs bg-white lg:max-w-none"
            onChange={(event) =>
              onAssignChair(event.target.value ? event.target.value : null)
            }
            value={chairUser?.id ?? ''}
          >
            <option value="">{chairName === 'Assign chair' ? chairName : 'Reassign chair'}</option>
            {users.map((user) => (
              <option key={user.id} value={user.id}>
                {user.name}
              </option>
            ))}
          </select>
          <button
            className="btn btn-outline w-full max-w-xs lg:max-w-none"
            onClick={onOpenSettings}
            type="button"
          >
            Settings
          </button>
        </div>
      </div>
    </div>
  );
}

function DepartmentSettingsModal({
  clusters,
  department,
  onClose,
  onRemoveRoutingEmail,
  onSave,
  onUpsertRoutingEmail,
}: {
  clusters: Array<{ id: string; name: string }>;
  department: AdminDepartment;
  onClose: () => void;
  onRemoveRoutingEmail: (emailId: string) => void;
  onSave: (
    updates: Partial<
      Pick<
        AdminDepartment,
        'approvalMode' | 'autoDebitEnabled' | 'clusterId' | 'dispositionRequired'
      >
    >
  ) => void;
  onUpsertRoutingEmail: (email: {
    address: string;
    id?: string;
    kind: 'to' | 'cc';
  }) => void;
}) {
  const [approvalMode, setApprovalMode] = useState(department.approvalMode);
  const [clusterId, setClusterId] = useState(department.clusterId ?? '');
  const [dispositionRequired, setDispositionRequired] = useState(
    department.dispositionRequired
  );
  const [autoDebitEnabled, setAutoDebitEnabled] = useState(
    department.autoDebitEnabled
  );
  const [newEmail, setNewEmail] = useState('');
  const [newEmailKind, setNewEmailKind] = useState<'to' | 'cc'>('to');

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/40 px-4 py-8">
      <div className="max-h-[90vh] w-full max-w-3xl overflow-y-auto rounded-[1.5rem] border border-[var(--admin-border)] bg-white p-6 shadow-2xl">
        <h2 className="text-xl font-semibold text-[var(--admin-blue)]">
          Department settings
        </h2>
        <p className="mt-2 text-sm text-[var(--admin-ink-muted)]">
          Matching the mockup, this is where approval rules, clustering, and
          AggieService routing would be maintained.
        </p>

        <div className="mt-6 grid gap-4 sm:grid-cols-2">
          <label className="form-control">
            <span className="label-text mb-2 text-sm font-medium text-[var(--admin-ink)]">
              Cluster
            </span>
            <select
              className="select select-bordered"
              onChange={(event) => setClusterId(event.target.value)}
              value={clusterId}
            >
              <option value="">No cluster</option>
              {clusters.map((cluster) => (
                <option key={cluster.id} value={cluster.id}>
                  {cluster.name}
                </option>
              ))}
            </select>
          </label>

          <label className="form-control">
            <span className="label-text mb-2 text-sm font-medium text-[var(--admin-ink)]">
              Approval mode
            </span>
            <select
              className="select select-bordered"
              onChange={(event) =>
                setApprovalMode(
                  event.target.value as 'approval' | 'auto' | 'notification'
                )
              }
              value={approvalMode}
            >
              <option value="notification">Notification only</option>
              <option value="approval">Approval required</option>
              <option value="auto">Auto-approve</option>
            </select>
          </label>
        </div>

        <div className="mt-5 space-y-3">
          <label className="flex items-center gap-3 text-sm text-[var(--admin-ink)]">
            <input
              checked={dispositionRequired}
              className="checkbox"
              onChange={(event) => setDispositionRequired(event.target.checked)}
              type="checkbox"
            />
            Require a work coverage plan
          </label>
          <label className="flex items-center gap-3 text-sm text-[var(--admin-ink)]">
            <input
              checked={autoDebitEnabled}
              className="checkbox"
              onChange={(event) => setAutoDebitEnabled(event.target.checked)}
              type="checkbox"
            />
            Allow auto-debit for this department
          </label>
        </div>

        <div className="mt-6 rounded-2xl bg-[var(--admin-sand)] p-5">
          <h3 className="text-sm font-semibold uppercase tracking-[0.2em] text-[var(--admin-gold-deep)]">
            AggieService routing
          </h3>
          <p className="mt-2 text-sm text-[var(--admin-ink-muted)]">
            The UI is live here, but the eventual source of truth will be the
            database rather than uploaded spreadsheets.
          </p>

          <div className="mt-4 space-y-3">
            {department.routingEmails.map((email) => (
              <div
                className="flex flex-col gap-3 rounded-xl border border-[var(--admin-border)] bg-white px-4 py-3 sm:flex-row sm:items-center"
                key={email.id}
              >
                <span className="badge border-0 bg-[var(--admin-sand)] text-[var(--admin-blue)]">
                  {email.kind.toUpperCase()}
                </span>
                <span className="flex-1 text-sm text-[var(--admin-ink)]">
                  {email.address}
                </span>
                <button
                  className="btn btn-ghost btn-sm text-rose-700"
                  onClick={() => onRemoveRoutingEmail(email.id)}
                  type="button"
                >
                  Remove
                </button>
              </div>
            ))}
          </div>

          <div className="mt-4 flex flex-col gap-3 sm:flex-row">
            <select
              className="select select-bordered bg-white sm:w-32"
              onChange={(event) =>
                setNewEmailKind(event.target.value as 'to' | 'cc')
              }
              value={newEmailKind}
            >
              <option value="to">TO</option>
              <option value="cc">CC</option>
            </select>
            <input
              className="input input-bordered flex-1 bg-white"
              onChange={(event) => setNewEmail(event.target.value)}
              placeholder="email@ucdavis.edu"
              type="email"
              value={newEmail}
            />
            <button
              className="btn btn-outline"
              disabled={!newEmail}
              onClick={() => {
                onUpsertRoutingEmail({
                  address: newEmail,
                  kind: newEmailKind,
                });
                setNewEmail('');
              }}
              type="button"
            >
              Add email
            </button>
          </div>
        </div>

        <div className="mt-6 flex justify-end gap-3">
          <button className="btn btn-ghost" onClick={onClose} type="button">
            Cancel
          </button>
          <button
            className="btn border-0 bg-[var(--admin-gold)] text-[var(--admin-blue)] hover:bg-[var(--admin-gold)]/85"
            onClick={() =>
              onSave({
                approvalMode,
                autoDebitEnabled,
                clusterId: clusterId || null,
                dispositionRequired,
              })
            }
            type="button"
          >
            Save preview changes
          </button>
        </div>
      </div>
    </div>
  );
}
