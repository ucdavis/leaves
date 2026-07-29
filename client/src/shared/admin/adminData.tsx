import {
  createContext,
  useContext,
  type ReactNode,
} from 'react';
import {
  useMutation,
  useQuery,
  useQueryClient,
} from '@tanstack/react-query';
import { fetchJson } from '@/lib/api.ts';
import {
  statusSurfaceColors,
  statusTextColors,
} from '@/shared/statusColors.ts';

export type AdminRole = 'faculty' | 'chair' | 'cao' | 'admin';
export type AdminDesignation = 'fy' | 'ay' | 'nfa' | 'chair' | 'cao' | 'admin';
export type ApprovalMode = 'notification' | 'approval' | 'auto';
export type ImportStatus = 'ready' | 'planned' | 'deferred';

export type AdminUser = {
  active: boolean;
  departmentId: string;
  departmentOverrideEndDate: string;
  departmentOverrideId: string;
  departmentOverrideStartDate: string;
  designation: AdminDesignation;
  email: string;
  employeeId: string;
  hasAppUser: boolean;
  iamId: string;
  id: string;
  name: string;
  position: string;
  role: AdminRole;
};

export type AdminUserEditableFields = Pick<
  AdminUser,
  'email' | 'name'
>;

export type UpdateUserInput = Partial<AdminUserEditableFields> & {
  active?: boolean;
  departmentOverrideEndDate?: string;
  departmentOverrideId?: string;
  departmentOverrideStartDate?: string;
};

export type DepartmentRoutingEmail = {
  address: string;
  id: string;
  kind: 'to' | 'cc';
};

export type AdminDepartment = {
  approvalMode: ApprovalMode;
  chairUserId: string | null;
  clusterId: string | null;
  code: string;
  id: string;
  name: string;
  routingEmails: DepartmentRoutingEmail[];
};

export type AdminCluster = {
  caoUserId: string | null;
  id: string;
  name: string;
};

export type AdminDataSource = {
  detail: string;
  id: string;
  label: string;
  status: ImportStatus;
  updatedAt: string | null;
};

type UpdateDepartmentInput = Partial<
  Pick<AdminDepartment, 'approvalMode' | 'clusterId'>
>;

type AdminStatusSnapshot = {
  departments: {
    clustered: number;
    total: number;
    withFaculty: number;
  };
  issues: {
    approachingVacationCap: number;
    excludedUsers: number;
    facultyAtVacationCap: number;
    missingEmails: number;
    pendingRequests: number;
  };
  requests: {
    bySource: Record<'cognos' | 'manual', number>;
    byType: Record<string, number>;
    pending: number;
    total: number;
  };
  users: {
    admins: number;
    ayFaculty: number;
    caos: number;
    chairs: number;
    fyFaculty: number;
    total: number;
  };
};

type AdminDashboardResponse = {
  clusters: AdminCluster[];
  dataSources: AdminDataSource[];
  departments: AdminDepartment[];
  facultyUsers: Array<AdminUser & { departmentId: string | null }>;
  statusSnapshot: AdminStatusSnapshot;
  users: Array<AdminUser & { departmentId: string | null }>;
};

type AdminDataContextValue = {
  clusters: AdminCluster[];
  dataSources: AdminDataSource[];
  departments: AdminDepartment[];
  facultyUsers: AdminUser[];
  removeRoutingEmail: (departmentId: string, emailId: string) => Promise<void>;
  renameDepartment: (departmentId: string, name: string) => Promise<void>;
  statusSnapshot: AdminStatusSnapshot;
  updateDepartment: (
    departmentId: string,
    updates: UpdateDepartmentInput
  ) => Promise<void>;
  updateUser: (userId: string, updates: UpdateUserInput) => Promise<void>;
  upsertRoutingEmail: (
    departmentId: string,
    email: Omit<DepartmentRoutingEmail, 'id'> & { id?: string }
  ) => Promise<void>;
  users: AdminUser[];
};

const ROLE_BY_DESIGNATION: Record<AdminDesignation, AdminRole> = {
  admin: 'admin',
  ay: 'faculty',
  cao: 'cao',
  chair: 'chair',
  fy: 'faculty',
  nfa: 'faculty',
};

const designationLabels: Record<AdminDesignation, string> = {
  admin: 'Admin',
  ay: 'AY Faculty',
  cao: 'CAO',
  chair: 'Chair',
  fy: 'FY Faculty',
  nfa: 'Non-Faculty Academic',
};

const adminDashboardQueryOptions = () => ({
  queryFn: () => fetchJson<AdminDashboardResponse>('/api/admin/dashboard'),
  queryKey: ['admin', 'dashboard'] as const,
});

const AdminDataContext = createContext<AdminDataContextValue | null>(null);

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

function normalizeDesignation(designation: string): AdminDesignation {
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

function normalizeAdminUser(
  user: AdminUser & { departmentId: string | null }
): AdminUser {
  return {
    ...user,
    departmentId: user.departmentId ?? '',
    departmentOverrideEndDate: user.departmentOverrideEndDate ?? '',
    departmentOverrideId: user.departmentOverrideId ?? '',
    departmentOverrideStartDate: user.departmentOverrideStartDate ?? '',
    designation: normalizeDesignation(user.designation),
    role: normalizeRole(user.role),
  };
}

function normalizeDashboardResponse(
  response: AdminDashboardResponse
): AdminDashboardResponse {
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
    facultyUsers: response.facultyUsers.map(normalizeAdminUser),
    users: response.users.map(normalizeAdminUser),
  };
}

async function invalidateAdminDashboard(
  queryClient: ReturnType<typeof useQueryClient>
) {
  await queryClient.invalidateQueries({ queryKey: ['admin', 'dashboard'] });
}

export function getRoleFromDesignation(
  designation: AdminDesignation
): AdminRole {
  return ROLE_BY_DESIGNATION[designation];
}

export function getDesignationLabel(designation: AdminDesignation) {
  return designationLabels[designation];
}

export function AdminDataProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const dashboardQuery = useQuery(adminDashboardQueryOptions());

  const updateUserMutation = useMutation({
    mutationFn: async ({
      updates,
      userId,
    }: {
      updates: UpdateUserInput;
      userId: string;
    }) => {
      await fetchJson<void>(`/api/admin/users/by-iam/${encodeURIComponent(userId)}`, {
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
      });
    },
    onSuccess: async () => {
      await invalidateAdminDashboard(queryClient);
    },
  });

  const updateDepartmentMutation = useMutation({
    mutationFn: async ({
      departmentId,
      updates,
    }: {
      departmentId: string;
      updates: UpdateDepartmentInput & { name?: string };
    }) => {
      const clusterIdWasProvided = Object.hasOwn(updates, 'clusterId');
      await fetchJson<void>(`/api/admin/departments/${departmentId}`, {
        body: JSON.stringify({
          approvalMode: updates.approvalMode,
          clusterId: updates.clusterId ? Number(updates.clusterId) : null,
          clusterIdSet: clusterIdWasProvided,
          name: updates.name,
        }),
        method: 'PATCH',
      });
    },
    onSuccess: async () => {
      await invalidateAdminDashboard(queryClient);
    },
  });

  const upsertRoutingEmailMutation = useMutation({
    mutationFn: async ({
      departmentId,
      email,
    }: {
      departmentId: string;
      email: Omit<DepartmentRoutingEmail, 'id'> & { id?: string };
    }) => {
      await fetchJson<void>(`/api/admin/departments/${departmentId}/routing-emails`, {
        body: JSON.stringify({ address: email.address }),
        method: 'POST',
      });
    },
    onSuccess: async () => {
      await invalidateAdminDashboard(queryClient);
    },
  });

  const removeRoutingEmailMutation = useMutation({
    mutationFn: async ({
      departmentId,
      emailId,
    }: {
      departmentId: string;
      emailId: string;
    }) => {
      await fetchJson<void>(
        `/api/admin/departments/${departmentId}/routing-emails/${emailId}`,
        { method: 'DELETE' }
      );
    },
    onSuccess: async () => {
      await invalidateAdminDashboard(queryClient);
    },
  });

  if (dashboardQuery.isLoading) {
    return (
      <section className="card border border-main-border bg-base-100">
        <div className="card-body p-6">
          <h2 className="text-lg font-semibold text-primary">
            Loading admin data
          </h2>
          <p className="mt-2 text-sm text-base-content/70">
            Pulling the current admin dashboard from the database.
          </p>
        </div>
      </section>
    );
  }

  if (dashboardQuery.isError || !dashboardQuery.data) {
    return (
      <section className={`card ${statusSurfaceColors.dangerCard}`}>
        <div className="card-body p-6">
          <h2
            className={`text-lg font-semibold ${statusTextColors.dangerStrong}`}
          >
            Admin data unavailable
          </h2>
          <p className={`mt-2 text-sm ${statusTextColors.danger}`}>
            The admin pages could not load their database-backed data right now.
          </p>
        </div>
      </section>
    );
  }

  const data = normalizeDashboardResponse(dashboardQuery.data);

  return (
    <AdminDataContext.Provider
      value={{
        clusters: data.clusters,
        dataSources: data.dataSources,
        departments: data.departments,
        facultyUsers: data.facultyUsers,
        removeRoutingEmail: async (departmentId, emailId) => {
          await removeRoutingEmailMutation.mutateAsync({ departmentId, emailId });
        },
        renameDepartment: async (departmentId, name) => {
          await updateDepartmentMutation.mutateAsync({
            departmentId,
            updates: { name },
          });
        },
        statusSnapshot: data.statusSnapshot,
        updateDepartment: async (departmentId, updates) => {
          await updateDepartmentMutation.mutateAsync({ departmentId, updates });
        },
        updateUser: async (userId, updates) => {
          await updateUserMutation.mutateAsync({ updates, userId });
        },
        upsertRoutingEmail: async (departmentId, email) => {
          await upsertRoutingEmailMutation.mutateAsync({ departmentId, email });
        },
        users: data.users,
      }}
    >
      {children}
    </AdminDataContext.Provider>
  );
}

export function useAdminData() {
  const value = useContext(AdminDataContext);

  if (!value) {
    throw new Error('useAdminData must be used within an AdminDataProvider.');
  }

  return value;
}
