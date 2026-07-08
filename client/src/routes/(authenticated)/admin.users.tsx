import { useState } from 'react';
import { createFileRoute } from '@tanstack/react-router';
import type { ColumnDef } from '@tanstack/react-table';
import {
  getDesignationLabel,
  type AdminDesignation,
  type AdminUser,
  useAdminData,
} from '@/shared/admin/adminData.tsx';
import { DataTable } from '@/shared/dataTable.tsx';

export const Route = createFileRoute('/(authenticated)/admin/users')({
  component: AdminUsersRoute,
});

type UserRow = AdminUser & {
  departmentName: string;
};

function AdminUsersRoute() {
  const { createUser, departments, updateUser, users } = useAdminData();
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
      departmentName: departmentNames[user.departmentId] ?? user.departmentId,
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
            {row.original.position}
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
      accessorKey: 'designation',
      cell: ({ row }) => (
        <span className="inline-flex rounded-full bg-[var(--admin-sand)] px-3 py-1 text-xs font-semibold text-[var(--admin-blue)]">
          {getDesignationLabel(row.original.designation)}
        </span>
      ),
      header: 'Designation',
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
            updateUser(row.original.id, { active: !row.original.active })
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
          label="Roster preview"
          text={`${activeUsers.length} active people shown in the in-memory roster.`}
          value={String(activeUsers.length)}
        />
        <SummaryCard
          accent="text-rose-700"
          label="Missing emails"
          text="Useful for validating the eventual directory sync."
          value={String(missingEmailCount)}
        />
        <SummaryCard
          accent="text-slate-700"
          label="Excluded users"
          text="Matches the mockup include/exclude review flow."
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
              This mirrors the mockup’s user grid and edit flow, but edits stay
              in browser memory for now.
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
                <option value="chair">Chair</option>
                <option value="cao">CAO</option>
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
          departments={departments}
          onClose={() => setEditingUserId(null)}
          onSave={(updates) => {
            updateUser(editingUser.id, updates);
            setEditingUserId(null);
          }}
          user={editingUser}
        />
      ) : null}

      {showCreateModal ? (
        <CreateUserModal
          departments={departments}
          onClose={() => setShowCreateModal(false)}
          onCreate={(payload) => {
            createUser(payload);
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
            Changes stay in local preview state until the real admin API exists.
          </p>
        </div>
        {children}
      </div>
    </div>
  );
}

function UserEditorModal({
  departments,
  onClose,
  onSave,
  user,
}: {
  departments: Array<{ id: string; name: string }>;
  onClose: () => void;
  onSave: (
    updates: Partial<
      Pick<
        AdminUser,
        | 'active'
        | 'departmentId'
        | 'designation'
        | 'email'
        | 'employeeId'
        | 'iamId'
        | 'name'
        | 'position'
      >
    >
  ) => void;
  user: AdminUser;
}) {
  const [name, setName] = useState(user.name);
  const [email, setEmail] = useState(user.email);
  const [employeeId, setEmployeeId] = useState(user.employeeId);
  const [iamId, setIamId] = useState(user.iamId);
  const [departmentId, setDepartmentId] = useState(user.departmentId);
  const [designation, setDesignation] = useState<AdminDesignation>(
    user.designation
  );
  const [position, setPosition] = useState(user.position);
  const [active, setActive] = useState(user.active);

  return (
    <ModalFrame title={`Edit ${user.name}`}>
      <div className="grid gap-4 sm:grid-cols-2">
        <FormField
          label="Display name"
          onChange={setName}
          value={name}
        />
        <FormField label="Email" onChange={setEmail} value={email} />
        <FormField
          label="Employee ID"
          onChange={setEmployeeId}
          value={employeeId}
        />
        <FormField label="IAM ID" onChange={setIamId} value={iamId} />
        <SelectField
          label="Department"
          onChange={setDepartmentId}
          options={departments.map((department) => ({
            label: department.name,
            value: department.id,
          }))}
          value={departmentId}
        />
        <SelectField
          label="Designation"
          onChange={(value) => setDesignation(value as AdminDesignation)}
          options={[
            { label: 'FY Faculty', value: 'fy' },
            { label: 'AY Faculty', value: 'ay' },
            { label: 'Non-Faculty Academic', value: 'nfa' },
            { label: 'Chair', value: 'chair' },
            { label: 'CAO', value: 'cao' },
            { label: 'Admin', value: 'admin' },
          ]}
          value={designation}
        />
        <div className="sm:col-span-2">
          <FormField label="Position" onChange={setPosition} value={position} />
        </div>
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
              departmentId,
              designation,
              email,
              employeeId,
              iamId,
              name,
              position,
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
  departments,
  onClose,
  onCreate,
}: {
  departments: Array<{ id: string; name: string }>;
  onClose: () => void;
  onCreate: (payload: {
    departmentId: string;
    designation: AdminDesignation;
    email: string;
    employeeId: string;
    iamId: string;
    name: string;
    position: string;
  }) => void;
}) {
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [employeeId, setEmployeeId] = useState('');
  const [iamId, setIamId] = useState('');
  const [departmentId, setDepartmentId] = useState(departments[0]?.id ?? '');
  const [designation, setDesignation] = useState<AdminDesignation>('fy');
  const [position, setPosition] = useState('');

  return (
    <ModalFrame title="Add user">
      <div className="grid gap-4 sm:grid-cols-2">
        <FormField label="Display name" onChange={setName} value={name} />
        <FormField label="Email" onChange={setEmail} value={email} />
        <FormField
          label="Employee ID"
          onChange={setEmployeeId}
          value={employeeId}
        />
        <FormField label="IAM ID" onChange={setIamId} value={iamId} />
        <SelectField
          label="Department"
          onChange={setDepartmentId}
          options={departments.map((department) => ({
            label: department.name,
            value: department.id,
          }))}
          value={departmentId}
        />
        <SelectField
          label="Designation"
          onChange={(value) => setDesignation(value as AdminDesignation)}
          options={[
            { label: 'FY Faculty', value: 'fy' },
            { label: 'AY Faculty', value: 'ay' },
            { label: 'Non-Faculty Academic', value: 'nfa' },
            { label: 'Chair', value: 'chair' },
            { label: 'CAO', value: 'cao' },
            { label: 'Admin', value: 'admin' },
          ]}
          value={designation}
        />
        <div className="sm:col-span-2">
          <FormField label="Position" onChange={setPosition} value={position} />
        </div>
      </div>

      <div className="mt-6 flex justify-end gap-3">
        <button className="btn btn-ghost" onClick={onClose} type="button">
          Cancel
        </button>
        <button
          className="btn border-0 bg-[var(--admin-gold)] text-[var(--admin-blue)] hover:bg-[var(--admin-gold)]/85"
          disabled={!name || !departmentId || !iamId}
          onClick={() =>
            onCreate({
              departmentId,
              designation,
              email,
              employeeId,
              iamId,
              name,
              position,
            })
          }
          type="button"
        >
          Create preview user
        </button>
      </div>
    </ModalFrame>
  );
}

function FormField({
  label,
  onChange,
  value,
}: {
  label: string;
  onChange: (value: string) => void;
  value: string;
}) {
  return (
    <label className="form-control w-full">
      <span className="label-text mb-2 text-sm font-medium text-[var(--admin-ink)]">
        {label}
      </span>
      <input
        className="input input-bordered w-full"
        onChange={(event) => onChange(event.target.value)}
        type="text"
        value={value}
      />
    </label>
  );
}

function SelectField({
  label,
  onChange,
  options,
  value,
}: {
  label: string;
  onChange: (value: string) => void;
  options: Array<{ label: string; value: string }>;
  value: string;
}) {
  return (
    <label className="form-control w-full">
      <span className="label-text mb-2 text-sm font-medium text-[var(--admin-ink)]">
        {label}
      </span>
      <select
        className="select select-bordered w-full"
        onChange={(event) => onChange(event.target.value)}
        value={value}
      >
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    </label>
  );
}
