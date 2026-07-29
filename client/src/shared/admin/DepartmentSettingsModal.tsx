import { useState } from 'react';
import { z } from 'zod';
import type {
  AdminCluster,
  AdminDepartment,
} from '@/shared/admin/adminData.tsx';
import { useAppForm } from '@/shared/forms/formContext.tsx';
import {
  statusSurfaceColors,
  statusTextColors,
} from '@/shared/statusColors.ts';
import { WarningModal } from '@/shared/WarningModal.tsx';
import { AdminModalFrame } from './AdminModalFrame.tsx';

const departmentSettingsSchema = z.object({
  approvalMode: z.enum(['notification', 'approval', 'auto']),
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
  onRemoveRoutingEmail: (emailId: string) => Promise<void>;
  onSave: (
    updates: Partial<Pick<AdminDepartment, 'approvalMode' | 'clusterId' | 'name'>>
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

  const settingsForm = useAppForm({
    defaultValues: {
      approvalMode: department.approvalMode,
      clusterId: department.clusterId ?? '',
      name: department.name,
    },
    onSubmit: async ({ value }) => {
      setSaveError(null);

      try {
        await onSave({
          approvalMode: value.approvalMode,
          clusterId: value.clusterId || null,
          name: value.name.trim(),
        });
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
                        label: 'Notification only',
                        value: 'notification',
                      },
                      {
                        label: 'Approval required',
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
                Routing emails
              </h3>

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
                <div className="mt-4 flex flex-col gap-3 sm:flex-row sm:items-start">
                  <div className="flex-1">
                    <routingEmailForm.AppField name="address">
                      {(field) => (
                        <field.TextField
                          inputClassName="input input-bordered w-full bg-base-100"
                          label="Add routing email"
                          placeholder="email@ucdavis.edu"
                          type="email"
                        />
                      )}
                    </routingEmailForm.AppField>
                  </div>
                  <routingEmailForm.SubscribeButton
                    className="btn btn-outline sm:mt-8"
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

            <div className="mt-6 flex justify-end gap-3">
              <button
                className={`btn btn-outline border-rose-300 text-rose-800 hover:border-rose-400 hover:bg-rose-100 ${statusTextColors.danger}`}
                disabled={isDeleting}
                onClick={() => setIsDeleteConfirmOpen(true)}
                type="button"
              >
                Delete department
              </button>
              <button className="btn btn-ghost" onClick={onClose} type="button">
                Cancel
              </button>
              <settingsForm.SubscribeButton
                className="btn btn-primary"
                label="Save changes"
                loadingLabel="Saving..."
              />
            </div>
          </settingsForm.AppForm>
        </form>
      </AdminModalFrame>

      {isDeleteConfirmOpen ? (
        <WarningModal
          confirmLabel="Delete department"
          isSaving={isDeleting}
          onCancel={() => setIsDeleteConfirmOpen(false)}
          onConfirm={() => {
            void handleDelete();
          }}
          title="Delete department?"
        >
          <span>
            Delete <strong>{department.name}</strong>?
          </span>
        </WarningModal>
      ) : null}
    </>
  );
}
