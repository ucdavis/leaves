import { useState } from 'react';
import { z } from 'zod';
import { useAppForm } from '@/shared/forms/formContext.tsx';
import { statusSurfaceColors } from '@/shared/statusColors.ts';
import { AdminModalFrame } from './AdminModalFrame.tsx';

const userFormSchema = z.object({
  email: z
    .string()
    .trim()
    .refine(
      (value) => value.length === 0 || z.email().safeParse(value).success,
      'Enter a valid email address.'
    ),
  employeeId: z
    .string()
    .trim()
    .refine(
      (value) => value.length === 0 || /^\d{8}$/.test(value),
      'Employee ID must be exactly 8 digits.'
    ),
  iamId: z
    .string()
    .trim()
    .min(1, 'IAM ID is required.')
    .max(10, 'IAM ID must be 10 characters or fewer.')
    .regex(
      /^[a-z][\w-]*$/i,
      'IAM ID must start with a letter and use only letters, numbers, underscores, or hyphens.'
    ),
  name: z.string().trim().min(1, 'Display name is required.'),
});

const editableUserFormSchema = userFormSchema.extend({
  active: z.boolean(),
});

export type AdminUserModalValues = z.infer<typeof editableUserFormSchema>;

export function AdminUserModal({
  initialValues,
  onClose,
  onSubmit,
  showActiveField = true,
  submitErrorMessage,
  submitLabel,
  submittingLabel,
  title,
}: {
  initialValues: AdminUserModalValues;
  onClose: () => void;
  onSubmit: (value: AdminUserModalValues) => Promise<void>;
  showActiveField?: boolean;
  submitErrorMessage: (error: unknown) => string;
  submitLabel: string;
  submittingLabel: string;
  title: string;
}) {
  const [submitError, setSubmitError] = useState<string | null>(null);
  const form = useAppForm({
    defaultValues: initialValues,
    onSubmit: async ({ value }) => {
      setSubmitError(null);

      try {
        await onSubmit({
          active: value.active,
          email: value.email.trim(),
          employeeId: value.employeeId.trim(),
          iamId: value.iamId.trim(),
          name: value.name.trim(),
        });
      } catch (error) {
        setSubmitError(submitErrorMessage(error));
      }
    },
    validators: {
      onChange: editableUserFormSchema,
    },
  });

  return (
    <AdminModalFrame
      description="These edits now persist to the AppUser table."
      title={title}
    >
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void form.handleSubmit();
        }}
      >
        <form.AppForm>
          <div className="grid gap-4 sm:grid-cols-2">
            <form.AppField name="name">
              {(field) => <field.TextField label="Display name" />}
            </form.AppField>
            <form.AppField name="email">
              {(field) => <field.TextField label="Email" type="email" />}
            </form.AppField>
            <form.AppField name="employeeId">
              {(field) => <field.TextField label="Employee ID" />}
            </form.AppField>
            <form.AppField name="iamId">
              {(field) => <field.TextField label="IAM ID" />}
            </form.AppField>
          </div>

          {showActiveField ? (
            <div className="mt-5">
              <form.AppField name="active">
                {(field) => (
                  <field.CheckboxField label="Include this person in the admin roster" />
                )}
              </form.AppField>
            </div>
          ) : null}

          {submitError ? (
            <div
              className={`mt-4 rounded-xl px-4 py-3 text-sm ${statusSurfaceColors.danger}`}
            >
              {submitError}
            </div>
          ) : null}

          <div className="mt-6 flex justify-end gap-3">
            <button className="btn btn-ghost" onClick={onClose} type="button">
              Cancel
            </button>
            <form.SubscribeButton
              className="btn btn-primary"
              label={submitLabel}
              loadingLabel={submittingLabel}
            />
          </div>
        </form.AppForm>
      </form>
    </AdminModalFrame>
  );
}
