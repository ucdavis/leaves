import { fetchJson } from '@/lib/api.ts';
import type {
  AdminCluster,
  AdminDepartment,
  AdminRole,
  AdminUser,
  ApprovalMode,
  DepartmentRoutingEmail,
} from '@/shared/admin/adminData.tsx';

export type CreateDepartmentInput = {
  approvalMode: ApprovalMode;
  clusterId: string | null;
  code: string;
  name: string;
};

type AdminDepartmentUserResponse = Omit<AdminUser, 'departmentId' | 'role'> & {
  departmentId: string | null;
  departmentOverrideEndDate?: string | null;
  departmentOverrideId?: string | null;
  departmentOverrideStartDate?: string | null;
  role: string;
};

type AdminDepartmentsResponse = {
  clusters: AdminCluster[];
  departments: AdminDepartment[];
  readonlyReason: string;
  users: AdminDepartmentUserResponse[];
};

export type AdminDepartmentsPageData = {
  clusters: AdminCluster[];
  departments: AdminDepartment[];
  readonlyReason: string;
  users: Array<AdminUser & { departmentId: string }>;
};

function normalizeApprovalMode(mode: string): ApprovalMode {
  if (mode === 'approval' || mode === 'auto') {
    return mode;
  }

  return 'notification';
}

function normalizeRole(role: string): AdminRole {
  if (role === 'admin' || role === 'chair' || role === 'cao') {
    return role;
  }

  return 'faculty';
}

function normalizeDepartmentData(
  response: AdminDepartmentsResponse
): AdminDepartmentsPageData {
  return {
    ...response,
    departments: response.departments.map((department) => ({
      ...department,
      approvalMode: normalizeApprovalMode(department.approvalMode),
      chairUserId: department.chairUserId ?? null,
      clusterId: department.clusterId ?? null,
      routingEmails: department.routingEmails.map((email) => ({
        ...email,
        kind: email.kind === 'cc' ? 'cc' : 'to',
      })),
    })),
    users: response.users.map((user) => ({
      ...user,
      departmentId: user.departmentId ?? '',
      departmentOverrideEndDate: user.departmentOverrideEndDate ?? '',
      departmentOverrideId: user.departmentOverrideId ?? '',
      departmentOverrideStartDate: user.departmentOverrideStartDate ?? '',
      role: normalizeRole(user.role),
    })),
  };
}

export const adminDepartmentsQueryOptions = () => ({
  queryFn: async ({
    signal,
  }: {
    signal: AbortSignal;
  }): Promise<AdminDepartmentsPageData> => {
    const response = await fetchJson<AdminDepartmentsResponse>(
      '/api/admin/departments',
      {},
      signal
    );

    return normalizeDepartmentData(response);
  },
  queryKey: ['admin', 'departments'] as const,
});

export async function createAdminCluster({
  name,
  signal,
}: {
  name: string;
  signal?: AbortSignal;
}) {
  await fetchJson<void>(
    '/api/admin/departments/clusters',
    {
      body: JSON.stringify({ name }),
      method: 'POST',
    },
    signal
  );
}

export async function createAdminDepartment({
  input,
  signal,
}: {
  input: CreateDepartmentInput;
  signal?: AbortSignal;
}) {
  await fetchJson<void>(
    '/api/admin/departments',
    {
      body: JSON.stringify({
        approvalMode: input.approvalMode,
        clusterId: input.clusterId ? Number(input.clusterId) : null,
        code: input.code,
        name: input.name,
      }),
      method: 'POST',
    },
    signal
  );
}

export async function renameAdminDepartment({
  departmentId,
  name,
  signal,
}: {
  departmentId: string;
  name: string;
  signal?: AbortSignal;
}) {
  await fetchJson<void>(
    `/api/admin/departments/${encodeURIComponent(departmentId)}`,
    {
      body: JSON.stringify({ name }),
      method: 'PATCH',
    },
    signal
  );
}

export async function updateAdminDepartment({
  departmentId,
  updates,
  signal,
}: {
  departmentId: string;
  updates: Partial<Pick<AdminDepartment, 'approvalMode' | 'clusterId'>>;
  signal?: AbortSignal;
}) {
  const clusterIdWasProvided = Object.hasOwn(updates, 'clusterId');

  await fetchJson<void>(
    `/api/admin/departments/${encodeURIComponent(departmentId)}`,
    {
      body: JSON.stringify({
        approvalMode: updates.approvalMode,
        clusterId: updates.clusterId ? Number(updates.clusterId) : null,
        clusterIdSet: clusterIdWasProvided,
      }),
      method: 'PATCH',
    },
    signal
  );
}

export async function upsertAdminDepartmentRoutingEmail({
  departmentId,
  email,
  signal,
}: {
  departmentId: string;
  email: Omit<DepartmentRoutingEmail, 'id'> & { id?: string };
  signal?: AbortSignal;
}) {
  await fetchJson<void>(
    `/api/admin/departments/${encodeURIComponent(departmentId)}/routing-emails`,
    {
      body: JSON.stringify({ address: email.address }),
      method: 'POST',
    },
    signal
  );
}

export async function removeAdminDepartmentRoutingEmail({
  departmentId,
  emailId,
  signal,
}: {
  departmentId: string;
  emailId: string;
  signal?: AbortSignal;
}) {
  await fetchJson<void>(
    `/api/admin/departments/${encodeURIComponent(departmentId)}/routing-emails/${encodeURIComponent(emailId)}`,
    { method: 'DELETE' },
    signal
  );
}
