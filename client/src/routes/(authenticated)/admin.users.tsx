import { useState } from 'react';
import { createFileRoute } from '@tanstack/react-router';
import type { ColumnDef } from '@tanstack/react-table';
import { z } from 'zod';
import { HttpError } from '@/lib/api.ts';
import type { AdminUser } from '@/shared/admin/adminData.tsx';
import { useAdminData } from '@/shared/admin/adminData.tsx';
import { DataTable } from '@/shared/dataTable.tsx';
import { useAppForm } from '@/shared/forms/formContext.tsx';

export const Route = createFileRoute('/(authenticated)/admin/users')({
  component: AdminUsersRoute,
});

type UserRow = AdminUser & {
  departmentName: string;
};

const userFormSchema = z.object({
  email: z
    .string()
    .trim()
    .refine(
      (value) => value.length === 0 || z.email().safeParse(value).success,
      'Enter a valid email address.'
    ),
  employeeId: z
    .string()
    .trim()
    .refine(
      (value) => value.length === 0 || /^\d{8}$/.test(value),
      'Employee ID must be exactly 8 digits.'
    ),
  iamId: z
    .string()
    .trim()
    .min(1, 'IAM ID is required.')
    .max(10, 'IAM ID must be 10 characters or fewer.')
    .regex(
      /^[a-z][\w-]*$/i,
      'IAM ID must start with a letter and use only letters, numbers, underscores, or hyphens.'
    ),
  name: z.string().trim().min(1, 'Display name is required.'),
});

const editableUserFormSchema = userFormSchema.extend({
  active: z.boolean(),
});

type UserFormValues = z.infer<typeof userFormSchema>;

function AdminUsersRoute() {
  const { createUser, departments, readonlyReason, updateUser, users } =
    useAdminData();
  const [filterRole, setFilterRole] = useState('');
  const [filterDepartmentId, setFilterDepartmentId] = useState('');
  const [showExcluded, setShowExcluded] = useState(false);
  const [editingUserId, setEditingUserId] = useState<string | null>(null);
  const [showCreateModal, setShowCreateModal] = useState(false);

  const departmentNames = Object.fromEntries(
    departments.map((department) => [department.id, department.name])
  );

  const rows: UserRow[] = users
    .filter((user) => (showExcluded ? true : user.active))
    .filter((user) => (filterRole ? user.role === filterRole : true))
    .filter((user) =>
      filterDepartmentId ? user.departmentId === filterDepartmentId : true
    )
    .map((user) => ({
      ...user,
      departmentName: departmentNames[user.departmentId] ?? 'Not mapped',
    }));

  const activeUsers = users.filter((user) => user.active);
  const excludedCount = users.length - activeUsers.length;
  const missingEmailCount = activeUsers.filter((user) => !user.email.trim()).length;

  const columns: ColumnDef<UserRow>[] = [
    {
      accessorKey: 'name',
      cell: ({ row }) => (
        <div>
          <div className="font-semibold text-[var(--admin-ink)]">
            {row.original.name}
          </div>
          <div className="text-xs text-[var(--admin-ink-muted)]">
            {row.original.role === 'admin'
              ? 'Application administrator'
              : 'App user'}
          </div>
        </div>
      ),
      header: 'Name',
    },
    {
      accessorKey: 'email',
      cell: ({ row }) =>
        row.original.email ? (
          <span>{row.original.email}</span>
        ) : (
          <span className="italic text-rose-700">Missing</span>
        ),
      header: 'Email',
    },
    {
      accessorKey: 'employeeId',
      cell: ({ row }) => (
        <span className="font-mono text-xs">{row.original.employeeId}</span>
      ),
      header: 'Emp ID',
    },
    {
      accessorKey: 'departmentName',
      header: 'Department',
    },
    {
      accessorKey: 'role',
      cell: ({ row }) => (
        <span className="inline-flex rounded-full bg-[var(--admin-sand)] px-3 py-1 text-xs font-semibold text-[var(--admin-blue)]">
          {row.original.role === 'admin' ? 'Admin' : 'Faculty'}
        </span>
      ),
      header: 'Role',
    },
    {
      accessorKey: 'active',
      cell: ({ row }) => (
        <UserStatusToggle
          active={row.original.active}
          onToggle={() =>
            updateUser(row.original.id, { active: !row.original.active })
          }
        />
      ),
      header: 'Status',
    },
    {
      cell: ({ row }) => (
        <button
          className="btn btn-ghost btn-sm"
          onClick={() => setEditingUserId(row.original.id)}
          type="button"
        >
          Edit
        </button>
      ),
      header: 'Actions',
      id: 'actions',
    },
  ];

  const editingUser =
    editingUserId === null
      ? null
      : users.find((user) => user.id === editingUserId) ?? null;

  return (
    <div className="space-y-6">
      <section className="grid gap-4 md:grid-cols-3">
        <SummaryCard
          label="Database roster"
          text={`${activeUsers.length} active people are currently loaded from AppUser.`}
          value={String(activeUsers.length)}
        />
        <SummaryCard
          accent="text-rose-700"
          label="Missing emails"
          text="Useful for checking directory and onboarding completeness."
          value={String(missingEmailCount)}
        />
        <SummaryCard
          accent="text-slate-700"
          label="Excluded users"
          text="Backed by the persisted AppUser.IsActive flag."
          value={String(excludedCount)}
        />
      </section>

      <section className="rounded-[1.25rem] border border-[var(--admin-border)] bg-white p-6 shadow-sm">
        <div className="mb-5 flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <h2 className="text-lg font-semibold text-[var(--admin-blue)]">
              User management
            </h2>
            <p className="mt-2 text-sm text-[var(--admin-ink-muted)]">
              This table is now sourced from the database. Department values are
              inferred from the user&apos;s latest leave request snapshot.
            </p>
            <p className="mt-2 text-sm text-[var(--admin-ink-muted)]">
              {readonlyReason}
            </p>
          </div>
          <button
            className="btn border-0 bg-[var(--admin-gold)] text-[var(--admin-blue)] hover:bg-[var(--admin-gold)]/85"
            onClick={() => setShowCreateModal(true)}
            type="button"
          >
            Add user
          </button>
        </div>

        <DataTable
          columns={columns}
          data={rows}
          filterPlaceholder="Search name, email, IAM ID, or department..."
          globalFilter="left"
          initialState={{
            pagination: {
              pageSize: 8,
            },
          }}
          tableActions={
            <div className="flex flex-col gap-3 sm:flex-row sm:flex-wrap sm:items-center">
              <select
                className="select select-bordered"
                onChange={(event) => setFilterRole(event.target.value)}
                value={filterRole}
              >
                <option value="">All roles</option>
                <option value="faculty">Faculty</option>
                <option value="admin">Admin</option>
              </select>

              <select
                className="select select-bordered"
                onChange={(event) => setFilterDepartmentId(event.target.value)}
                value={filterDepartmentId}
              >
                <option value="">All departments</option>
                {departments.map((department) => (
                  <option key={department.id} value={department.id}>
                    {department.name}
                  </option>
                ))}
              </select>

              <label className="label cursor-pointer gap-3 rounded-xl border border-[var(--admin-border)] px-4 py-2">
                <span className="label-text text-sm text-[var(--admin-ink)]">
                  Show excluded
                </span>
                <input
                  checked={showExcluded}
                  className="toggle toggle-sm"
                  onChange={(event) => setShowExcluded(event.target.checked)}
                  type="checkbox"
                />
              </label>
            </div>
          }
        />
      </section>

      {editingUser ? (
        <UserEditorModal
          onClose={() => setEditingUserId(null)}
          onSave={(updates) =>
            updateUser(editingUser.id, updates).then(() => {
              setEditingUserId(null);
            })
          }
          user={editingUser}
        />
      ) : null}

      {showCreateModal ? (
        <CreateUserModal
          onClose={() => setShowCreateModal(false)}
          onCreate={async (payload) => {
            await createUser(payload);
            setShowCreateModal(false);
          }}
        />
      ) : null}
    </div>
  );
}

function SummaryCard({
  accent,
  label,
  text,
  value,
}: {
  accent?: string;
  label: string;
  text: string;
  value: string;
}) {
  return (
    <section className="rounded-[1.25rem] border border-[var(--admin-border)] bg-white p-5 shadow-sm">
      <div className="text-xs font-semibold uppercase tracking-[0.24em] text-[var(--admin-gold-deep)]">
        {label}
      </div>
      <div className={`mt-3 text-3xl font-bold ${accent ?? 'text-[var(--admin-blue)]'}`}>
        {value}
      </div>
      <p className="mt-2 text-sm text-[var(--admin-ink-muted)]">{text}</p>
    </section>
  );
}

function ModalFrame({
  children,
  title,
}: {
  children: React.ReactNode;
  title: string;
}) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/40 px-4 py-8">
      <div className="max-h-[90vh] w-full max-w-2xl overflow-y-auto rounded-[1.5rem] border border-[var(--admin-border)] bg-white p-6 shadow-2xl">
        <div className="mb-6">
          <h2 className="text-xl font-semibold text-[var(--admin-blue)]">
            {title}
          </h2>
          <p className="mt-2 text-sm text-[var(--admin-ink-muted)]">
            These edits now persist to the AppUser table.
          </p>
        </div>
        {children}
      </div>
    </div>
  );
}

function UserStatusToggle({
  active,
  onToggle,
}: {
  active: boolean;
  onToggle: () => Promise<void>;
}) {
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const handleToggle = async () => {
    setIsSaving(true);
    setError(null);

    try {
      await onToggle();
    } catch (toggleError) {
      setError(getUserUpdateErrorMessage(toggleError));
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="flex flex-col items-start gap-2">
      <button
        className={`badge border-0 px-3 py-3 text-xs font-semibold ${
          active ? 'bg-emerald-100 text-emerald-800' : 'bg-slate-200 text-slate-700'
        }`}
        disabled={isSaving}
        onClick={() => {
          void handleToggle();
        }}
        type="button"
      >
        {isSaving ? 'Saving...' : active ? 'Included' : 'Excluded'}
      </button>
      {error ? <span className="text-xs text-rose-700">{error}</span> : null}
    </div>
  );
}

function UserEditorModal({
  onClose,
  onSave,
  user,
}: {
  onClose: () => void;
  onSave: (
    updates: Partial<
      Pick<AdminUser, 'active' | 'email' | 'employeeId' | 'iamId' | 'name'>
    >
  ) => Promise<void>;
  user: AdminUser;
}) {
  const [submitError, setSubmitError] = useState<string | null>(null);
  const form = useAppForm({
    defaultValues: {
      active: user.active,
      email: user.email,
      employeeId: user.employeeId,
      iamId: user.iamId,
      name: user.name,
    },
    onSubmit: async ({ value }) => {
      setSubmitError(null);

      try {
        await onSave({
          active: value.active,
          email: value.email.trim(),
          employeeId: value.employeeId.trim(),
          iamId: value.iamId.trim(),
          name: value.name.trim(),
        });
      } catch (error) {
        setSubmitError(getUserUpdateErrorMessage(error));
      }
    },
    validators: {
      onChange: editableUserFormSchema,
    },
  });

  return (
    <ModalFrame title={`Edit ${user.name}`}>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void form.handleSubmit();
        }}
      >
        <form.AppForm>
          <div className="grid gap-4 sm:grid-cols-2">
            <form.AppField name="name">
              {(field) => <field.TextField label="Display name" />}
            </form.AppField>
            <form.AppField name="email">
              {(field) => <field.TextField label="Email" type="email" />}
            </form.AppField>
            <form.AppField name="employeeId">
              {(field) => <field.TextField label="Employee ID" />}
            </form.AppField>
            <form.AppField name="iamId">
              {(field) => (
                <field.TextField
                  label="IAM ID"
                />
              )}
            </form.AppField>
          </div>

          <div className="mt-5">
            <form.AppField name="active">
              {(field) => (
                <field.CheckboxField label="Include this person in the admin roster" />
              )}
            </form.AppField>
          </div>

          {submitError ? (
            <div className="mt-4 rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">
              {submitError}
            </div>
          ) : null}

          <div className="mt-6 flex justify-end gap-3">
            <button className="btn btn-ghost" onClick={onClose} type="button">
              Cancel
            </button>
            <form.SubscribeButton
              className="btn border-0 bg-[var(--admin-gold)] text-[var(--admin-blue)] hover:bg-[var(--admin-gold)]/85"
              label="Save changes"
              loadingLabel="Saving..."
            />
          </div>
        </form.AppForm>
      </form>
    </ModalFrame>
  );
}

function CreateUserModal({
  onClose,
  onCreate,
}: {
  onClose: () => void;
  onCreate: (payload: {
    email: string;
    employeeId: string;
    iamId: string;
    name: string;
  }) => Promise<void>;
}) {
  const [submitError, setSubmitError] = useState<string | null>(null);
  const form = useAppForm({
    defaultValues: {
      email: '',
      employeeId: '',
      iamId: '',
      name: '',
    } satisfies UserFormValues,
    onSubmit: async ({ value }) => {
      setSubmitError(null);

      try {
        await onCreate({
          email: value.email.trim(),
          employeeId: value.employeeId.trim(),
          iamId: value.iamId.trim(),
          name: value.name.trim(),
        });
      } catch (error) {
        setSubmitError(getUserCreateErrorMessage(error));
      }
    },
    validators: {
      onChange: userFormSchema,
    },
  });

  return (
    <ModalFrame title="Add user">
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void form.handleSubmit();
        }}
      >
        <form.AppForm>
          <div className="grid gap-4 sm:grid-cols-2">
            <form.AppField name="name">
              {(field) => <field.TextField label="Display name" />}
            </form.AppField>
            <form.AppField name="email">
              {(field) => <field.TextField label="Email" type="email" />}
            </form.AppField>
            <form.AppField name="employeeId">
              {(field) => <field.TextField label="Employee ID" />}
            </form.AppField>
            <form.AppField name="iamId">
              {(field) => (
                <field.TextField
                  label="IAM ID"
                />
              )}
            </form.AppField>
          </div>

          {submitError ? (
            <div className="mt-4 rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">
              {submitError}
            </div>
          ) : null}

          <div className="mt-6 flex justify-end gap-3">
            <button className="btn btn-ghost" onClick={onClose} type="button">
              Cancel
            </button>
            <form.SubscribeButton
              className="btn border-0 bg-[var(--admin-gold)] text-[var(--admin-blue)] hover:bg-[var(--admin-gold)]/85"
              label="Create user"
              loadingLabel="Creating..."
            />
          </div>
        </form.AppForm>
      </form>
    </ModalFrame>
  );
}

function getUserCreateErrorMessage(error: unknown) {
  return getUserMutationErrorMessage(
    error,
    'Unable to create the user. Please review the fields and try again.'
  );
}

function getUserUpdateErrorMessage(error: unknown) {
  return getUserMutationErrorMessage(
    error,
    'Unable to save the user. Please review the fields and try again.'
  );
}

function getUserMutationErrorMessage(error: unknown, fallbackMessage: string) {
  if (error instanceof HttpError) {
    if (typeof error.body === 'string' && error.body.trim()) {
      return error.body;
    }

    if (error.body && typeof error.body === 'object') {
      const body = error.body as {
        detail?: string;
        errors?: Record<string, string[]>;
        title?: string;
      };

      const validationMessage = body.errors
        ? Object.values(body.errors)
            .flat()
            .find(Boolean)
        : null;

      if (validationMessage) {
        return validationMessage;
      }

      if (body.detail) {
        return body.detail;
      }

      if (body.title) {
        return body.title;
      }
    }

    if (error.status === 409) {
      return 'A user with that IAM ID, employee ID, or identity already exists.';
    }

    return fallbackMessage;
  }

  if (error instanceof Error && error.message) {
    return error.message;
  }

  return fallbackMessage;
}
