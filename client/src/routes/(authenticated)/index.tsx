import { useQuery } from '@tanstack/react-query';
import { createFileRoute, redirect } from '@tanstack/react-router';
import { z } from 'zod';
import { HttpError } from '@/lib/api.ts';
import type { RouterContext } from '@/main.tsx';
import {
  facultyDashboardQueryOptions,
  facultyHistoryQueryOptions,
} from '@/queries/faculty.ts';
import { meQueryOptions } from '@/queries/user.ts';
import {
  canAccessApprovalWorkspace,
  canAccessFacultyWorkspace,
  hasAdminRole,
} from '@/shared/auth/roleAccess.ts';
import { PageErrorState } from '@/shared/errors/PageErrorState.tsx';
import { FacultyDashboardPage } from '@/shared/faculty/FacultyDashboardPage.tsx';

const dashboardSearchSchema = z.object({
  calendarDate: z.iso.date().optional(),
});

export const Route = createFileRoute('/(authenticated)/')({
  validateSearch: (search: Record<string, unknown>) => {
    const result = dashboardSearchSchema.safeParse(search);

    return result.success ? result.data : {};
  },
  // TanStack Router requires URL validation before route lifecycle hooks.
  beforeLoad: async ({ context }: { context: RouterContext }) => {
    const user = await context.queryClient.ensureQueryData(meQueryOptions());

    if (hasAdminRole(user.roles)) {
      throw redirect({ replace: true, to: '/admin' });
    }

    if (
      canAccessApprovalWorkspace(user.roles) &&
      !canAccessFacultyWorkspace(user.roles)
    ) {
      throw redirect({ replace: true, to: '/team-calendar' });
    }

    if (!canAccessFacultyWorkspace(user.roles)) {
      throw new HttpError(403, '/api/faculty/dashboard');
    }
  },
  component: RouteComponent,
});

function RouteComponent() {
  const dashboardQuery = useQuery(facultyDashboardQueryOptions());
  const historyQuery = useQuery(facultyHistoryQueryOptions());
  const { calendarDate } = Route.useSearch();

  if (dashboardQuery.isLoading) {
    return (
      <div className="container py-10">
        <div className="rounded-lg border border-base-300 bg-base-100 p-8 text-center shadow-sm">
          <span className="loading loading-spinner loading-lg text-primary"></span>
          <p className="mt-4 text-sm font-semibold text-base-content/70">
            Loading your leave dashboard.
          </p>
        </div>
      </div>
    );
  }

  if (dashboardQuery.isError || !dashboardQuery.data) {
    return (
      <div className="container py-10">
        <PageErrorState
          badge="Faculty dashboard"
          code="500"
          description="We could not load your faculty dashboard right now."
          title="Dashboard unavailable"
        />
      </div>
    );
  }

  return (
    <FacultyDashboardPage
      calendarDate={calendarDate}
      calendarRequests={historyQuery.data?.recentRequests}
      data={dashboardQuery.data}
    />
  );
}
