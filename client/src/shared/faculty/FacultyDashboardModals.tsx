import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { z } from 'zod';
import {
  createFacultyLeaveRequest,
  facultyDashboardQueryOptions,
  type FacultyDashboardResponse,
  type FacultyLeaveRequest,
} from '@/queries/faculty.ts';
import { statusSurfaceColors } from '@/shared/statusColors.ts';
import { useAppForm } from '@/shared/forms/formContext.tsx';
import { facultyLeaveTypeLabels } from './leaveTypes.ts';
import { Modal } from './FacultyDashboardModal.tsx';
import {
  formatCompactHours,
  formatDate,
  formatDateRange,
  getLeaveTone,
  isIsoDate,
  reportLeaveButtonClass,
} from './FacultyDashboardPanels.tsx';
import {
  DraftEmailPreviewModal,
  ExistingRequestEmailPreviewModal,
} from './FacultyDashboardEmailPreviews.tsx';
import { RequestStatusBadge } from './FacultyDashboardPanels.tsx';

export function getReportLeaveTypeOptions(
  leaveTypes: FacultyDashboardResponse['leaveTypes']
) {
  return facultyLeaveTypeLabels.flatMap((label) => {
    const matchingType = leaveTypes.find(
      (type) => type.displayName === label
    );

    return matchingType
      ? [
          {
            label,
            value: String(matchingType.id),
          },
        ]
      : [];
  });
}

const leaveRequestSchema = z
  .object({
    dateSelection: z.enum(['single', 'range']),
    endDate: z.string(),
    leaveTypeId: z.string().min(1, 'Select a leave type.'),
    note: z.string().trim().max(1000, 'Note is too long.'),
    startDate: z.string(),
    totalHours: z
      .string()
      .min(1, 'Total hours are required.')
      .refine((value) => Number(value) > 0, 'Hours must be greater than zero.')
      .refine((value) => Number(value) <= 240, 'Hours must be 240 or fewer.'),
  })
  .superRefine((value, context) => {
    const dateMessage = 'Select a date.';
    const dateRangeMessage = 'Use a valid date.';

    if (!value.startDate) {
      context.addIssue({
        code: 'custom',
        message:
          value.dateSelection === 'single'
            ? dateMessage
            : 'Select a start date.',
        path: ['startDate'],
      });
    } else if (!isIsoDate(value.startDate)) {
      context.addIssue({
        code: 'custom',
        message: dateRangeMessage,
        path: ['startDate'],
      });
    }

    if (value.dateSelection === 'range') {
      if (!value.endDate) {
        context.addIssue({
          code: 'custom',
          message: 'Select an end date.',
          path: ['endDate'],
        });
      } else if (!isIsoDate(value.endDate)) {
        context.addIssue({
          code: 'custom',
          message: dateRangeMessage,
          path: ['endDate'],
        });
      } else if (value.startDate && value.endDate <= value.startDate) {
        context.addIssue({
          code: 'custom',
          message: 'End date must be after the start date.',
          path: ['endDate'],
        });
      }
    }
  });

type LeaveRequestFormValues = z.infer<typeof leaveRequestSchema>;
type LeaveRequestDraft = {
  endDate: string;
  leaveTypeId: number;
  leaveTypeLabel: string;
  note: string | null;
  startDate: string;
  totalHours: number;
};

export function RequestDetailModal({
  faculty,
  onClose,
  request,
}: {
  faculty: FacultyDashboardResponse['faculty'];
  onClose: () => void;
  request: FacultyLeaveRequest;
}) {
  const [emailPreviewOpen, setEmailPreviewOpen] = useState(false);

  return (
    <>
      <Modal onClose={onClose} title="Request Detail">
        <div className="space-y-5">
          <RequestDetailHeader request={request} />
          <RequestDetailGrid faculty={faculty} request={request} />
          <RequestNote note={request.note} />
          <div className="flex justify-end gap-3">
            <button
              className="btn btn-outline btn-primary"
              onClick={() => setEmailPreviewOpen(true)}
              type="button"
            >
              View Email
            </button>
            <button className="btn btn-ghost" onClick={onClose} type="button">
              Close
            </button>
          </div>
        </div>
      </Modal>

      {emailPreviewOpen ? (
        <ExistingRequestEmailPreviewModal
          faculty={faculty}
          onClose={() => setEmailPreviewOpen(false)}
          onPrimaryAction={() => setEmailPreviewOpen(false)}
          primaryLabel="Close"
          request={request}
          secondaryLabel={null}
        />
      ) : null}
    </>
  );
}

function RequestDetailHeader({ request }: { request: FacultyLeaveRequest }) {
  const tone = getLeaveTone(request.leaveType);

  return (
    <div className="flex w-full items-start justify-between gap-4">
      <div className="flex items-center gap-2 font-bold">
        <span className={`h-2.5 w-2.5 rounded-full ${tone.dot}`} />
        {request.leaveType}
      </div>
      <RequestStatusBadge status={request.status} />
    </div>
  );
}

function RequestDetailGrid({
  faculty,
  request,
}: {
  faculty: FacultyDashboardResponse['faculty'];
  request: FacultyLeaveRequest;
}) {
  return (
    <dl className="grid gap-5 text-sm sm:grid-cols-2">
      <RequestDetailItem label="Faculty" value={faculty.name} />
      <RequestDetailItem label="Department" value={request.departmentName} />
      <RequestDetailItem
        label="Date(s)"
        value={formatDateRange(request.startDate, request.endDate)}
      />
      <RequestDetailItem
        label="Hours"
        value={formatCompactHours(request.totalHours)}
      />
      <RequestDetailItem
        label="Submitted"
        value={formatDate(request.submittedAt)}
      />
      <RequestDetailItem label="Request ID" value={`r${request.id}`} />
    </dl>
  );
}

function RequestDetailItem({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs font-bold uppercase tracking-[0.12em] text-base-content/60">
        {label}
      </dt>
      <dd className="mt-1 font-medium">{value}</dd>
    </div>
  );
}

function RequestNote({ note }: { note?: string | null }) {
  return (
    <div className="rounded-lg bg-base-200 p-4">
      <div className="text-xs font-bold uppercase tracking-[0.12em] text-base-content/60">
        Note
      </div>
      <p className="mt-2 text-sm">{note?.trim() || 'No note provided.'}</p>
    </div>
  );
}

export function ReportLeaveModal({
  data,
  onClose,
  onSent,
}: {
  data: FacultyDashboardResponse;
  onClose: () => void;
  onSent: (message: string) => void;
}) {
  return (
    <Modal onClose={onClose} title="Report Leave Taken">
      <LeaveRequestForm
        data={data}
        onClose={onClose}
        onSent={onSent}
        onSubmitted={onClose}
      />
    </Modal>
  );
}

function LeaveRequestForm({
  data,
  onClose,
  onSent,
  onSubmitted,
}: {
  data: FacultyDashboardResponse;
  onClose: () => void;
  onSent: (message: string) => void;
  onSubmitted: () => void;
}) {
  const queryClient = useQueryClient();
  const [pendingDraft, setPendingDraft] = useState<LeaveRequestDraft | null>(
    null
  );
  const requestMutation = useMutation({
    mutationFn: createFacultyLeaveRequest,
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: facultyDashboardQueryOptions().queryKey,
      });
      onSent(
        'Email simulated successfully. In production, this sends to AggieService.'
      );
      onSubmitted();
    },
  });

  const leaveTypeOptions = getReportLeaveTypeOptions(data.leaveTypes);
  const defaultValues: LeaveRequestFormValues = {
    dateSelection: 'single',
    endDate: '',
    leaveTypeId: '',
    note: '',
    startDate: '',
    totalHours: '',
  };

  const form = useAppForm({
    defaultValues,
    onSubmit: async ({ value }) => {
      const endDate =
        value.dateSelection === 'single' ? value.startDate : value.endDate;
      const leaveTypeLabel =
        leaveTypeOptions.find((option) => option.value === value.leaveTypeId)
          ?.label ?? 'Leave';

      setPendingDraft({
        endDate,
        leaveTypeId: Number(value.leaveTypeId),
        leaveTypeLabel,
        note: value.note.trim() || null,
        startDate: value.startDate,
        totalHours: Number(value.totalHours),
      });
    },
    validators: {
      onChange: leaveRequestSchema,
    },
  });

  const handleSimulateSend = async () => {
    if (!pendingDraft) {
      return;
    }

    await requestMutation.mutateAsync({
      coveragePlan: null,
      endDate: pendingDraft.endDate,
      leaveTypeId: pendingDraft.leaveTypeId,
      note: pendingDraft.note,
      payLeaveTypeId: null,
      startDate: pendingDraft.startDate,
      totalHours: pendingDraft.totalHours,
    });
    setPendingDraft(null);
    form.reset();
  };

  return (
    <>
      <form
        className="space-y-4"
        onSubmit={(event) => {
          event.preventDefault();
          void form.handleSubmit();
        }}
      >
        <form.AppForm>
          <FacultySummary faculty={data.faculty} />

          <div className="grid gap-4">
            <form.AppField name="leaveTypeId">
              {(field) => (
                <field.SelectField
                  label="Type of Leave"
                  options={leaveTypeOptions}
                  placeholder="Select..."
                  required
                />
              )}
            </form.AppField>

            <form.AppField name="dateSelection">
              {(field) => (
                <fieldset className="form-control">
                  <legend className="label pb-1">
                    <span className="label-text font-medium">
                      Date Selection
                    </span>
                  </legend>
                  <div className="flex flex-wrap gap-5">
                    <label className="flex cursor-pointer items-center gap-2 text-sm">
                      <input
                        checked={field.state.value === 'single'}
                        className="radio radio-primary radio-sm"
                        name={field.name}
                        onBlur={field.handleBlur}
                        onChange={() => {
                          field.handleChange('single');
                          form.setFieldValue('endDate', '');
                        }}
                        type="radio"
                        value="single"
                      />
                      <span>Single Day</span>
                    </label>
                    <label className="flex cursor-pointer items-center gap-2 text-sm">
                      <input
                        checked={field.state.value === 'range'}
                        className="radio radio-primary radio-sm"
                        name={field.name}
                        onBlur={field.handleBlur}
                        onChange={() => field.handleChange('range')}
                        type="radio"
                        value="range"
                      />
                      <span>Date Range</span>
                    </label>
                  </div>
                </fieldset>
              )}
            </form.AppField>

            <form.Subscribe selector={(state) => state.values.dateSelection}>
              {(dateSelection) =>
                dateSelection === 'single' ? (
                  <form.AppField name="startDate">
                    {(field) => (
                      <field.TextField
                        label="Leave Date"
                        required
                        type="date"
                      />
                    )}
                  </form.AppField>
                ) : (
                  <div className="grid gap-4 sm:grid-cols-2">
                    <form.AppField name="startDate">
                      {(field) => (
                        <field.TextField
                          label="Range Start Date"
                          required
                          type="date"
                        />
                      )}
                    </form.AppField>
                    <form.AppField name="endDate">
                      {(field) => (
                        <field.TextField
                          label="Range End Date"
                          required
                          type="date"
                        />
                      )}
                    </form.AppField>
                  </div>
                )
              }
            </form.Subscribe>

            <form.AppField name="totalHours">
              {(field) => (
                <field.TextField
                  label="Total Hours"
                  placeholder="e.g., 8"
                  required
                />
              )}
            </form.AppField>
            <form.AppField name="note">
              {(field) => (
                <field.TextAreaField
                  label="Note (optional)"
                  placeholder="Any additional context..."
                />
              )}
            </form.AppField>
          </div>

          {requestMutation.isError ? (
            <div
              className={`mt-4 rounded-lg px-4 py-3 text-sm ${statusSurfaceColors.danger}`}
            >
              The request could not be submitted. Check the fields and try
              again.
            </div>
          ) : null}

          <div className="flex justify-end gap-3 pt-2">
            <button
              className="btn btn-outline btn-primary min-w-24"
              onClick={onClose}
              type="button"
            >
              Cancel
            </button>
            <form.SubscribeButton
              className={`${reportLeaveButtonClass} min-w-44`}
              label="Submit Leave Report"
              loadingLabel="Submitting"
            />
          </div>
        </form.AppForm>
      </form>

      {pendingDraft ? (
        <DraftEmailPreviewModal
          draft={pendingDraft}
          faculty={data.faculty}
          onClose={() => setPendingDraft(null)}
          onPrimaryAction={() => void handleSimulateSend()}
          onSecondaryAction={() => setPendingDraft(null)}
          primaryLabel="Simulate Send"
          primaryLoading={requestMutation.isPending}
          secondaryLabel="Cancel"
        />
      ) : null}
    </>
  );
}

function FacultySummary({
  faculty,
}: {
  faculty: FacultyDashboardResponse['faculty'];
}) {
  const details = [
    faculty.email,
    faculty.departmentName ?? faculty.departmentCode,
    faculty.employeeClass ?? faculty.jobTitle,
  ].filter(Boolean);

  return (
    <div className="rounded-lg bg-base-200 px-4 py-3 text-sm text-base-content/70">
      <span className="font-bold text-base-content">{faculty.name}</span>
      {details.length > 0 ? ` · ${details.join(' · ')}` : null}
    </div>
  );
}
