import { useState } from 'react';
import { z } from 'zod';
import type {
  AdminCluster,
  AdminDepartment,
} from '@/shared/admin/adminData.tsx';
import { useAppForm } from '@/shared/forms/formContext.tsx';
import { AdminModalFrame } from './AdminModalFrame.tsx';

const departmentSettingsSchema = z.object({
  approvalMode: z.enum(['notification', 'approval', 'auto']),
  clusterId: z.string(),
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
  onClose,
  onRemoveRoutingEmail,
  onSave,
  onUpsertRoutingEmail,
}: {
  clusters: AdminCluster[];
  department: AdminDepartment;
  formatError: (error: unknown) => string;
  onClose: () => void;
  onRemoveRoutingEmail: (emailId: string) => Promise<void>;
  onSave: (
    updates: Partial<Pick<AdminDepartment, 'approvalMode' | 'clusterId'>>
  ) => Promise<void>;
  onUpsertRoutingEmail: (email: {
    address: string;
    id?: string;
    kind: 'to' | 'cc';
  }) => Promise<void>;
}) {
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
    },
    onSubmit: async ({ value }) => {
      setSaveError(null);

      try {
        await onSave({
          approvalMode: value.approvalMode,
          clusterId: value.clusterId || null,
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

  return (
    <AdminModalFrame
      description="These controls persist to the database for cluster placement, workflow mode, and routing emails."
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
            <p className="mt-2 text-sm text-base-content/70">
              Routing emails are now stored in `DepartmentEmailRouting`.
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
                    className="btn btn-ghost btn-sm text-rose-700"
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
              <div className="mt-4 rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">
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
            <div className="mt-6 rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">
              {saveError}
            </div>
          ) : null}

          <div className="mt-6 flex justify-end gap-3">
            <button className="btn btn-ghost" onClick={onClose} type="button">
              Cancel
            </button>
            <settingsForm.SubscribeButton
              className="btn border-0 bg-secondary text-primary hover:bg-secondary/85"
              label="Save changes"
              loadingLabel="Saving..."
            />
          </div>
        </settingsForm.AppForm>
      </form>
    </AdminModalFrame>
  );
}
