import { useQuery } from '@tanstack/react-query';
import { createFileRoute } from '@tanstack/react-router';
import { HttpError } from '@/lib/api.ts';
import { useState } from 'react';
import { RouterContext } from '@/main.tsx';
import type {
  FacultyDashboardResponse,
  FacultyLeaveRequest,
} from '@/queries/faculty.ts';
import { facultyHistoryQueryOptions } from '@/queries/faculty.ts';
import { meQueryOptions } from '@/queries/user.ts';
import { canAccessFacultyWorkspace } from '@/shared/auth/roleAccess.ts';
import { PageErrorState } from '@/shared/errors/PageErrorState.tsx';
import {
  FacultyToast,
  reportLeaveButtonClass,
} from '@/shared/faculty/FacultyDashboardPanels.tsx';
import {
  ReportLeaveModal,
  RequestDetailModal,
  getReportLeaveTypeOptions,
} from '@/shared/faculty/FacultyDashboardModals.tsx';
import { ExistingRequestEmailPreviewModal } from '@/shared/faculty/FacultyDashboardEmailPreviews.tsx';
import { RequestHistoryTable } from '@/shared/faculty/RequestHistoryTable.tsx';

export const Route = createFileRoute('/(authenticated)/history')({
  beforeLoad: async ({ context }: { context: RouterContext }) => {
    const user = await context.queryClient.ensureQueryData(meQueryOptions());

    if (!canAccessFacultyWorkspace(user.roles)) {
      throw new HttpError(403, '/api/faculty/history');
    }
  },
  component: RouteComponent,
});

function RouteComponent() {
  const dashboardQuery = useQuery(facultyHistoryQueryOptions());
  const [selectedType, setSelectedType] = useState('');
  const [reportModalOpen, setReportModalOpen] = useState(false);
  const [selectedRequest, setSelectedRequest] =
    useState<FacultyLeaveRequest | null>(null);
  const [emailPreviewRequest, setEmailPreviewRequest] =
    useState<FacultyLeaveRequest | null>(null);
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  if (dashboardQuery.isLoading) {
    return (
      <div className="container py-10">
        <div className="rounded-lg border border-base-300 bg-base-100 p-8 text-center shadow-sm">
          <span className="loading loading-spinner loading-lg text-primary"></span>
          <p className="mt-4 text-sm font-semibold text-base-content/70">
            Loading your request history.
          </p>
        </div>
      </div>
    );
  }

  if (dashboardQuery.isError || !dashboardQuery.data) {
    return (
      <div className="container py-10">
        <PageErrorState
          badge="Request history"
          code="500"
          description="We could not load your request history right now."
          title="History unavailable"
        />
      </div>
    );
  }

  return (
    <HistoryContent
      data={dashboardQuery.data}
      emailPreviewRequest={emailPreviewRequest}
      onEmailPreviewRequest={setEmailPreviewRequest}
      onReportModalOpen={setReportModalOpen}
      onRequestSelected={setSelectedRequest}
      onSelectedTypeChange={setSelectedType}
      onToastMessage={setToastMessage}
      reportModalOpen={reportModalOpen}
      selectedRequest={selectedRequest}
      selectedType={selectedType}
      toastMessage={toastMessage}
    />
  );
}

function HistoryContent({
  data,
  emailPreviewRequest,
  onEmailPreviewRequest,
  onReportModalOpen,
  onRequestSelected,
  onSelectedTypeChange,
  onToastMessage,
  reportModalOpen,
  selectedRequest,
  selectedType,
  toastMessage,
}: {
  data: FacultyDashboardResponse;
  emailPreviewRequest: FacultyLeaveRequest | null;
  onEmailPreviewRequest: (request: FacultyLeaveRequest | null) => void;
  onReportModalOpen: (value: boolean) => void;
  onRequestSelected: (request: FacultyLeaveRequest | null) => void;
  onSelectedTypeChange: (value: string) => void;
  onToastMessage: (message: string | null) => void;
  reportModalOpen: boolean;
  selectedRequest: FacultyLeaveRequest | null;
  selectedType: string;
  toastMessage: string | null;
}) {
  const typeOptions = getReportLeaveTypeOptions(data.leaveTypes);
  const requests = selectedType
    ? data.recentRequests.filter(
        (request) => request.leaveType === selectedType
      )
    : data.recentRequests;

  return (
    <div className="container">
      <section className="mx-auto rounded-lg border border-base-300 bg-base-100 p-6 shadow-sm mt-10">
        <div className="mb-7 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <h1 className="text-lg font-bold text-primary">Request History</h1>
          <div className="flex flex-col gap-3 sm:flex-row">
            <select
              className="select select-bordered min-w-52"
              onChange={(event) => onSelectedTypeChange(event.target.value)}
              value={selectedType}
            >
              <option value="">All Types</option>
              {typeOptions.map((option) => (
                <option key={option.value} value={option.label}>
                  {option.label}
                </option>
              ))}
            </select>
            <button
              className={reportLeaveButtonClass}
              onClick={() => onReportModalOpen(true)}
              type="button"
            >
              Report Leave
            </button>
          </div>
        </div>

        <RequestHistoryTable
          key={selectedType}
          onSelectRequest={onRequestSelected}
          onViewEmail={onEmailPreviewRequest}
          requests={requests}
        />
      </section>

      {reportModalOpen ? (
        <ReportLeaveModal
          data={data}
          onClose={() => onReportModalOpen(false)}
          onSent={(message) => onToastMessage(message)}
        />
      ) : null}
      {selectedRequest ? (
        <RequestDetailModal
          faculty={data.faculty}
          onClose={() => onRequestSelected(null)}
          request={selectedRequest}
        />
      ) : null}
      {emailPreviewRequest ? (
        <ExistingRequestEmailPreviewModal
          faculty={data.faculty}
          onClose={() => onEmailPreviewRequest(null)}
          onPrimaryAction={() => onEmailPreviewRequest(null)}
          primaryLabel="Close"
          request={emailPreviewRequest}
          secondaryLabel={null}
        />
      ) : null}
      <FacultyToast
        message={toastMessage}
        onDismiss={() => onToastMessage(null)}
      />
    </div>
  );
}
