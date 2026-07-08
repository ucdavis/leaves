import {
  createContext,
  useContext,
  useState,
  type ReactNode,
} from 'react';

export type AdminRole = 'faculty' | 'chair' | 'cao' | 'admin';
export type AdminDesignation = 'fy' | 'ay' | 'nfa' | 'chair' | 'cao' | 'admin';
export type ApprovalMode = 'notification' | 'approval' | 'auto';
export type ImportStatus = 'ready' | 'planned' | 'deferred';

export type AdminUser = {
  active: boolean;
  departmentId: string;
  designation: AdminDesignation;
  email: string;
  employeeId: string;
  iamId: string;
  id: string;
  name: string;
  position: string;
  role: AdminRole;
};

export type DepartmentRoutingEmail = {
  address: string;
  id: string;
  kind: 'to' | 'cc';
};

export type AdminDepartment = {
  approvalMode: ApprovalMode;
  autoDebitEnabled: boolean;
  chairUserId: string | null;
  clusterId: string | null;
  code: string;
  dispositionRequired: boolean;
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

export type AdminImportRecord = {
  filename: string;
  id: string;
  notes: string;
  rows: number;
  source: string;
  status: 'planned' | 'seeded' | 'deferred';
  updatedAt: string;
};

type CreateUserInput = {
  departmentId: string;
  designation: AdminDesignation;
  email: string;
  employeeId: string;
  iamId: string;
  name: string;
  position: string;
};

type UpdateUserInput = Partial<
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
>;

type UpdateDepartmentInput = Partial<
  Pick<
    AdminDepartment,
    | 'approvalMode'
    | 'autoDebitEnabled'
    | 'clusterId'
    | 'dispositionRequired'
  >
>;

type AdminStatusSnapshot = {
  autoDebit: {
    active: number;
    eligible: number;
  };
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
    bySource: Record<'auto-debit' | 'cognos' | 'manual', number>;
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

type AdminDataContextValue = {
  clusters: AdminCluster[];
  createUser: (input: CreateUserInput) => void;
  dataSources: AdminDataSource[];
  departments: AdminDepartment[];
  readonlyReason: string;
  removeRoutingEmail: (departmentId: string, emailId: string) => void;
  renameCluster: (clusterId: string, name: string) => void;
  renameDepartment: (departmentId: string, name: string) => void;
  setClusterCao: (clusterId: string, userId: string | null) => void;
  setDepartmentChair: (departmentId: string, userId: string | null) => void;
  statusSnapshot: AdminStatusSnapshot;
  updateDepartment: (
    departmentId: string,
    updates: UpdateDepartmentInput
  ) => void;
  updateUser: (userId: string, updates: UpdateUserInput) => void;
  upsertRoutingEmail: (
    departmentId: string,
    email: Omit<DepartmentRoutingEmail, 'id'> & { id?: string }
  ) => void;
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

const initialClusters: AdminCluster[] = [
  {
    caoUserId: 'user-lin',
    id: 'cluster-animal',
    name: 'Animal Sciences Cluster',
  },
  {
    caoUserId: 'user-owens',
    id: 'cluster-land',
    name: 'Land & Environment Cluster',
  },
];

const initialDepartments: AdminDepartment[] = [
  {
    approvalMode: 'approval',
    autoDebitEnabled: true,
    chairUserId: 'user-patel',
    clusterId: 'cluster-animal',
    code: '030010',
    dispositionRequired: true,
    id: 'dept-animal-science',
    name: 'Animal Science',
    routingEmails: [
      { address: 'aggieservice-animal@ucdavis.edu', id: 'route-1', kind: 'to' },
      { address: 'leave-ops@ucdavis.edu', id: 'route-2', kind: 'cc' },
    ],
  },
  {
    approvalMode: 'notification',
    autoDebitEnabled: false,
    chairUserId: 'user-garcia',
    clusterId: 'cluster-animal',
    code: '030385',
    dispositionRequired: false,
    id: 'dept-vet-med',
    name: 'Population Health & Reproduction',
    routingEmails: [
      { address: 'aggieservice-vet@ucdavis.edu', id: 'route-3', kind: 'to' },
    ],
  },
  {
    approvalMode: 'approval',
    autoDebitEnabled: true,
    chairUserId: 'user-chen',
    clusterId: 'cluster-land',
    code: '030005',
    dispositionRequired: true,
    id: 'dept-plant-sciences',
    name: 'Plant Sciences',
    routingEmails: [
      { address: 'aggieservice-plants@ucdavis.edu', id: 'route-4', kind: 'to' },
    ],
  },
  {
    approvalMode: 'auto',
    autoDebitEnabled: false,
    chairUserId: null,
    clusterId: null,
    code: '030001',
    dispositionRequired: false,
    id: 'dept-agronomy',
    name: 'Agricultural Experiment Stations',
    routingEmails: [],
  },
];

const initialUsers: AdminUser[] = [
  {
    active: true,
    departmentId: 'dept-animal-science',
    designation: 'admin',
    email: 'admin@ucdavis.edu',
    employeeId: '10294837',
    iamId: 'adminherd',
    id: 'user-admin',
    name: 'Maya Thompson',
    position: 'Application Administrator',
    role: 'admin',
  },
  {
    active: true,
    departmentId: 'dept-animal-science',
    designation: 'chair',
    email: 'apatel@ucdavis.edu',
    employeeId: '10294838',
    iamId: 'apatel',
    id: 'user-patel',
    name: 'Asha Patel',
    position: 'Department Chair',
    role: 'chair',
  },
  {
    active: true,
    departmentId: 'dept-animal-science',
    designation: 'cao',
    email: 'jlin@ucdavis.edu',
    employeeId: '10294839',
    iamId: 'jlin',
    id: 'user-lin',
    name: 'Jordan Lin',
    position: 'Chief Administrative Officer',
    role: 'cao',
  },
  {
    active: true,
    departmentId: 'dept-vet-med',
    designation: 'chair',
    email: 'egarcia@ucdavis.edu',
    employeeId: '10294840',
    iamId: 'egarcia',
    id: 'user-garcia',
    name: 'Elena Garcia',
    position: 'Department Chair',
    role: 'chair',
  },
  {
    active: true,
    departmentId: 'dept-plant-sciences',
    designation: 'chair',
    email: 'kchen@ucdavis.edu',
    employeeId: '10294841',
    iamId: 'kchen',
    id: 'user-chen',
    name: 'Kai Chen',
    position: 'Department Chair',
    role: 'chair',
  },
  {
    active: true,
    departmentId: 'dept-plant-sciences',
    designation: 'cao',
    email: 'mowens@ucdavis.edu',
    employeeId: '10294842',
    iamId: 'mowens',
    id: 'user-owens',
    name: 'Morgan Owens',
    position: 'Chief Administrative Officer',
    role: 'cao',
  },
  {
    active: true,
    departmentId: 'dept-animal-science',
    designation: 'fy',
    email: 'lwilson@ucdavis.edu',
    employeeId: '10294843',
    iamId: 'lwilson',
    id: 'user-wilson',
    name: 'Lena Wilson',
    position: 'Professor',
    role: 'faculty',
  },
  {
    active: true,
    departmentId: 'dept-animal-science',
    designation: 'ay',
    email: '',
    employeeId: '10294844',
    iamId: 'rshah',
    id: 'user-shah',
    name: 'Riya Shah',
    position: 'Assistant Professor',
    role: 'faculty',
  },
  {
    active: true,
    departmentId: 'dept-vet-med',
    designation: 'fy',
    email: 'nroberts@ucdavis.edu',
    employeeId: '10294845',
    iamId: 'nroberts',
    id: 'user-roberts',
    name: 'Noah Roberts',
    position: 'Professor',
    role: 'faculty',
  },
  {
    active: true,
    departmentId: 'dept-plant-sciences',
    designation: 'nfa',
    email: 'sbaker@ucdavis.edu',
    employeeId: '10294846',
    iamId: 'sbaker',
    id: 'user-baker',
    name: 'Sofia Baker',
    position: 'Specialist',
    role: 'faculty',
  },
  {
    active: false,
    departmentId: 'dept-agronomy',
    designation: 'fy',
    email: 'tnguyen@ucdavis.edu',
    employeeId: '10294847',
    iamId: 'tnguyen',
    id: 'user-nguyen',
    name: 'Theo Nguyen',
    position: 'Professor',
    role: 'faculty',
  },
];

const initialDataSources: AdminDataSource[] = [
  {
    detail: '',
    id: 'cognos',
    label: 'Cognos leave history',
    status: 'deferred',
    updatedAt: null,
  },
  {
    detail: '',
    id: 'roster',
    label: 'People roster',
    status: 'planned',
    updatedAt: '2026-07-01T16:00:00Z',
  },
  {
    detail: '',
    id: 'assignments',
    label: 'Department assignments',
    status: 'planned',
    updatedAt: '2026-07-02T18:30:00Z',
  },
  {
    detail: '',
    id: 'balances',
    label: 'Balance snapshots',
    status: 'deferred',
    updatedAt: null,
  },
];

const AdminDataContext = createContext<AdminDataContextValue | null>(null);

function buildStatusSnapshot(
  users: AdminUser[],
  departments: AdminDepartment[],
  clusters: AdminCluster[]
): AdminStatusSnapshot {
  const activeUsers = users.filter((user) => user.active);
  const fyFaculty = activeUsers.filter((user) => user.designation === 'fy').length;
  const ayFaculty = activeUsers.filter((user) => user.designation === 'ay').length;
  const admins = activeUsers.filter((user) => user.role === 'admin').length;
  const chairs = activeUsers.filter((user) => user.role === 'chair').length;
  const caos = activeUsers.filter((user) => user.role === 'cao').length;
  const missingEmails = activeUsers.filter((user) => !user.email.trim()).length;
  const excludedUsers = users.filter((user) => !user.active).length;
  const departmentsWithFaculty = departments.filter((department) =>
    activeUsers.some(
      (user) =>
        user.departmentId === department.id &&
        ['fy', 'ay', 'nfa'].includes(user.designation)
    )
  ).length;

  return {
    autoDebit: {
      active: departments.filter((department) => department.autoDebitEnabled)
        .length,
      eligible: fyFaculty + chairs,
    },
    departments: {
      clustered: departments.filter((department) => department.clusterId).length,
      total: departments.length,
      withFaculty: departmentsWithFaculty,
    },
    issues: {
      approachingVacationCap: 3,
      excludedUsers,
      facultyAtVacationCap: 1,
      missingEmails,
      pendingRequests: 4,
    },
    requests: {
      bySource: {
        'auto-debit': 8,
        cognos: 11,
        manual: 27,
      },
      byType: {
        FamilyCare: 6,
        Sabbatical: 3,
        Sick: 14,
        Vacation: 23,
      },
      pending: 4,
      total: 46,
    },
    users: {
      admins,
      ayFaculty,
      caos,
      chairs,
      fyFaculty,
      total: activeUsers.length,
    },
  };
}

function buildRoutingEmailId() {
  return `route-${Math.random().toString(36).slice(2, 8)}`;
}

function toTitleCaseLabel(designation: AdminDesignation) {
  return designationLabels[designation];
}

export function getRoleFromDesignation(
  designation: AdminDesignation
): AdminRole {
  return ROLE_BY_DESIGNATION[designation];
}

export function getDesignationLabel(designation: AdminDesignation) {
  return toTitleCaseLabel(designation);
}

export function AdminDataProvider({ children }: { children: ReactNode }) {
  const [users, setUsers] = useState(initialUsers);
  const [departments, setDepartments] = useState(initialDepartments);
  const [clusters, setClusters] = useState(initialClusters);

  const readonlyReason =
    'These pages are intentionally backed by in-memory preview data until the people, assignment, and balance tables exist in leaves.';

  const createUser = (input: CreateUserInput) => {
    const designation = input.designation;
    const nextUser: AdminUser = {
      active: true,
      departmentId: input.departmentId,
      designation,
      email: input.email,
      employeeId: input.employeeId,
      iamId: input.iamId,
      id: `user-${input.iamId.toLowerCase()}`,
      name: input.name,
      position: input.position,
      role: getRoleFromDesignation(designation),
    };

    setUsers((currentUsers) => [...currentUsers, nextUser]);
  };

  const updateUser = (userId: string, updates: UpdateUserInput) => {
    setUsers((currentUsers) =>
      currentUsers.map((user) => {
        if (user.id !== userId) {
          return user;
        }

        const designation = updates.designation ?? user.designation;

        return {
          ...user,
          ...updates,
          designation,
          role: getRoleFromDesignation(designation),
        };
      })
    );
  };

  const setDepartmentChair = (departmentId: string, userId: string | null) => {
    const previousChairId =
      departments.find((department) => department.id === departmentId)
        ?.chairUserId ?? null;

    setDepartments((currentDepartments) =>
      currentDepartments.map((department) =>
        department.id === departmentId ? { ...department, chairUserId: userId } : department
      )
    );

    setUsers((currentUsers) =>
      currentUsers.map((user) => {
        if (user.id === previousChairId && user.id !== userId) {
          return { ...user, designation: 'fy', role: 'faculty' };
        }

        if (user.id === userId) {
          return {
            ...user,
            active: true,
            departmentId,
            designation: 'chair',
            role: 'chair',
          };
        }

        return user;
      })
    );
  };

  const setClusterCao = (clusterId: string, userId: string | null) => {
    const previousCaoId =
      clusters.find((cluster) => cluster.id === clusterId)?.caoUserId ?? null;
    const fallbackDepartmentId =
      departments.find((department) => department.clusterId === clusterId)?.id ??
      departments[0]?.id ??
      '';

    setClusters((currentClusters) =>
      currentClusters.map((cluster) =>
        cluster.id === clusterId ? { ...cluster, caoUserId: userId } : cluster
      )
    );

    setUsers((currentUsers) =>
      currentUsers.map((user) => {
        if (user.id === previousCaoId && user.id !== userId) {
          return { ...user, designation: 'fy', role: 'faculty' };
        }

        if (user.id === userId) {
          return {
            ...user,
            active: true,
            departmentId: fallbackDepartmentId,
            designation: 'cao',
            role: 'cao',
          };
        }

        return user;
      })
    );
  };

  const updateDepartment = (
    departmentId: string,
    updates: UpdateDepartmentInput
  ) => {
    setDepartments((currentDepartments) =>
      currentDepartments.map((department) =>
        department.id === departmentId ? { ...department, ...updates } : department
      )
    );
  };

  const renameDepartment = (departmentId: string, name: string) => {
    setDepartments((currentDepartments) =>
      currentDepartments.map((department) =>
        department.id === departmentId ? { ...department, name } : department
      )
    );
  };

  const renameCluster = (clusterId: string, name: string) => {
    setClusters((currentClusters) =>
      currentClusters.map((cluster) =>
        cluster.id === clusterId ? { ...cluster, name } : cluster
      )
    );
  };

  const upsertRoutingEmail = (
    departmentId: string,
    email: Omit<DepartmentRoutingEmail, 'id'> & { id?: string }
  ) => {
    setDepartments((currentDepartments) =>
      currentDepartments.map((department) => {
        if (department.id !== departmentId) {
          return department;
        }

        const nextEmail: DepartmentRoutingEmail = {
          ...email,
          id: email.id ?? buildRoutingEmailId(),
        };
        const existingIndex = department.routingEmails.findIndex(
          (item) => item.id === nextEmail.id
        );

        if (existingIndex === -1) {
          return {
            ...department,
            routingEmails: [...department.routingEmails, nextEmail],
          };
        }

        return {
          ...department,
          routingEmails: department.routingEmails.map((item) =>
            item.id === nextEmail.id ? nextEmail : item
          ),
        };
      })
    );
  };

  const removeRoutingEmail = (departmentId: string, emailId: string) => {
    setDepartments((currentDepartments) =>
      currentDepartments.map((department) =>
        department.id === departmentId
          ? {
              ...department,
              routingEmails: department.routingEmails.filter(
                (email) => email.id !== emailId
              ),
            }
          : department
      )
    );
  };

  return (
    <AdminDataContext.Provider
      value={{
        clusters,
        createUser,
        dataSources: initialDataSources,
        departments,
        readonlyReason,
        removeRoutingEmail,
        renameCluster,
        renameDepartment,
        setClusterCao,
        setDepartmentChair,
        statusSnapshot: buildStatusSnapshot(users, departments, clusters),
        updateDepartment,
        updateUser,
        upsertRoutingEmail,
        users,
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
