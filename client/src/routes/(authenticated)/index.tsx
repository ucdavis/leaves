import { useQuery } from '@tanstack/react-query';
import { createFileRoute } from '@tanstack/react-router';
import { HttpError } from '@/lib/api.ts';
import { useNavigate } from '@tanstack/react-router';
import { useState } from 'react';
import { RouterContext } from '@/main.tsx';
import {
  facultyDashboardQueryOptions,
  type FacultyDashboardResponse,
  type FacultyLeaveRequest,
} from '@/queries/faculty.ts';
import { meQueryOptions } from '@/queries/user.ts';
import { canAccessFacultyWorkspace } from '@/shared/auth/roleAccess.ts';
import { PageErrorState } from '@/shared/errors/PageErrorState.tsx';
import { LeaveCalendar } from '@/shared/faculty/FacultyDashboardCalendar.tsx';
import {
  AccrualBalancePanel,
  FacultyToast,
  RecentRequestsPanel,
  QuickActionsPanel,
} from '@/shared/faculty/FacultyDashboardPanels.tsx';
import {
  ReportLeaveModal,
  RequestDetailModal,
} from '@/shared/faculty/FacultyDashboardModals.tsx';

export const Route = createFileRoute('/(authenticated)/')({
  beforeLoad: async ({ context }: { context: RouterContext }) => {
    const user = await context.queryClient.ensureQueryData(meQueryOptions());

    if (!canAccessFacultyWorkspace(user.roles)) {
      throw new HttpError(403, '/api/faculty/dashboard');
    }
  },
  component: RouteComponent,
});

function RouteComponent() {
  const dashboardQuery = useQuery(facultyDashboardQueryOptions());

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

  return <DashboardContent data={dashboardQuery.data} />;
}

function DashboardContent({
  data,
}: {
  data: FacultyDashboardResponse;
}) {
  const navigate = useNavigate();
  const [reportModalOpen, setReportModalOpen] = useState(false);
  const [selectedRequest, setSelectedRequest] =
    useState<FacultyLeaveRequest | null>(null);
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  return (
    <div className="container py-8 lg:py-10">
      <div className="mx-auto grid max-w-6xl gap-5 lg:grid-cols-2">
        <AccrualBalancePanel balances={data.accrualBalances} />
        <div className="space-y-5">
          <QuickActionsPanel
            data={data}
            onReportLeave={() => setReportModalOpen(true)}
            onViewHistory={() => void navigate({ to: '/history' })}
          />
          <RecentRequestsPanel
            onSelectRequest={setSelectedRequest}
            requests={data.recentRequests.slice(0, 5)}
          />
        </div>
      </div>

      <div className="mx-auto mt-6 max-w-6xl">
        <LeaveCalendar faculty={data.faculty} requests={data.recentRequests} />
      </div>

      {reportModalOpen ? (
        <ReportLeaveModal
          data={data}
          onClose={() => setReportModalOpen(false)}
          onSent={(message) => setToastMessage(message)}
        />
      ) : null}
      {selectedRequest ? (
        <RequestDetailModal
          faculty={data.faculty}
          onClose={() => setSelectedRequest(null)}
          request={selectedRequest}
        />
      ) : null}
      <FacultyToast
        message={toastMessage}
        onDismiss={() => setToastMessage(null)}
      />
    </div>
  );
}
