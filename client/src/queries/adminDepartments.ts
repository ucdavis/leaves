import { queryOptions } from '@tanstack/react-query';
import { fetchJson } from '@/lib/api.ts';
import type {
  AdminCluster,
  AdminDepartment,
  AdminUser,
  ApprovalMode,
  DepartmentRoutingEmail,
} from '@/shared/admin/adminData.ts';

export type CreateDepartmentInput = {
  approvalMode: ApprovalMode;
  clusterId: string | null;
  code: string;
  name: string;
};

export type DepartmentOption = {
  code: string;
  currentFacultyCount: number;
  description: string | null;
};

export type AdminCaoUser = Pick<
  AdminUser,
  'id' | 'active' | 'email' | 'name'
> & {
  designation: AdminUser['designation'] | 'faculty';
};

export type DepartmentFacultyMember = {
  email: string;
  id: string;
  name: string;
};

type AdminDepartmentsResponse = {
  clusters: AdminCluster[];
  departments: AdminDepartment[];
  inactiveClusters?: AdminCluster[];
  inactiveDepartments?: AdminDepartment[];
};

export type AdminDepartmentsPageData = {
  clusters: AdminCluster[];
  departments: AdminDepartment[];
  inactiveClusters: AdminCluster[];
  inactiveDepartments: AdminDepartment[];
};

function normalizeApprovalMode(mode: string): ApprovalMode {
  return mode === 'approval' ? 'approval' : 'auto';
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
    inactiveClusters: response.inactiveClusters ?? [],
    inactiveDepartments: response.inactiveDepartments ?? [],
  };
}

export const adminDepartmentsQueryOptions = () =>
  queryOptions({
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
    staleTime: 60_000,
  });

export const departmentFacultyQueryOptions = (departmentCode: string) =>
  queryOptions({
    queryFn: ({ signal }): Promise<DepartmentFacultyMember[]> =>
      fetchJson<DepartmentFacultyMember[]>(
        `/api/admin/departments/${encodeURIComponent(departmentCode)}/faculty`,
        {},
        signal
      ),
    queryKey: ['admin', 'departments', departmentCode, 'faculty'] as const,
    staleTime: 60_000,
  });

export const caoCandidatesQueryOptions = (query: string) => {
  const term = query.trim();
  return queryOptions({
    enabled: term.length >= 2 && term.length <= 128,
    queryFn: ({ signal }): Promise<AdminCaoUser[]> =>
      fetchJson<AdminCaoUser[]>(
        `/api/admin/departments/cao-candidates?query=${encodeURIComponent(term)}`,
        {},
        signal
      ),
    queryKey: ['admin', 'caoCandidates', term] as const,
  });
};

export const departmentOptionsQueryOptions = (query = '') => {
  const term = query.trim();
  return queryOptions({
    enabled: term.length <= 128,
    queryFn: ({ signal }): Promise<DepartmentOption[]> =>
      fetchJson<DepartmentOption[]>(
        `/api/admin/departments/options?query=${encodeURIComponent(term)}`,
        {},
        signal
      ),
    queryKey: ['admin', 'departmentOptions', term] as const,
  });
};

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

export async function updateAdminCluster({
  clusterId,
  signal,
  updates,
}: {
  clusterId: string;
  signal?: AbortSignal;
  updates: Partial<Pick<AdminCluster, 'caoUserId' | 'name'>>;
}) {
  const caoUserIdWasProvided = Object.hasOwn(updates, 'caoUserId');

  await fetchJson<void>(
    `/api/admin/departments/clusters/${encodeURIComponent(clusterId)}`,
    {
      body: JSON.stringify({
        caoUserId: updates.caoUserId,
        caoUserIdSet: caoUserIdWasProvided,
        name: updates.name,
      }),
      method: 'PATCH',
    },
    signal
  );
}

export async function deleteAdminCluster({
  clusterId,
  signal,
}: {
  clusterId: string;
  signal?: AbortSignal;
}) {
  await fetchJson<void>(
    `/api/admin/departments/clusters/${encodeURIComponent(clusterId)}`,
    { method: 'DELETE' },
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

export async function reactivateAdminDepartment({
  departmentId,
  signal,
}: {
  departmentId: string;
  signal?: AbortSignal;
}) {
  await fetchJson<void>(
    `/api/admin/departments/${encodeURIComponent(departmentId)}/reactivate`,
    { method: 'POST' },
    signal
  );
}

export async function reactivateAdminCluster({
  clusterId,
  signal,
}: {
  clusterId: string;
  signal?: AbortSignal;
}) {
  await fetchJson<void>(
    `/api/admin/departments/clusters/${encodeURIComponent(clusterId)}/reactivate`,
    { method: 'POST' },
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

export async function deleteAdminDepartment({
  departmentId,
  signal,
}: {
  departmentId: string;
  signal?: AbortSignal;
}) {
  await fetchJson<void>(
    `/api/admin/departments/${encodeURIComponent(departmentId)}`,
    { method: 'DELETE' },
    signal
  );
}

export async function updateAdminDepartment({
  departmentId,
  signal,
  updates,
}: {
  departmentId: string;
  signal?: AbortSignal;
  updates: UpdateAdminDepartmentInput;
}) {
  const chairUserIdWasProvided = Object.hasOwn(updates, 'chairUserId');
  const clusterIdWasProvided = Object.hasOwn(updates, 'clusterId');

  await fetchJson<void>(
    `/api/admin/departments/${encodeURIComponent(departmentId)}`,
    {
      body: JSON.stringify({
        approvalMode: updates.approvalMode,
        approvePendingRequests: updates.approvePendingRequests ?? false,
        chairUserId: updates.chairUserId,
        chairUserIdSet: chairUserIdWasProvided,
        clusterId: updates.clusterId ? Number(updates.clusterId) : null,
        clusterIdSet: clusterIdWasProvided,
        name: updates.name,
      }),
      method: 'PATCH',
    },
    signal
  );
}

export type UpdateAdminDepartmentInput = Partial<
  Pick<AdminDepartment, 'approvalMode' | 'chairUserId' | 'clusterId' | 'name'>
> & {
  approvePendingRequests?: boolean;
};

export async function getAdminDepartmentPendingApprovalCount({
  departmentId,
  signal,
}: {
  departmentId: string;
  signal?: AbortSignal;
}) {
  return await fetchJson<{ pendingRequestCount: number }>(
    `/api/admin/departments/${encodeURIComponent(departmentId)}/pending-request-count`,
    {},
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
