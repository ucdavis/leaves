import { useState } from 'react';
import { z } from 'zod';
import type {
  AdminCluster,
  AdminDepartment,
} from '@/shared/admin/adminData.ts';
import { useAppForm } from '@/shared/forms/formContext.tsx';
import {
  statusSurfaceColors,
  statusTextColors,
} from '@/shared/statusColors.ts';
import { WarningModal } from '@/shared/WarningModal.tsx';
import { AdminModalFrame } from './AdminModalFrame.tsx';

const departmentSettingsSchema = z.object({
  approvalMode: z.enum(['approval', 'auto']),
  clusterId: z.string(),
  name: z
    .string()
    .trim()
    .min(1, 'Department name is required.')
    .max(100, 'Department name must be 100 characters or fewer.'),
});

const routingEmailSchema = z.object({
  address: z.email('Enter a valid email address.'),
  kind: z.enum(['to', 'cc']),
});

type RoutingEmailFormValues = z.infer<typeof routingEmailSchema>;

const defaultRoutingEmailValues: RoutingEmailFormValues = {
  address: '',
  kind: 'to',
};

export function DepartmentSettingsModal({
  clusters,
  department,
  formatError,
  isDeleting,
  onClose,
  onDelete,
  onGetPendingRequestCount,
  onRemoveRoutingEmail,
  onSave,
  onUpsertRoutingEmail,
}: {
  clusters: AdminCluster[];
  department: AdminDepartment;
  formatError: (error: unknown) => string;
  isDeleting: boolean;
  onClose: () => void;
  onDelete: () => Promise<void>;
  onGetPendingRequestCount: () => Promise<number>;
  onRemoveRoutingEmail: (emailId: string) => Promise<void>;
  onSave: (
    updates: Partial<
      Pick<AdminDepartment, 'approvalMode' | 'clusterId' | 'name'>
    > & {
      approvePendingRequests?: boolean;
    }
  ) => Promise<void>;
  onUpsertRoutingEmail: (email: {
    address: string;
    id?: string;
    kind: 'to' | 'cc';
  }) => Promise<void>;
}) {
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [isDeleteConfirmOpen, setIsDeleteConfirmOpen] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [routingEmailError, setRoutingEmailError] = useState<string | null>(
    null
  );
  const [pendingRemovalEmailId, setPendingRemovalEmailId] = useState<
    string | null
  >(null);
  const [pendingAutoApproveSave, setPendingAutoApproveSave] = useState<{
    approvalMode: 'approval' | 'auto';
    clusterId: string | null;
    name: string;
    pendingRequestCount: number;
  } | null>(null);
  const [isConfirmingAutoApprove, setIsConfirmingAutoApprove] = useState(false);

  const settingsForm = useAppForm({
    defaultValues: {
      approvalMode: department.approvalMode,
      clusterId: department.clusterId ?? '',
      name: department.name,
    },
    onSubmit: async ({ value }) => {
      setSaveError(null);

      const updates = {
        approvalMode: value.approvalMode,
        clusterId: value.clusterId || null,
        name: value.name.trim(),
      };

      if (
        department.approvalMode === 'approval' &&
        updates.approvalMode === 'auto'
      ) {
        try {
          const pendingRequestCount = await onGetPendingRequestCount();
          if (pendingRequestCount > 0) {
            setPendingAutoApproveSave({ ...updates, pendingRequestCount });
            return;
          }
        } catch (error) {
          setSaveError(formatError(error));
          return;
        }
      }

      try {
        await onSave(updates);
      } catch (error) {
        setSaveError(formatError(error));
      }
    },
    validators: {
      onChange: departmentSettingsSchema,
    },
  });

  const routingEmailForm = useAppForm({
    defaultValues: defaultRoutingEmailValues,
    onSubmit: async ({ value }) => {
      setRoutingEmailError(null);

      try {
        await onUpsertRoutingEmail({
          address: value.address.trim(),
          kind: value.kind,
        });
        routingEmailForm.reset();
      } catch (error) {
        setRoutingEmailError(formatError(error));
      }
    },
    validators: {
      onChange: routingEmailSchema,
    },
  });

  const handleRemoveEmail = async (emailId: string) => {
    setPendingRemovalEmailId(emailId);
    setRoutingEmailError(null);

    try {
      await onRemoveRoutingEmail(emailId);
    } catch (error) {
      setRoutingEmailError(formatError(error));
    } finally {
      setPendingRemovalEmailId(null);
    }
  };

  const handleDelete = async () => {
    setDeleteError(null);

    try {
      await onDelete();
      setIsDeleteConfirmOpen(false);
    } catch (error) {
      setDeleteError(formatError(error));
      setIsDeleteConfirmOpen(false);
    }
  };

  return (
    <>
      <AdminModalFrame
        maxWidthClassName="max-w-3xl"
        onRequestClose={onClose}
        title="Department settings"
      >
        <form
          onSubmit={(event) => {
            event.preventDefault();
            void settingsForm.handleSubmit();
          }}
        >
          <settingsForm.AppForm>
            <div className="mt-6 grid gap-4 sm:grid-cols-2">
              <settingsForm.AppField name="name">
                {(field) => (
                  <field.TextField
                    inputClassName="input input-bordered w-full bg-base-100"
                    label="Edit Department name"
                    placeholder="Enter department name"
                    type="text"
                  />
                )}
              </settingsForm.AppField>

              <settingsForm.AppField name="clusterId">
                {(field) => (
                  <field.SelectField
                    label="Cluster"
                    options={clusters.map((cluster) => ({
                      label: cluster.name,
                      value: cluster.id,
                    }))}
                    placeholder="No cluster"
                  />
                )}
              </settingsForm.AppField>

              <settingsForm.AppField name="approvalMode">
                {(field) => (
                  <field.SelectField
                    label="Approval mode"
                    options={[
                      {
                        label: 'Required',
                        value: 'approval',
                      },
                      {
                        label: 'Auto-approve',
                        value: 'auto',
                      },
                    ]}
                  />
                )}
              </settingsForm.AppField>
            </div>

            <div className="mt-6 rounded-2xl bg-base-200 p-5">
              <h3 className="text-sm font-semibold uppercase tracking-[0.2em] text-secondary">
                Request notification emails
              </h3>
              <p className="mt-2 text-sm leading-6 text-base-content/70">
                These addresses receive notification of approved leave
                requests for this department.
              </p>

              <div className="mt-4 space-y-3">
                {department.routingEmails.map((email) => (
                  <div
                    className="flex flex-col gap-3 rounded-xl border border-base-300 bg-base-100 px-4 py-3 sm:flex-row sm:items-center"
                    key={email.id}
                  >
                    <span className="badge border-0 bg-base-200 text-primary">
                      EMAIL
                    </span>
                    <span className="flex-1 text-sm text-base-content">
                      {email.address}
                    </span>
                    <button
                      className={`btn btn-ghost btn-sm ${statusTextColors.danger}`}
                      disabled={pendingRemovalEmailId === email.id}
                      onClick={() => {
                        void handleRemoveEmail(email.id);
                      }}
                      type="button"
                    >
                      {pendingRemovalEmailId === email.id ? (
                        <>
                          <span className="loading loading-spinner loading-xs mr-2"></span>
                          Removing...
                        </>
                      ) : (
                        'Remove'
                      )}
                    </button>
                  </div>
                ))}
              </div>

              {routingEmailError ? (
                <div
                  className={`mt-4 rounded-xl px-4 py-3 text-sm ${statusSurfaceColors.danger}`}
                >
                  {routingEmailError}
                </div>
              ) : null}

              <routingEmailForm.AppForm>
                <div className="mt-4 grid gap-3 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-end">
                  <div className="min-w-0">
                    <routingEmailForm.AppField name="address">
                      {(field) => (
                        <field.TextField
                          inputClassName="input input-bordered w-full bg-base-100"
                          label="Add request notification email"
                          placeholder="email@ucdavis.edu"
                          type="email"
                        />
                      )}
                    </routingEmailForm.AppField>
                  </div>
                  <routingEmailForm.SubscribeButton
                    className="btn btn-outline sm:self-end"
                    label="Add email"
                    loadingLabel="Adding..."
                    onClick={() => {
                      void routingEmailForm.handleSubmit();
                    }}
                    type="button"
                  />
                </div>
              </routingEmailForm.AppForm>
            </div>

            {saveError ? (
              <div
                className={`mt-6 rounded-xl px-4 py-3 text-sm ${statusSurfaceColors.danger}`}
              >
                {saveError}
              </div>
            ) : null}

            {deleteError ? (
              <div
                className={`mt-6 rounded-xl px-4 py-3 text-sm ${statusSurfaceColors.danger}`}
              >
                {deleteError}
              </div>
            ) : null}

            <div className="mt-6 flex flex-wrap items-center justify-between gap-3">
              <button
                className={`btn btn-outline border-rose-300 text-rose-800 hover:border-rose-400 hover:bg-rose-100 ${statusTextColors.danger}`}
                disabled={isDeleting}
                onClick={() => setIsDeleteConfirmOpen(true)}
                type="button"
              >
                Deactivate department
              </button>
              <div className="flex items-center gap-3">
                <button
                  className="btn btn-ghost"
                  onClick={onClose}
                  type="button"
                >
                  Cancel
                </button>
                <settingsForm.SubscribeButton
                  className="btn btn-primary"
                  label="Save changes"
                  loadingLabel="Saving..."
                />
              </div>
            </div>
          </settingsForm.AppForm>
        </form>
      </AdminModalFrame>

      {pendingAutoApproveSave ? (
        <WarningModal
          confirmLabel="Switch to Auto-approve"
          isSaving={isConfirmingAutoApprove}
          onCancel={() => setPendingAutoApproveSave(null)}
          onConfirm={() => {
            void (async () => {
              setIsConfirmingAutoApprove(true);
              try {
                await onSave({
                  approvalMode: pendingAutoApproveSave.approvalMode,
                  approvePendingRequests: true,
                  clusterId: pendingAutoApproveSave.clusterId,
                  name: pendingAutoApproveSave.name,
                });
                setPendingAutoApproveSave(null);
              } catch (error) {
                setSaveError(formatError(error));
                setPendingAutoApproveSave(null);
              } finally {
                setIsConfirmingAutoApprove(false);
              }
            })();
          }}
          title="Approve pending requests?"
        >
          Switching to Auto-approve will approve all{' '}
          <strong>{pendingAutoApproveSave.pendingRequestCount}</strong> pending{' '}
          request{pendingAutoApproveSave.pendingRequestCount === 1 ? '' : 's'}{' '}
          for this department.
        </WarningModal>
      ) : null}

      {isDeleteConfirmOpen ? (
        <WarningModal
          confirmLabel="Deactivate department"
          isSaving={isDeleting}
          onCancel={() => setIsDeleteConfirmOpen(false)}
          onConfirm={() => {
            void handleDelete();
          }}
          title="Deactivate department?"
        >
          <span>
            Deactivate <strong>{department.name}</strong>? Users will no longer
            be able to submit requests to this department.
          </span>
        </WarningModal>
      ) : null}
    </>
  );
}
