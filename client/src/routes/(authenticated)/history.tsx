import { useQuery } from '@tanstack/react-query';
import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { HttpError } from '@/lib/api.ts';
import { useState } from 'react';
import { RouterContext } from '@/main.tsx';
import type {
  FacultyDashboardResponse,
  FacultyHistoryPageResponse,
  FacultyLeaveRequest,
} from '@/queries/faculty.ts';
import {
  facultyHistoryPageQueryOptions,
  facultyHistoryQueryOptions,
} from '@/queries/faculty.ts';
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
  const pageSize = 10;
  const [selectedType, setSelectedType] = useState('');
  const [page, setPage] = useState(1);
  const [reportModalOpen, setReportModalOpen] = useState(false);
  const [selectedRequest, setSelectedRequest] =
    useState<FacultyLeaveRequest | null>(null);
  const [toastMessage, setToastMessage] = useState<string | null>(null);
  const historyQuery = useQuery(
    facultyHistoryPageQueryOptions(
      page,
      pageSize,
      selectedType ? Number(selectedType) : undefined
    )
  );
  const reportHistoryQuery = useQuery({
    ...facultyHistoryQueryOptions(),
    enabled: reportModalOpen,
  });

  if (historyQuery.isLoading) {
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

  if (historyQuery.isError || !historyQuery.data) {
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
      data={historyQuery.data}
      onPageChange={setPage}
      onReportModalOpen={setReportModalOpen}
      onRequestSelected={setSelectedRequest}
      onSelectedTypeChange={(leaveType) => {
        setPage(1);
        setSelectedType(leaveType);
      }}
      onToastMessage={setToastMessage}
      page={page}
      pageSize={pageSize}
      reportData={reportHistoryQuery.data}
      reportDataError={reportHistoryQuery.isError}
      reportModalOpen={reportModalOpen}
      selectedRequest={selectedRequest}
      selectedType={selectedType}
      toastMessage={toastMessage}
    />
  );
}

function HistoryContent({
  data,
  onPageChange,
  onReportModalOpen,
  onRequestSelected,
  onSelectedTypeChange,
  onToastMessage,
  page,
  pageSize,
  reportModalOpen,
  reportData,
  reportDataError,
  selectedRequest,
  selectedType,
  toastMessage,
}: {
  data: FacultyHistoryPageResponse;
  onPageChange: (page: number) => void;
  onReportModalOpen: (value: boolean) => void;
  onRequestSelected: (request: FacultyLeaveRequest | null) => void;
  onSelectedTypeChange: (value: string) => void;
  onToastMessage: (message: string | null) => void;
  page: number;
  pageSize: number;
  reportData?: FacultyDashboardResponse;
  reportDataError: boolean;
  reportModalOpen: boolean;
  selectedRequest: FacultyLeaveRequest | null;
  selectedType: string;
  toastMessage: string | null;
}) {
  const navigate = useNavigate();
  const typeOptions = getReportLeaveTypeOptions(data.leaveTypes);
  return (
    <div className="container py-8 lg:py-10">
      <section className="mx-auto rounded-lg border border-base-300 bg-base-100 p-6 shadow-sm">
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
                <option key={option.value} value={option.value}>
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
          key={`${selectedType}-${page}`}
          onPageChange={onPageChange}
          onSelectRequest={onRequestSelected}
          onShowInCalendar={(request) =>
            void navigate({
              search: { calendarDate: request.startDate },
              to: '/',
            })
          }
          page={page}
          pageSize={pageSize}
          requests={data.requests}
          totalCount={data.totalCount}
        />
      </section>

      {reportModalOpen && reportData ? (
        <ReportLeaveModal
          data={reportData}
          onClose={() => onReportModalOpen(false)}
          onSent={(message) => onToastMessage(message)}
        />
      ) : null}
      {reportModalOpen && !reportData ? (
        <div className="fixed inset-0 z-50 grid place-items-center bg-neutral/40 p-4">
          <div className="rounded-lg bg-base-100 p-6 text-center shadow-xl">
            {reportDataError ? (
              <>
                <p className="font-semibold">Unable to load the leave form.</p>
                <button
                  className="btn btn-primary btn-sm mt-4"
                  onClick={() => onReportModalOpen(false)}
                  type="button"
                >
                  Close
                </button>
              </>
            ) : (
              <>
                <span className="loading loading-spinner loading-md text-primary" />
                <p className="mt-3 text-sm">Loading the leave report form.</p>
              </>
            )}
          </div>
        </div>
      ) : null}
      {selectedRequest ? (
        <RequestDetailModal
          faculty={data.faculty}
          onClose={() => onRequestSelected(null)}
          request={selectedRequest}
        />
      ) : null}
      <FacultyToast
        message={toastMessage}
        onDismiss={() => onToastMessage(null)}
      />
    </div>
  );
}
