import { useQuery } from '@tanstack/react-query';
import { createFileRoute } from '@tanstack/react-router';
import { HttpError } from '@/lib/api.ts';
import type { RouterContext } from '@/main.tsx';
import { facultyDashboardByIamIdQueryOptions } from '@/queries/faculty.ts';
import { meQueryOptions } from '@/queries/user.ts';
import { canAccessApprovalWorkspace } from '@/shared/auth/roleAccess.ts';
import { PageErrorState } from '@/shared/errors/PageErrorState.tsx';
import { FacultyDashboardPage } from '@/shared/faculty/FacultyDashboardPage.tsx';

export const Route = createFileRoute('/(authenticated)/faculty/$iamId')({
  beforeLoad: async ({ context }: { context: RouterContext }) => {
    const user = await context.queryClient.ensureQueryData(meQueryOptions());

    if (!canAccessApprovalWorkspace(user.roles)) {
      throw new HttpError(403, '/api/faculty/dashboard');
    }
  },
  component: RouteComponent,
});

function RouteComponent() {
  const { iamId } = Route.useParams();
  const dashboardQuery = useQuery(facultyDashboardByIamIdQueryOptions(iamId));

  if (dashboardQuery.isLoading) {
    return (
      <div className="container py-10">
        <div className="rounded-lg border border-base-300 bg-base-100 p-8 text-center shadow-sm">
          <span className="loading loading-spinner loading-lg text-primary" />
          <p className="mt-4 text-sm font-semibold text-base-content/70">
            Loading faculty dashboard.
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
          code="404"
          description="We could not load that faculty dashboard right now."
          title="Dashboard unavailable"
        />
      </div>
    );
  }

  return <FacultyDashboardPage data={dashboardQuery.data} readOnly />;
}
