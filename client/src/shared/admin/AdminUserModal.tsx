import { useState } from 'react';
import { z } from 'zod';
import type { AdminDepartment } from '@/shared/admin/adminData.tsx';
import { useAppForm } from '@/shared/forms/formContext.tsx';
import { statusSurfaceColors } from '@/shared/statusColors.ts';
import { AdminModalFrame } from './AdminModalFrame.tsx';

const userFormSchema = z.object({
  departmentOverrideEndDate: z.string(),
  departmentOverrideId: z.string(),
  departmentOverrideStartDate: z.string(),
  email: z
    .string()
    .trim()
    .refine(
      (value) => value.length === 0 || z.email().safeParse(value).success,
      'Enter a valid email address.'
    ),
  name: z.string().trim().min(1, 'Display name is required.'),
}).refine(
  (value) =>
    !value.departmentOverrideId.trim() ||
    !!value.departmentOverrideStartDate.trim(),
  {
    message: 'Start date is required when adding a department override.',
    path: ['departmentOverrideStartDate'],
  }
).refine(
  (value) =>
    !value.departmentOverrideEndDate.trim() ||
    !value.departmentOverrideStartDate.trim() ||
    value.departmentOverrideEndDate > value.departmentOverrideStartDate,
  {
    message: 'End date must be after the start date.',
    path: ['departmentOverrideEndDate'],
  }
);

export type AdminUserModalValues = z.infer<typeof userFormSchema>;

export function AdminUserModal({
  departments,
  initialValues,
  onClose,
  onSubmit,
  submitErrorMessage,
  submitLabel,
  submittingLabel,
  title,
}: {
  departments: AdminDepartment[];
  initialValues: AdminUserModalValues;
  onClose: () => void;
  onSubmit: (value: AdminUserModalValues) => Promise<void>;
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
          departmentOverrideEndDate: value.departmentOverrideEndDate,
          departmentOverrideId: value.departmentOverrideId,
          departmentOverrideStartDate: value.departmentOverrideStartDate,
          email: value.email.trim(),
          name: value.name.trim(),
        });
      } catch (error) {
        setSubmitError(submitErrorMessage(error));
      }
    },
    validators: {
      onChange: userFormSchema,
    },
  });

  return (
    <AdminModalFrame
      description="Profile edits persist to People. Department overrides create dated reporting department rows."
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
          </div>

          <div className="mt-6 rounded-2xl bg-[var(--admin-sand)] p-5">
            <h3 className="text-sm font-semibold uppercase tracking-[0.2em] text-[var(--admin-gold-deep)]">
              Department override
            </h3>
            <div className="mt-4 grid gap-4 sm:grid-cols-3">
              <form.AppField name="departmentOverrideId">
                {(field) => (
                  <field.SelectField
                    allowEmptyOption
                    label="Department"
                    options={departments.map((department) => ({
                      label: department.name,
                      value: department.id,
                    }))}
                    placeholder="No override"
                    selectClassName="select select-bordered w-full bg-white"
                  />
                )}
              </form.AppField>
              <form.AppField name="departmentOverrideStartDate">
                {(field) => <field.TextField label="Start date" type="date" />}
              </form.AppField>
              <form.AppField name="departmentOverrideEndDate">
                {(field) => <field.TextField label="End date" type="date" />}
              </form.AppField>
            </div>
          </div>

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
