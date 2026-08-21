export const vacationLeaveTypeLabel = 'Vacation';
export const sickLeaveTypeLabel = 'Sick Leave';
export const professionalDevelopmentLeaveTypeLabel =
  'Professional Development';
export const sabbaticalLeaveTypeLabel = 'Sabbatical';
export const fmlaLeaveTypeLabel = 'FMLA';

export const facultyLeaveTypeLabels = [
  vacationLeaveTypeLabel,
  sickLeaveTypeLabel,
  professionalDevelopmentLeaveTypeLabel,
  sabbaticalLeaveTypeLabel,
  fmlaLeaveTypeLabel,
] as const;

export type FacultyLeaveTypeLabel =
  (typeof facultyLeaveTypeLabels)[number];
