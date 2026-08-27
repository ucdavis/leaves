import { useQuery } from '@tanstack/react-query';
import { createFileRoute } from '@tanstack/react-router';
import { HttpError } from '@/lib/api.ts';
import type { RouterContext } from '@/main.tsx';
import { approvalWorkspaceQueryOptions } from '@/queries/approvals.ts';
import { meQueryOptions } from '@/queries/user.ts';
import { LeaveOverviewCalendar } from '@/shared/approvals/LeaveOverviewCalendar.tsx';
import { canAccessApprovalWorkspace } from '@/shared/auth/roleAccess.ts';

export const Route = createFileRoute('/(authenticated)/team-calendar')({
  beforeLoad: async ({ context }: { context: RouterContext }) => {
    const user = await context.queryClient.ensureQueryData(meQueryOptions());

    if (!canAccessApprovalWorkspace(user.roles)) {
      throw new HttpError(403, '/team-calendar');
    }
  },
  component: RouteComponent,
});

function RouteComponent() {
  const { data, isError, isLoading } = useQuery(approvalWorkspaceQueryOptions());

  if (isLoading) {
    return (
      <div className="container py-8 lg:py-10">
        <div className="mx-auto max-w-6xl rounded-lg border border-base-300 bg-base-100 p-8 text-center shadow-sm">
          <span className="loading loading-spinner loading-lg text-primary" />
          <p className="mt-4 text-sm font-semibold text-base-content/70">
            Loading team calendar data.
          </p>
        </div>
      </div>
    );
  }

  if (isError || !data) {
    return (
      <div className="container py-8 lg:py-10">
        <div className="mx-auto max-w-6xl rounded-lg border border-base-300 bg-base-100 p-8 text-center shadow-sm">
          <h2 className="text-lg font-bold text-primary">
            Team calendar unavailable
          </h2>
          <p className="mt-2 text-sm text-base-content/70">
            We could not load the faculty roster from the database.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="container py-8 lg:py-10">
      <div className="mx-auto max-w-6xl">
        <LeaveOverviewCalendar
          faculty={data.faculty}
          leaves={data.leaves}
          scope={data.scope}
        />
      </div>
    </div>
  );
}
