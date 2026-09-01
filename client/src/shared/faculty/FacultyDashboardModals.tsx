import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState, type ReactNode } from 'react';
import { z } from 'zod';
import { HttpError } from '@/lib/api.ts';
import {
  createFacultyLeaveRequest,
  type FacultyDashboardResponse,
  type FacultyLeaveRequest,
} from '@/queries/faculty.ts';
import { useAppForm } from '@/shared/forms/formContext.tsx';
import {
  facultyLeaveTypeLabels,
  fmlaLeaveTypeLabel,
  professionalDevelopmentLeaveTypeLabel,
  sabbaticalLeaveTypeLabel,
  sickLeaveTypeLabel,
  vacationLeaveTypeLabel,
  getFacultyLeaveTypeKey,
} from './leaveTypes.ts';
import { Modal } from './FacultyDashboardModal.tsx';
import {
  formatCompactHours,
  formatDate,
  formatDateRange,
  getLeaveTone,
  isIsoDate,
  reportLeaveButtonClass,
} from './FacultyDashboardPanels.tsx';
import { RequestStatusBadge } from './FacultyDashboardPanels.tsx';
import { getValidationErrorMessage } from '@/shared/forms/validationError.ts';

const myInfoVaultUrl = 'https://myinfovault.ucdavis.edu/';
const noPayOptionValue = 'none';

export function getReportLeaveTypeOptions(
  leaveTypes: FacultyDashboardResponse['leaveTypes']
) {
  return facultyLeaveTypeLabels.flatMap((label) => {
    const matchingType = leaveTypes.find((type) => type.displayName === label);

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

type LeaveRequestFormValues = {
  approvedInMyInfoVault: boolean;
  dateSelection: 'single' | 'range';
  endDate: string;
  leaveTypeId: string;
  note: string;
  payLeaveTypeId: string;
  startDate: string;
  totalHours: string;
};

function createLeaveRequestSchema(leaveTypeLabelById: Map<string, string>) {
  return z
    .object({
      approvedInMyInfoVault: z.boolean(),
      dateSelection: z.enum(['single', 'range']),
      endDate: z.string(),
      leaveTypeId: z.string().min(1, 'Select a leave type.'),
      note: z.string().trim().max(1000, 'Note is too long.'),
      payLeaveTypeId: z.string(),
      startDate: z.string(),
      totalHours: z.string(),
    })
    .superRefine((value, context) => {
      const selectedLeaveType = getSelectedLeaveTypeLabel(
        value.leaveTypeId,
        leaveTypeLabelById
      );
      const requiresApproval =
        selectedLeaveType === sabbaticalLeaveTypeLabel ||
        selectedLeaveType === fmlaLeaveTypeLabel;
      const usesDateRange =
        selectedLeaveType === sabbaticalLeaveTypeLabel ||
        value.dateSelection === 'range';
      const requiresHours =
        selectedLeaveType !== professionalDevelopmentLeaveTypeLabel &&
        selectedLeaveType !== sabbaticalLeaveTypeLabel;
      const dateMessage = 'Select a date.';
      const dateRangeMessage = 'Use a valid date.';

      if (!value.startDate) {
        context.addIssue({
          code: 'custom',
          message: usesDateRange ? 'Select a start date.' : dateMessage,
          path: ['startDate'],
        });
      } else if (!isIsoDate(value.startDate)) {
        context.addIssue({
          code: 'custom',
          message: dateRangeMessage,
          path: ['startDate'],
        });
      }

      if (usesDateRange) {
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
        } else if (value.startDate && value.endDate < value.startDate) {
          context.addIssue({
            code: 'custom',
            message: 'End date must be after the start date.',
            path: ['endDate'],
          });
        }
      }

      if (requiresHours) {
        if (!value.totalHours) {
          context.addIssue({
            code: 'custom',
            message: 'Total hours are required.',
            path: ['totalHours'],
          });
        } else if (Number(value.totalHours) <= 0) {
          context.addIssue({
            code: 'custom',
            message: 'Hours must be greater than zero.',
            path: ['totalHours'],
          });
        } else if (Number(value.totalHours) > 240) {
          context.addIssue({
            code: 'custom',
            message: 'Hours must be 240 or fewer.',
            path: ['totalHours'],
          });
        }
      }

      if (requiresApproval && !value.approvedInMyInfoVault) {
        context.addIssue({
          code: 'custom',
          message: 'Confirm approval in MyInfoVault before recording leave.',
          path: ['approvedInMyInfoVault'],
        });
      }
    });
}

export function RequestDetailModal({
  faculty,
  onClose,
  request,
}: {
  faculty: FacultyDashboardResponse['faculty'];
  onClose: () => void;
  request: FacultyLeaveRequest;
}) {
  return (
    <Modal onClose={onClose} title="Request Detail">
      <div className="space-y-5">
        <RequestDetailHeader request={request} />
        <RequestDetailGrid faculty={faculty} request={request} />
        <RequestNote note={request.note} />
        <div className="flex justify-end gap-3">
          <button className="btn btn-ghost" onClick={onClose} type="button">
            Close
          </button>
        </div>
      </div>
    </Modal>
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
  const [title, setTitle] = useState('Report Leave Taken');

  return (
    <Modal onClose={onClose} title={title}>
      <LeaveRequestForm
        data={data}
        onClose={onClose}
        onSent={onSent}
        onSubmitted={onClose}
        onTitleChange={setTitle}
      />
    </Modal>
  );
}

function LeaveRequestForm({
  data,
  onClose,
  onSent,
  onSubmitted,
  onTitleChange,
}: {
  data: FacultyDashboardResponse;
  onClose: () => void;
  onSent: (message: string) => void;
  onSubmitted: () => void;
  onTitleChange: (title: string) => void;
}) {
  const queryClient = useQueryClient();
  const [submitError, setSubmitError] = useState<string | null>(null);
  const leaveTypeOptions = getReportLeaveTypeOptions(data.leaveTypes);
  const leaveTypeLabelById = new Map(
    leaveTypeOptions.map((option) => [option.value, option.label])
  );
  const payTypeOptions = getFmlaPayTypeOptions(data);
  const defaultValues: LeaveRequestFormValues = {
    approvedInMyInfoVault: false,
    dateSelection: 'single',
    endDate: '',
    leaveTypeId: '',
    note: '',
    payLeaveTypeId: '',
    startDate: '',
    totalHours: '',
  };
  const requestMutation = useMutation({
    mutationFn: createFacultyLeaveRequest,
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: ['faculty'],
      });
    },
  });

  const form = useAppForm({
    defaultValues,
    onSubmit: async ({ value }) => {
      const overlapError = getOverlapValidationError(
        value,
        data.recentRequests,
        leaveTypeLabelById
      );

      if (overlapError) {
        setSubmitError(overlapError.form);
        return;
      }

      setSubmitError(null);

      const selectedLeaveType = getSelectedLeaveTypeLabel(
        value.leaveTypeId,
        leaveTypeLabelById
      );
      const usesDateRange =
        selectedLeaveType === sabbaticalLeaveTypeLabel ||
        value.dateSelection === 'range';
      const totalHours =
        selectedLeaveType === professionalDevelopmentLeaveTypeLabel ||
        selectedLeaveType === sabbaticalLeaveTypeLabel
          ? 0
          : Number(value.totalHours);
      const payLeaveTypeId =
        value.payLeaveTypeId && value.payLeaveTypeId !== noPayOptionValue
          ? Number(value.payLeaveTypeId)
          : null;
      try {
        await requestMutation.mutateAsync({
          coveragePlan: null,
          endDate: usesDateRange ? value.endDate : value.startDate,
          leaveTypeId: Number(value.leaveTypeId),
          note: value.note.trim() || null,
          payLeaveTypeId,
          startDate: value.startDate,
          totalHours,
        });
        form.reset();
        onTitleChange('Report Leave Taken');
        onSent(getSuccessMessage(selectedLeaveType));
        onSubmitted();
      } catch (error) {
        const errorMap = getSubmitErrorMap(error);
        form.setErrorMap({ onSubmit: errorMap });
        setSubmitError(errorMap.form ?? null);
      }
    },
    validators: {
      onChange: createLeaveRequestSchema(leaveTypeLabelById),
    },
  });

  const selectedLeaveType = getSelectedLeaveTypeLabel(
    form.state.values.leaveTypeId,
    leaveTypeLabelById
  );
  const usesDateRange =
    selectedLeaveType === sabbaticalLeaveTypeLabel ||
    form.state.values.dateSelection === 'range';
  const requiresHours =
    selectedLeaveType !== professionalDevelopmentLeaveTypeLabel &&
    selectedLeaveType !== sabbaticalLeaveTypeLabel;

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
                <div className="form-control w-full">
                  <label className="label">
                    <span className="label-text font-medium">
                      Type of Leave
                      <span className="text-error"> *</span>
                    </span>
                  </label>
                  <select
                    aria-required
                    className={`select select-bordered w-full ${
                      field.state.meta.errors.length > 0 ? 'select-error' : ''
                    }`}
                    onBlur={field.handleBlur}
                    onChange={(event) => {
                      const nextLeaveTypeId = event.target.value;
                      const nextLeaveType = getSelectedLeaveTypeLabel(
                        nextLeaveTypeId,
                        leaveTypeLabelById
                      );

                      field.handleChange(nextLeaveTypeId);
                      form.setFieldValue('approvedInMyInfoVault', false);
                      form.setFieldValue('dateSelection', 'single');
                      form.setFieldValue('endDate', '');
                      form.setFieldValue('payLeaveTypeId', '');
                      form.setFieldValue('totalHours', '');

                      if (nextLeaveType === sabbaticalLeaveTypeLabel) {
                        form.setFieldValue('dateSelection', 'range');
                      }

                      onTitleChange(getModalTitle(nextLeaveType));
                    }}
                    value={field.state.value}
                  >
                    <option value="">Select...</option>
                    {leaveTypeOptions.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </select>
                  {field.state.meta.errors.length > 0 ? (
                    <label className="label">
                      <span className="label-text-alt text-error" role="alert">
                        {field.state.meta.errors
                          .map(getValidationErrorMessage)
                          .join(', ')}
                      </span>
                    </label>
                  ) : null}
                </div>
              )}
            </form.AppField>

            {selectedLeaveType ? (
              <LeaveTypeNotice selectedLeaveType={selectedLeaveType} />
            ) : null}

            {selectedLeaveType === sabbaticalLeaveTypeLabel ||
            selectedLeaveType === fmlaLeaveTypeLabel ? (
              <form.AppField name="approvedInMyInfoVault">
                {(field) => (
                  <field.CheckboxField
                    label={`I confirm this ${getMyInfoVaultSubject(
                      selectedLeaveType
                    )} has been approved in MyInfoVault`}
                  />
                )}
              </form.AppField>
            ) : null}

            {selectedLeaveType === fmlaLeaveTypeLabel ? (
              <form.AppField name="payLeaveTypeId">
                {(field) => (
                  <field.SelectField
                    label="Pay Type (optional)"
                    options={payTypeOptions}
                    placeholder="Select..."
                  />
                )}
              </form.AppField>
            ) : null}

            {selectedLeaveType === sabbaticalLeaveTypeLabel ? (
              <div className="text-sm font-medium text-base-content">
                Sabbatical Period
              </div>
            ) : (
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
            )}

            {usesDateRange ? (
              <div className="grid gap-4 sm:grid-cols-2">
                <form.AppField name="startDate">
                  {(field) => (
                    <field.TextField
                      label={
                        selectedLeaveType === sabbaticalLeaveTypeLabel
                          ? 'Start Date'
                          : 'Range Start Date'
                      }
                      required
                      type="date"
                    />
                  )}
                </form.AppField>
                <form.AppField name="endDate">
                  {(field) => (
                    <field.TextField
                      label={
                        selectedLeaveType === sabbaticalLeaveTypeLabel
                          ? 'End Date'
                          : 'Range End Date'
                      }
                      required
                      type="date"
                    />
                  )}
                </form.AppField>
              </div>
            ) : (
              <form.AppField name="startDate">
                {(field) => (
                  <field.TextField label="Leave Date" required type="date" />
                )}
              </form.AppField>
            )}

            {requiresHours ? (
              <form.AppField name="totalHours">
                {(field) => (
                  <field.TextField
                    label="Total Hours"
                    placeholder="e.g., 8"
                    required
                  />
                )}
              </form.AppField>
            ) : null}

            <form.AppField name="note">
              {(field) => (
                <field.TextAreaField
                  label="Note (optional)"
                  placeholder="Any additional context..."
                />
              )}
            </form.AppField>
          </div>

          {submitError ? (
            <div
              className="rounded-lg border border-error/30 bg-error/10 px-4 py-3 text-sm text-error"
              role="alert"
            >
              {submitError}
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
            <button
              className={`${reportLeaveButtonClass} min-w-44`}
              disabled={requestMutation.isPending}
              type="submit"
            >
              {requestMutation.isPending
                ? 'Submitting'
                : getSubmitLabel(selectedLeaveType)}
            </button>
          </div>
        </form.AppForm>
      </form>
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

function getSelectedLeaveTypeLabel(
  leaveTypeId: string,
  leaveTypeLabelById: Map<string, string>
) {
  return leaveTypeLabelById.get(leaveTypeId) ?? '';
}

function getModalTitle(selectedLeaveType: string) {
  if (selectedLeaveType === professionalDevelopmentLeaveTypeLabel) {
    return 'Report Professional Development';
  }

  if (
    selectedLeaveType === sabbaticalLeaveTypeLabel ||
    selectedLeaveType === fmlaLeaveTypeLabel
  ) {
    return 'Record Approved Leave';
  }

  return 'Report Leave Taken';
}

function getSubmitLabel(selectedLeaveType: string) {
  if (selectedLeaveType === professionalDevelopmentLeaveTypeLabel) {
    return 'Submit Notification';
  }

  if (
    selectedLeaveType === sabbaticalLeaveTypeLabel ||
    selectedLeaveType === fmlaLeaveTypeLabel
  ) {
    return 'Record Approved Leave';
  }

  return 'Submit Leave Report';
}

function getOverlapValidationError(
  value: LeaveRequestFormValues,
  requests: FacultyLeaveRequest[],
  leaveTypeLabelById: Map<string, string>
): { form: string } | undefined {
  const selectedLeaveType = getSelectedLeaveTypeLabel(
    value.leaveTypeId,
    leaveTypeLabelById
  );
  const usesDateRange =
    selectedLeaveType === sabbaticalLeaveTypeLabel ||
    value.dateSelection === 'range';
  const overlapRequest = findOverlappingActiveRequest(
    requests,
    value.startDate,
    usesDateRange ? value.endDate : value.startDate
  );

  if (!overlapRequest) {
    return undefined;
  }

  return {
    form: buildOverlapMessage(overlapRequest),
  };
}

function getSuccessMessage(selectedLeaveType: string) {
  if (selectedLeaveType === professionalDevelopmentLeaveTypeLabel) {
    return 'Professional development notification submitted.';
  }

  if (
    selectedLeaveType === sabbaticalLeaveTypeLabel ||
    selectedLeaveType === fmlaLeaveTypeLabel
  ) {
    return 'Approved leave recorded successfully.';
  }

  return 'Leave report submitted successfully.';
}

function getMyInfoVaultSubject(selectedLeaveType: string) {
  return selectedLeaveType === sabbaticalLeaveTypeLabel
    ? 'sabbatical'
    : 'FMLA leave';
}

function getFmlaPayTypeOptions(data: FacultyDashboardResponse) {
  const leaveTypeIdByLabel = new Map(
    data.leaveTypes.map((leaveType) => [
      getFacultyLeaveTypeKey(leaveType.displayName),
      leaveType.id,
    ])
  );
  const balancesByLabel = new Map(
    data.accrualBalances.map((balance) => [
      getFacultyLeaveTypeKey(balance.typeLabel),
      balance,
    ])
  );

  return [
    {
      label: 'No pay / decide later',
      value: noPayOptionValue,
    },
    createPayTypeOption(
      vacationLeaveTypeLabel,
      leaveTypeIdByLabel,
      balancesByLabel
    ),
    createPayTypeOption(
      sickLeaveTypeLabel,
      leaveTypeIdByLabel,
      balancesByLabel
    ),
  ].flatMap((option) => (option ? [option] : []));
}

function createPayTypeOption(
  leaveTypeLabel: string,
  leaveTypeIdByLabel: Map<string, number>,
  balancesByLabel: Map<
    string,
    FacultyDashboardResponse['accrualBalances'][number]
  >
) {
  const leaveTypeKey = getFacultyLeaveTypeKey(leaveTypeLabel);
  const leaveTypeId = leaveTypeIdByLabel.get(leaveTypeKey);
  const balance = balancesByLabel.get(leaveTypeKey);

  if (!leaveTypeId || !balance) {
    return null;
  }

  return {
    label: `${leaveTypeLabel.replace(' Leave', '')} (${formatBalanceHours(
      balance.calculatedBalance
    )} available)`,
    value: String(leaveTypeId),
  };
}

function formatBalanceHours(hours: number) {
  return Number.isInteger(hours) ? `${hours}h` : `${hours.toFixed(2)}h`;
}

function getSubmitErrorMap(error: unknown) {
  if (
    error instanceof HttpError &&
    error.status === 400 &&
    isValidationProblemDetails(error.body)
  ) {
    const fields = Object.fromEntries(
      Object.entries(error.body.errors).map(([fieldName, messages]) => [
        toClientFieldName(fieldName),
        messages.join(', '),
      ])
    );

    return {
      fields,
      form: getFirstValidationMessage(error.body.errors),
    };
  }

  return {
    fields: {},
    form: 'The leave request could not be submitted. Please review the form and try again.',
  };
}

function isValidationProblemDetails(
  value: unknown
): value is { errors: Record<string, string[]> } {
  if (!value || typeof value !== 'object' || !('errors' in value)) {
    return false;
  }

  return typeof value.errors === 'object' && value.errors !== null;
}

function getFirstValidationMessage(errors: Record<string, string[]>) {
  return (
    Object.values(errors)
      .flat()
      .find((message) => message.length > 0) ?? null
  );
}

function findOverlappingActiveRequest(
  requests: FacultyLeaveRequest[],
  startDate: string,
  endDate: string
) {
  return requests.find(
    (request) =>
      isActiveRequestStatus(request.status) &&
      request.startDate <= endDate &&
      request.endDate >= startDate
  );
}

function buildOverlapMessage(request: FacultyLeaveRequest) {
  return `This overlaps with your ${request.leaveType} request (${formatDateRange(
    request.startDate,
    request.endDate
  )}, ${formatCompactHours(request.totalHours)}, request r${request.id}).`;
}

function isActiveRequestStatus(status: string) {
  const normalized = status.toLowerCase();
  return normalized.includes('approved') || normalized.includes('pending');
}

function toClientFieldName(fieldName: string) {
  return fieldName.length > 0
    ? `${fieldName[0]!.toLowerCase()}${fieldName.slice(1)}`
    : fieldName;
}

function LeaveTypeNotice({ selectedLeaveType }: { selectedLeaveType: string }) {
  if (selectedLeaveType === professionalDevelopmentLeaveTypeLabel) {
    return (
      <NoticePanel tone="info">
        Professional Development is informational only - no hours will be
        deducted from your balance.
      </NoticePanel>
    );
  }

  if (selectedLeaveType === sabbaticalLeaveTypeLabel) {
    return (
      <NoticePanel tone="danger">
        <span className="font-semibold">
          Sabbatical must be approved in{' '}
          <a
            className="link link-primary"
            href={myInfoVaultUrl}
            rel="noreferrer"
            target="_blank"
          >
            MyInfoVault
          </a>{' '}
          before entering here.
        </span>{' '}
        Enter your approved sabbatical dates below. This will automatically set
        up a monthly debit of 16 hours from your vacation balance for the
        duration of the sabbatical.
      </NoticePanel>
    );
  }

  if (selectedLeaveType === fmlaLeaveTypeLabel) {
    return (
      <NoticePanel tone="warning">
        <span className="font-semibold">
          FMLA must be approved in{' '}
          <a
            className="link link-primary"
            href={myInfoVaultUrl}
            rel="noreferrer"
            target="_blank"
          >
            MyInfoVault
          </a>{' '}
          before entering here.
        </span>{' '}
        Use this form to record your approved FMLA leave for calendar and
        tracking purposes. Optionally choose a leave balance to be paid from.
      </NoticePanel>
    );
  }

  return null;
}

function NoticePanel({
  children,
  tone,
}: {
  children: ReactNode;
  tone: 'danger' | 'info' | 'warning';
}) {
  const toneClasses =
    tone === 'danger'
      ? 'border-error/40 bg-error/10 text-base-content'
      : tone === 'warning'
        ? 'border-warning bg-warning/10 text-base-content'
        : 'border-primary/20 bg-primary/10 text-base-content';

  return (
    <div className={`rounded-lg border px-4 py-3 text-sm ${toneClasses}`}>
      {children}
    </div>
  );
}
