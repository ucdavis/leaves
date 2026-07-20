import { fetchJson } from '@/lib/api.ts';

export type AdminAssignableRoleType = 'admin' | 'cao' | 'chair';

export type AdminRoleAssignment = {
  active: boolean;
  effectiveEndDate: string | null;
  effectiveStartDate: string | null;
  email: string;
  id: string;
  iamId: string;
  name: string;
  targetId: string | null;
  targetName: string | null;
  type: AdminAssignableRoleType;
};

export type AdminRoleOption = {
  id: string;
  name: string;
};

export type AdminRoleUserOption = {
  email: string;
  iamId: string;
  name: string;
};

export type AdminRolesResponse = {
  assignments: AdminRoleAssignment[];
  clusters: AdminRoleOption[];
  departments: AdminRoleOption[];
  users: AdminRoleUserOption[];
};

export const adminRolesQueryOptions = () => ({
  queryFn: async ({
    signal,
  }: {
    signal: AbortSignal;
  }): Promise<AdminRolesResponse> => {
    return await fetchJson<AdminRolesResponse>('/api/admin/roles', {}, signal);
  },
  queryKey: ['admin', 'roles'] as const,
});

export async function addAdminAssignment({
  iamId,
  signal,
}: {
  iamId: string;
  signal?: AbortSignal;
}) {
  await fetchJson<void>(
    '/api/admin/roles/admins',
    {
      body: JSON.stringify({ iamId }),
      method: 'POST',
    },
    signal
  );
}

export async function addCaoAssignment({
  clusterId,
  effectiveEndDate,
  effectiveStartDate,
  iamId,
  signal,
}: {
  clusterId: string;
  effectiveEndDate: string;
  effectiveStartDate: string;
  iamId: string;
  signal?: AbortSignal;
}) {
  await fetchJson<void>(
    '/api/admin/roles/caos',
    {
      body: JSON.stringify({
        clusterId: Number(clusterId),
        effectiveEndDate,
        effectiveStartDate,
        iamId,
      }),
      method: 'POST',
    },
    signal
  );
}

export async function addChairAssignment({
  departmentCode,
  effectiveEndDate,
  effectiveStartDate,
  iamId,
  signal,
}: {
  departmentCode: string;
  effectiveEndDate: string;
  effectiveStartDate: string;
  iamId: string;
  signal?: AbortSignal;
}) {
  await fetchJson<void>(
    '/api/admin/roles/chairs',
    {
      body: JSON.stringify({
        departmentCode,
        effectiveEndDate,
        effectiveStartDate,
        iamId,
      }),
      method: 'POST',
    },
    signal
  );
}

export async function removeRoleAssignment({
  id,
  type,
  signal,
}: {
  id: string;
  type: AdminAssignableRoleType;
  signal?: AbortSignal;
}) {
  await fetchJson<void>(
    `/api/admin/roles/${type}s/${encodeURIComponent(id)}`,
    { method: 'DELETE' },
    signal
  );
}
