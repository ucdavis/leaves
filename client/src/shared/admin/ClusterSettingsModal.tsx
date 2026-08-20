import { useState } from 'react';
import { z } from 'zod';
import type { AdminCluster } from '@/shared/admin/adminData.ts';
import { useAppForm } from '@/shared/forms/formContext.tsx';
import {
  statusSurfaceColors,
  statusTextColors,
} from '@/shared/statusColors.ts';
import { WarningModal } from '@/shared/WarningModal.tsx';
import { AdminModalFrame } from './AdminModalFrame.tsx';

const clusterSettingsSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, 'Cluster name is required.')
    .max(100, 'Cluster name must be 100 characters or fewer.'),
});

export function ClusterSettingsModal({
  cluster,
  departmentCount,
  formatError,
  isDeleting,
  onClose,
  onDelete,
  onSave,
}: {
  cluster: AdminCluster;
  departmentCount: number;
  formatError: (error: unknown) => string;
  isDeleting: boolean;
  onClose: () => void;
  onDelete: () => Promise<void>;
  onSave: (updates: Pick<AdminCluster, 'name'>) => Promise<void>;
}) {
  const [saveError, setSaveError] = useState<string | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [isDeleteConfirmOpen, setIsDeleteConfirmOpen] = useState(false);

  const settingsForm = useAppForm({
    defaultValues: {
      name: cluster.name,
    },
    onSubmit: async ({ value }) => {
      setSaveError(null);

      try {
        await onSave({ name: value.name.trim() });
      } catch (error) {
        setSaveError(formatError(error));
      }
    },
    validators: {
      onChange: clusterSettingsSchema,
    },
  });

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
        maxWidthClassName="max-w-2xl"
        onRequestClose={onClose}
        title="Cluster settings"
      >
        <form
          onSubmit={(event) => {
            event.preventDefault();
            void settingsForm.handleSubmit();
          }}
        >
          <settingsForm.AppForm>
            <div className="grid gap-4">
              <settingsForm.AppField name="name">
                {(field) => (
                  <field.TextField
                    inputClassName="input input-bordered w-full bg-base-100"
                    label="Edit Cluster Name"
                    placeholder="Enter cluster name"
                    type="text"
                  />
                )}
              </settingsForm.AppField>
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
                Deactivate cluster
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
          confirmLabel="Deactivate cluster"
          isSaving={isDeleting}
          onCancel={() => setIsDeleteConfirmOpen(false)}
          onConfirm={() => {
            void handleDelete();
          }}
          title="Deactivate cluster?"
        >
          <span>
            This will unassign all departments from the{' '}
            <strong>{cluster.name}</strong> cluster and hide the cluster from
            active lists.
          </span>
        </WarningModal>
      ) : null}
    </>
  );
}
