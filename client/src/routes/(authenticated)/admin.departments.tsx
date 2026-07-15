import { useState } from 'react';
import { createFileRoute } from '@tanstack/react-router';
import { z } from 'zod';
import {
  type AdminDepartment,
  useAdminData,
} from '@/shared/admin/adminData.tsx';
import { HttpError } from '@/lib/api.ts';
import { useAppForm } from '@/shared/forms/formContext.tsx';

export const Route = createFileRoute('/(authenticated)/admin/departments')({
  component: AdminDepartmentsRoute,
});

const departmentNameSchema = z.object({
  name: z.string().trim().min(1, 'Department name is required.'),
});

const departmentSettingsSchema = z.object({
  approvalMode: z.enum(['notification', 'approval', 'auto']),
  clusterId: z.string(),
});

const routingEmailSchema = z.object({
  address: z.email('Enter a valid email address.'),
  kind: z.enum(['to', 'cc']),
});

type RoutingEmailFormValues = z.infer<typeof routingEmailSchema>;

const defaultRoutingEmailValues: RoutingEmailFormValues = {
  address: '',
  kind: 'to',
};

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

function NameEditor({
  inputClassName,
  name,
  onSave,
  savingMessage,
  wrapperClassName,
}: {
  inputClassName: string;
  name: string;
  onSave: (name: string) => Promise<void>;
  savingMessage: string;
  wrapperClassName?: string;
}) {
  const [error, setError] = useState<string | null>(null);
  const form = useAppForm({
    defaultValues: {
      name,
    },
    onSubmit: async ({ value }) => {
      const nextName = value.name.trim();

      setError(null);

      if (nextName === name) {
        return;
      }

      try {
        await onSave(nextName);
      } catch (mutationError) {
        setError(getAdminMutationErrorMessage(mutationError));
      }
    },
    validators: {
      onChange: departmentNameSchema,
    },
  });

  return (
    <form.AppForm>
      <form.AppField name="name">
        {(field) => {
          const hasError =
            field.state.meta.isTouched && !field.state.meta.isValid;

          return (
            <div className={wrapperClassName}>
              <input
                className={`${inputClassName} ${
                  hasError || error
                    ? 'border-rose-400 focus:border-rose-500'
                    : ''
                }`}
                disabled={field.form.state.isSubmitting}
                onBlur={(event) => {
                  field.handleBlur();

                  if (event.target.value.trim()) {
                    void form.handleSubmit();
                  }
                }}
                onChange={(event) => {
                  setError(null);
                  field.handleChange(event.target.value);
                }}
                value={field.state.value}
              />
              {error ? (
                <p className="mt-2 text-sm text-rose-700">{error}</p>
              ) : hasError ? (
                <p className="mt-2 text-sm text-rose-700">
                  {field.state.meta.errors
                    .flatMap((issue) => (issue?.message ? [issue.message] : []))
                    .join(', ')}
                </p>
              ) : field.form.state.isSubmitting ? (
                <p className="mt-2 text-sm text-[var(--admin-ink-muted)]">
                  {savingMessage}
                </p>
              ) : null}
            </div>
          );
        }}
      </form.AppField>
    </form.AppForm>
  );
}

function DepartmentRow({
  department,
  linkedUserCount,
  onOpenRoster,
  onOpenSettings,
  onRename,
}: {
  department: AdminDepartment;
  linkedUserCount: number;
  onOpenRoster: () => void;
  onOpenSettings: () => void;
  onRename: (name: string) => Promise<void>;
}) {
  const approvalLabel =
    department.approvalMode === 'approval'
      ? 'Approval required'
      : department.approvalMode === 'auto'
        ? 'Auto-approve'
        : 'Notification only';

  return (
    <div className="rounded-2xl border border-[var(--admin-border)] bg-white px-5 py-4 shadow-sm transition hover:shadow-md">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
            <NameEditor
              inputClassName="input input-ghost h-auto min-h-0 w-full max-w-md justify-start px-0 text-lg font-bold uppercase tracking-wide text-[var(--admin-blue)] shadow-none focus:bg-transparent"
              key={`${department.id}:${department.name}`}
              name={department.name}
              onSave={onRename}
              savingMessage="Saving department name..."
              wrapperClassName="w-full max-w-md"
            />
            <button
              className="text-sm font-medium text-[var(--admin-blue)] underline decoration-[var(--admin-gold)] decoration-2 underline-offset-4"
              onClick={onOpenRoster}
              type="button"
            >
              View linked users
            </button>
          </div>
          <div className="mt-1 font-mono text-sm text-[var(--admin-ink-muted)]">
            {department.code}
          </div>
          <div className="mt-2 text-sm text-[var(--admin-ink-muted)]">
            {linkedUserCount} active users · {approvalLabel}
            {department.routingEmails.length > 0
              ? ` · ${department.routingEmails.length} routing emails`
              : ' · No email configured'}
          </div>
        </div>

        <div className="flex flex-col items-start gap-3 lg:w-72 lg:items-end lg:text-right">
          <div className="text-xs font-semibold uppercase tracking-[0.2em] text-[var(--admin-ink-soft)]">
            Database-backed settings
          </div>
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
  onRemoveRoutingEmail: (emailId: string) => Promise<void>;
  onSave: (
    updates: Partial<Pick<AdminDepartment, 'approvalMode' | 'clusterId'>>
  ) => Promise<void>;
  onUpsertRoutingEmail: (email: {
    address: string;
    id?: string;
    kind: 'to' | 'cc';
  }) => Promise<void>;
}) {
  const [saveError, setSaveError] = useState<string | null>(null);
  const [routingEmailError, setRoutingEmailError] = useState<string | null>(
    null
  );
  const [pendingRemovalEmailId, setPendingRemovalEmailId] = useState<
    string | null
  >(null);
  const settingsForm = useAppForm({
    defaultValues: {
      approvalMode: department.approvalMode,
      clusterId: department.clusterId ?? '',
    },
    onSubmit: async ({ value }) => {
      setSaveError(null);

      try {
        await onSave({
          approvalMode: value.approvalMode,
          clusterId: value.clusterId || null,
        });
      } catch (mutationError) {
        setSaveError(getAdminMutationErrorMessage(mutationError));
      }
    },
    validators: {
      onChange: departmentSettingsSchema,
    },
  });
  const routingEmailForm = useAppForm({
    defaultValues: defaultRoutingEmailValues,
    onSubmit: async ({ value }) => {
      setRoutingEmailError(null);

      try {
        await onUpsertRoutingEmail({
          address: value.address.trim(),
          kind: value.kind,
        });
        routingEmailForm.reset();
      } catch (mutationError) {
        setRoutingEmailError(getAdminMutationErrorMessage(mutationError));
      }
    },
    validators: {
      onChange: routingEmailSchema,
    },
  });

  const handleRemoveEmail = async (emailId: string) => {
    setPendingRemovalEmailId(emailId);
    setRoutingEmailError(null);

    try {
      await onRemoveRoutingEmail(emailId);
    } catch (mutationError) {
      setRoutingEmailError(getAdminMutationErrorMessage(mutationError));
    } finally {
      setPendingRemovalEmailId(null);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/40 px-4 py-8">
      <div className="max-h-[90vh] w-full max-w-3xl overflow-y-auto rounded-[1.5rem] border border-[var(--admin-border)] bg-white p-6 shadow-2xl">
        <h2 className="text-xl font-semibold text-[var(--admin-blue)]">
          Department settings
        </h2>
        <p className="mt-2 text-sm text-[var(--admin-ink-muted)]">
          These controls persist to the database for cluster placement, workflow
          mode, and routing emails.
        </p>

        <form
          onSubmit={(event) => {
            event.preventDefault();
            void settingsForm.handleSubmit();
          }}
        >
          <settingsForm.AppForm>
            <div className="mt-6 grid gap-4 sm:grid-cols-2">
              <settingsForm.AppField name="clusterId">
                {(field) => (
                  <field.SelectField
                    label="Cluster"
                    options={clusters.map((cluster) => ({
                      label: cluster.name,
                      value: cluster.id,
                    }))}
                    placeholder="No cluster"
                  />
                )}
              </settingsForm.AppField>

              <settingsForm.AppField name="approvalMode">
                {(field) => (
                  <field.SelectField
                    label="Approval mode"
                    options={[
                      {
                        label: 'Notification only',
                        value: 'notification',
                      },
                      {
                        label: 'Approval required',
                        value: 'approval',
                      },
                      {
                        label: 'Auto-approve',
                        value: 'auto',
                      },
                    ]}
                  />
                )}
              </settingsForm.AppField>
            </div>

            <div className="mt-6 rounded-2xl bg-[var(--admin-sand)] p-5">
              <h3 className="text-sm font-semibold uppercase tracking-[0.2em] text-[var(--admin-gold-deep)]">
                Routing emails
              </h3>
              <p className="mt-2 text-sm text-[var(--admin-ink-muted)]">
                Routing emails are now stored in `DepartmentEmailRouting`.
              </p>

              <div className="mt-4 space-y-3">
                {department.routingEmails.map((email) => (
                  <div
                    className="flex flex-col gap-3 rounded-xl border border-[var(--admin-border)] bg-white px-4 py-3 sm:flex-row sm:items-center"
                    key={email.id}
                  >
                    <span className="badge border-0 bg-[var(--admin-sand)] text-[var(--admin-blue)]">
                      EMAIL
                    </span>
                    <span className="flex-1 text-sm text-[var(--admin-ink)]">
                      {email.address}
                    </span>
                    <button
                      className="btn btn-ghost btn-sm text-rose-700"
                      disabled={pendingRemovalEmailId === email.id}
                      onClick={() => {
                        void handleRemoveEmail(email.id);
                      }}
                      type="button"
                    >
                      {pendingRemovalEmailId === email.id ? (
                        <>
                          <span className="loading loading-spinner loading-xs mr-2"></span>
                          Removing...
                        </>
                      ) : (
                        'Remove'
                      )}
                    </button>
                  </div>
                ))}
              </div>

              {routingEmailError ? (
                <div className="mt-4 rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">
                  {routingEmailError}
                </div>
              ) : null}

              <routingEmailForm.AppForm>
                <div className="mt-4 flex flex-col gap-3 sm:flex-row sm:items-start">
                  <div className="flex-1">
                    <routingEmailForm.AppField name="address">
                      {(field) => (
                        <field.TextField
                          inputClassName="input input-bordered w-full bg-white"
                          label="Add routing email"
                          placeholder="email@ucdavis.edu"
                          type="email"
                        />
                      )}
                    </routingEmailForm.AppField>
                  </div>
                  <routingEmailForm.SubscribeButton
                    className="btn btn-outline sm:mt-8"
                    label="Add email"
                    loadingLabel="Adding..."
                    onClick={() => {
                      void routingEmailForm.handleSubmit();
                    }}
                    type="button"
                  />
                </div>
              </routingEmailForm.AppForm>
            </div>

            {saveError ? (
              <div className="mt-6 rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">
                {saveError}
              </div>
            ) : null}

            <div className="mt-6 flex justify-end gap-3">
              <button className="btn btn-ghost" onClick={onClose} type="button">
                Cancel
              </button>
              <settingsForm.SubscribeButton
                className="btn border-0 bg-[var(--admin-gold)] text-[var(--admin-blue)] hover:bg-[var(--admin-gold)]/85"
                label="Save changes"
                loadingLabel="Saving..."
              />
            </div>
          </settingsForm.AppForm>
        </form>
      </div>
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
