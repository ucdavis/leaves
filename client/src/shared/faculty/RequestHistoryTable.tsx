import { useMemo } from 'react';
import type { ColumnDef } from '@tanstack/react-table';
import type { FacultyLeaveRequest } from '@/queries/faculty.ts';
import { DataTable } from '@/shared/dataTable.tsx';
import {
  EmptyPanelMessage,
  RequestStatusBadge,
  formatCompactHours,
  formatDate,
  formatDateRange,
  getLeaveTone,
} from '@/shared/faculty/FacultyDashboardPanels.tsx';

export function RequestHistoryTable({
  onSelectRequest,
  onShowInCalendar,
  requests,
}: {
  onSelectRequest: (request: FacultyLeaveRequest) => void;
  onShowInCalendar?: (request: FacultyLeaveRequest) => void;
  requests: FacultyLeaveRequest[];
}) {
  const columns = useMemo<ColumnDef<FacultyLeaveRequest>[]>(
    () => [
      {
        accessorKey: 'submittedAt',
        cell: ({ getValue, row }) => {
          const submittedAt = getValue<string>();

          return (
            <button
              className="link link-hover font-medium"
              onClick={(event) => {
                event.stopPropagation();
                onSelectRequest(row.original);
              }}
              type="button"
            >
              {formatDate(submittedAt)}
            </button>
          );
        },
        header: 'Submitted',
      },
      {
        accessorKey: 'leaveType',
        cell: ({ getValue }) => {
          const value = getValue<string>();
          const tone = getLeaveTone(value);

          return (
            <span className="flex items-center gap-2">
              <span className={`h-2 w-2 rounded-full ${tone.dot}`} />
              {value}
            </span>
          );
        },
        header: 'Leave Type',
      },
      {
        accessorFn: (row) => `${row.startDate} ${row.endDate}`,
        cell: ({ row }) => {
          const { endDate, startDate } = row.original;
          return formatDateRange(startDate, endDate);
        },
        header: 'Date(s)',
        id: 'dateRange',
      },
      {
        accessorKey: 'totalHours',
        cell: ({ getValue }) => (
          <span className="font-bold">
            {formatCompactHours(getValue<number>())}
          </span>
        ),
        header: 'Hours',
      },
      {
        accessorKey: 'status',
        cell: ({ getValue }) => (
          <RequestStatusBadge status={getValue<string>()} />
        ),
        header: 'Status',
      },
      ...(onShowInCalendar
        ? [
            {
              cell: ({ row }) => (
                <button
                  className="btn btn-ghost btn-xs"
                  onClick={(event) => {
                    event.stopPropagation();
                    onShowInCalendar(row.original);
                  }}
                  type="button"
                >
                  Show in calendar
                </button>
              ),
              header: '',
              id: 'showInCalendar',
            } satisfies ColumnDef<FacultyLeaveRequest>,
          ]
        : []),
    ],
    [onSelectRequest, onShowInCalendar]
  );

  if (requests.length === 0) {
    return <EmptyPanelMessage message="No leave history is on file." />;
  }

  return (
    <DataTable
      columns={columns}
      data={requests}
      getRowProps={(row) => ({
        className:
          'cursor-pointer border-base-300 transition hover:bg-base-200',
        onClick: () => onSelectRequest(row.original),
      })}
      globalFilter="none"
      initialState={{
        pagination: {
          pageSize: 24,
        },
      }}
      tableClassName="table-zebra"
    />
  );
}
