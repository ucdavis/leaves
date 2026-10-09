export type ApprovalDecision = 'approved' | 'denied';

export type ApprovalScope = 'cluster' | 'team';

export type ApprovalRequest = {
  departmentName: string;
  endDate: string;
  facultyInitials: string;
  facultyName: string;
  id: number;
  leaveType: LeaveCategory;
  note?: string;
  startDate: string;
  totalHours: number;
};

export type CalendarFaculty = {
  departmentName: string;
  id: string;
  name: string;
};

export type CalendarLeave = {
  endDate: string;
  facultyId: string;
  id: number;
  leaveDates: string[];
  leaveType: LeaveCategory;
  startDate: string;
  status: 'Approved' | 'PendingApproval';
};

export type LeaveCategory =
  | 'FMLA'
  | 'Professional Development'
  | 'Sabbatical'
  | 'Sick Leave'
  | 'Vacation';
