import { queryOptions } from '@tanstack/react-query';
import { fetchJson } from '@/lib/api.ts';

export type AdminAssignableRoleType = 'admin' | 'cao' | 'chair';

export type AdminRoleAssignment = {
  active: boolean;
  effectiveEndDate: string | null;
  effectiveStartDate: string | null;
  email: string;
  iamId: string;
  id: string;
  name: string;
  targetId: string | null;
  targetName: string | null;
  type: AdminAssignableRoleType;
};

export type AdminRoleOption = {
  active: boolean;
  id: string;
  name: string;
};

export type AdminRoleUserOption = {
  departmentId: string | null;
  departmentName: string | null;
  departmentOptions: AdminRoleOption[];
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

export const adminRolesQueryOptions = () =>
  queryOptions({
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
  iamId,
  signal,
}: {
  clusterId: string;
  iamId: string;
  signal?: AbortSignal;
}) {
  await fetchJson<void>(
    '/api/admin/roles/caos',
    {
      body: JSON.stringify({
        clusterId: Number(clusterId),
        iamId,
      }),
      method: 'POST',
    },
    signal
  );
}

export async function addChairAssignment({
  departmentCode,
  iamId,
  signal,
}: {
  departmentCode: string;
  iamId: string;
  signal?: AbortSignal;
}) {
  await fetchJson<void>(
    '/api/admin/roles/chairs',
    {
      body: JSON.stringify({
        departmentCode,
        iamId,
      }),
      method: 'POST',
    },
    signal
  );
}

export async function removeRoleAssignment({
  id,
  signal,
  type,
}: {
  id: string;
  signal?: AbortSignal;
  type: AdminAssignableRoleType;
}) {
  await fetchJson<void>(
    `/api/admin/roles/${type}s/${encodeURIComponent(id)}`,
    { method: 'DELETE' },
    signal
  );
}
