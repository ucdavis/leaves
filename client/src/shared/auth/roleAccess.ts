export const ROLE_NAMES = {
  admin: 'Admin',
  cao: 'CAO',
  chair: 'Chair',
  faculty: 'Faculty',
} as const;

export type AppRole = (typeof ROLE_NAMES)[keyof typeof ROLE_NAMES];

const FACULTY_WORKSPACE_ROLES = new Set<AppRole>([
  ROLE_NAMES.faculty,
  ROLE_NAMES.chair,
]);

const APPROVAL_WORKSPACE_ROLES = new Set<AppRole>([
  ROLE_NAMES.cao,
  ROLE_NAMES.chair,
]);

export const hasRole = (
  roles: readonly string[],
  allowedRoles: ReadonlySet<AppRole>
) =>
  roles.some((role): role is AppRole =>
    [...allowedRoles].some(
      (allowedRole) => allowedRole.toLowerCase() === role.toLowerCase()
    )
  );

export const hasAdminRole = (roles: readonly string[]) =>
  roles.some((role) => role.toLowerCase() === ROLE_NAMES.admin.toLowerCase());

export const canAccessFacultyWorkspace = (roles: readonly string[]) =>
  hasRole(roles, FACULTY_WORKSPACE_ROLES);

export const canAccessApprovalWorkspace = (roles: readonly string[]) =>
  hasRole(roles, APPROVAL_WORKSPACE_ROLES);

export const hasCaoRole = (roles: readonly string[]) =>
  hasRole(roles, new Set([ROLE_NAMES.cao]));
