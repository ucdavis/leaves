import { fetchJson } from '@/lib/api.ts';
import type {
  AdminDepartment,
  AdminRole,
  AdminUser,
  ApprovalMode,
  UpdateUserInput,
} from '@/shared/admin/adminData.tsx';

type AdminFacultyUserResponse = Omit<AdminUser, 'departmentId' | 'role'> & {
  departmentId: string | null;
  departmentOverrideEndDate?: string | null;
  departmentOverrideId?: string | null;
  departmentOverrideStartDate?: string | null;
  role: string;
};

type AdminFacultyResponse = {
  departments: AdminDepartment[];
  facultyUsers: AdminFacultyUserResponse[];
};

export type AdminFacultyPageData = {
  departments: AdminDepartment[];
  facultyUsers: AdminUser[];
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

function normalizeDesignation(designation: string): AdminUser['designation'] {
  if (
    designation === 'admin' ||
    designation === 'ay' ||
    designation === 'cao' ||
    designation === 'chair' ||
    designation === 'nfa'
  ) {
    return designation;
  }

  return 'fy';
}

function normalizeFacultyData(
  response: AdminFacultyResponse
): AdminFacultyPageData {
  return {
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
    facultyUsers: response.facultyUsers.map((user) => ({
      ...user,
      departmentId: user.departmentId ?? '',
      departmentOverrideEndDate: user.departmentOverrideEndDate ?? '',
      departmentOverrideId: user.departmentOverrideId ?? '',
      departmentOverrideStartDate: user.departmentOverrideStartDate ?? '',
      designation: normalizeDesignation(user.designation),
      role: normalizeRole(user.role),
    })),
  };
}

export const adminFacultyQueryOptions = () => ({
  queryFn: async ({
    signal,
  }: {
    signal: AbortSignal;
  }): Promise<AdminFacultyPageData> => {
    const response = await fetchJson<AdminFacultyResponse>(
      '/api/admin/faculty',
      {},
      signal
    );

    return normalizeFacultyData(response);
  },
  queryKey: ['admin', 'faculty'] as const,
});

export async function updateAdminFacultyUser({
  signal,
  updates,
  userId,
}: {
  signal?: AbortSignal;
  updates: UpdateUserInput;
  userId: string;
}) {
  await fetchJson<void>(
    `/api/admin/users/by-iam/${encodeURIComponent(userId)}`,
    {
      body: JSON.stringify({
        active: updates.active,
        departmentOverrideEndDate: updates.departmentOverrideEndDate,
        departmentOverrideId: updates.departmentOverrideId,
        departmentOverrideSet: Object.hasOwn(updates, 'departmentOverrideId'),
        departmentOverrideStartDate: updates.departmentOverrideStartDate,
        email: updates.email,
        emailSet: Object.hasOwn(updates, 'email'),
        name: updates.name,
        nameSet: Object.hasOwn(updates, 'name'),
      }),
      method: 'PATCH',
    },
    signal
  );
}
