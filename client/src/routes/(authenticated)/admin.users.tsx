import { useState } from 'react';
import { createFileRoute } from '@tanstack/react-router';
import type { ColumnDef } from '@tanstack/react-table';
import { z } from 'zod';
import { HttpError } from '@/lib/api.ts';
import type { AdminUser } from '@/shared/admin/adminData.tsx';
import { useAdminData } from '@/shared/admin/adminData.tsx';
import { DataTable } from '@/shared/dataTable.tsx';

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
      /^[a-z][a-z0-9_-]*$/i,
      'IAM ID must start with a letter and use only letters, numbers, underscores, or hyphens.'
    ),
  name: z.string().trim().min(1, 'Display name is required.'),
});

type UserFormValues = z.infer<typeof userFormSchema>;
type UserFormField = keyof UserFormValues;
type UserFormErrors = Partial<Record<UserFormField, string>>;

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
        <button
          className={`badge border-0 px-3 py-3 text-xs font-semibold ${
            row.original.active
              ? 'bg-emerald-100 text-emerald-800'
              : 'bg-slate-200 text-slate-700'
          }`}
          onClick={() =>
            void updateUser(row.original.id, { active: !row.original.active })
          }
          type="button"
        >
          {row.original.active ? 'Included' : 'Excluded'}
        </button>
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
          onSave={(updates) => {
            void updateUser(editingUser.id, updates);
            setEditingUserId(null);
          }}
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
  ) => void;
  user: AdminUser;
}) {
  const [name, setName] = useState(user.name);
  const [email, setEmail] = useState(user.email);
  const [employeeId, setEmployeeId] = useState(user.employeeId);
  const [iamId, setIamId] = useState(user.iamId);
  const [active, setActive] = useState(user.active);

  return (
    <ModalFrame title={`Edit ${user.name}`}>
      <div className="grid gap-4 sm:grid-cols-2">
        <FormField label="Display name" onChange={setName} value={name} />
        <FormField label="Email" onChange={setEmail} value={email} />
        <FormField
          label="Employee ID"
          onChange={setEmployeeId}
          value={employeeId}
        />
        <FormField label="IAM ID" onChange={setIamId} value={iamId} />
      </div>

      <label className="mt-5 flex items-center gap-3 text-sm text-[var(--admin-ink)]">
        <input
          checked={active}
          className="checkbox"
          onChange={(event) => setActive(event.target.checked)}
          type="checkbox"
        />
        Include this person in the admin roster
      </label>

      <div className="mt-6 flex justify-end gap-3">
        <button className="btn btn-ghost" onClick={onClose} type="button">
          Cancel
        </button>
        <button
          className="btn border-0 bg-[var(--admin-gold)] text-[var(--admin-blue)] hover:bg-[var(--admin-gold)]/85"
          onClick={() =>
            onSave({
              active,
              email,
              employeeId,
              iamId,
              name,
            })
          }
          type="button"
        >
          Save changes
        </button>
      </div>
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
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [employeeId, setEmployeeId] = useState('');
  const [iamId, setIamId] = useState('');
  const [fieldErrors, setFieldErrors] = useState<UserFormErrors>({});
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  function clearFieldError(field: UserFormField) {
    setFieldErrors((current) => {
      if (!current[field]) {
        return current;
      }

      return {
        ...current,
        [field]: undefined,
      };
    });
  }

  async function handleSubmit() {
    setSubmitError(null);

    const formValues: UserFormValues = {
      email,
      employeeId,
      iamId,
      name,
    };

    const result = userFormSchema.safeParse(formValues);

    if (!result.success) {
      const nextFieldErrors: UserFormErrors = {};
      for (const issue of result.error.issues) {
        const field = issue.path[0];
        if (typeof field === 'string' && !nextFieldErrors[field as UserFormField]) {
          nextFieldErrors[field as UserFormField] = issue.message;
        }
      }

      setFieldErrors(nextFieldErrors);
      return;
    }

    setFieldErrors({});
    setIsSubmitting(true);

    try {
      await onCreate({
        email: result.data.email.trim(),
        employeeId: result.data.employeeId.trim(),
        iamId: result.data.iamId.trim(),
        name: result.data.name.trim(),
      });
    } catch (error) {
      setSubmitError(getUserCreateErrorMessage(error));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <ModalFrame title="Add user">
      <div className="grid gap-4 sm:grid-cols-2">
        <FormField
          error={fieldErrors.name}
          label="Display name"
          onChange={(value) => {
            setName(value);
            clearFieldError('name');
          }}
          value={name}
        />
        <FormField
          error={fieldErrors.email}
          label="Email"
          onChange={(value) => {
            setEmail(value);
            clearFieldError('email');
          }}
          type="email"
          value={email}
        />
        <FormField
          error={fieldErrors.employeeId}
          label="Employee ID"
          onChange={(value) => {
            setEmployeeId(value);
            clearFieldError('employeeId');
          }}
          value={employeeId}
        />
        <FormField
          error={fieldErrors.iamId}
          helperText="Use the campus IAM ID / Kerberos-style username."
          label="IAM ID"
          onChange={(value) => {
            setIamId(value);
            clearFieldError('iamId');
          }}
          value={iamId}
        />
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
        <button
          className="btn border-0 bg-[var(--admin-gold)] text-[var(--admin-blue)] hover:bg-[var(--admin-gold)]/85"
          disabled={isSubmitting}
          onClick={() => void handleSubmit()}
          type="button"
        >
          {isSubmitting ? 'Creating...' : 'Create user'}
        </button>
      </div>
    </ModalFrame>
  );
}

function getUserCreateErrorMessage(error: unknown) {
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

    return 'Unable to create the user. Please review the fields and try again.';
  }

  if (error instanceof Error && error.message) {
    return error.message;
  }

  return 'Unable to create the user. Please review the fields and try again.';
}

function FormField({
  error,
  helperText,
  label,
  onChange,
  type,
  value,
}: {
  error?: string;
  helperText?: string;
  label: string;
  onChange: (value: string) => void;
  type?: 'email' | 'text';
  value: string;
}) {
  return (
    <label className="form-control w-full">
      <span className="label-text mb-2 text-sm font-medium text-[var(--admin-ink)]">
        {label}
      </span>
      <input
        className={`input input-bordered w-full ${error ? 'border-rose-400 focus:border-rose-500' : ''}`}
        onChange={(event) => onChange(event.target.value)}
        type={type ?? 'text'}
        value={value}
      />
      {error ? (
        <span className="mt-2 text-sm text-rose-700">{error}</span>
      ) : helperText ? (
        <span className="mt-2 text-sm text-[var(--admin-ink-muted)]">
          {helperText}
        </span>
      ) : null}
    </label>
  );
}
