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
  id: string;
  status: ImportStatus;
  updatedAt: string | null;
};
