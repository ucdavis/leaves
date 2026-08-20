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

export const hasRole = (
  roles: readonly string[],
  allowedRoles: ReadonlySet<AppRole>
) => roles.some((role): role is AppRole => allowedRoles.has(role as AppRole));

export const hasAdminRole = (roles: readonly string[]) =>
  roles.includes(ROLE_NAMES.admin);

export const canAccessFacultyWorkspace = (roles: readonly string[]) =>
  hasRole(roles, FACULTY_WORKSPACE_ROLES);
