import { z } from 'zod';
import { useAppForm } from '@/shared/forms/formContext.tsx';
import {
  statusBorderColors,
  statusTextColors,
} from '@/shared/statusColors.ts';

type InlineTextEditorValues = {
  value: string;
};

export function InlineTextEditor({
  initialValue,
  inputClassName,
  onSave,
  requiredMessage,
  savingMessage,
  wrapperClassName,
}: {
  initialValue: string;
  inputClassName: string;
  onSave: (value: string) => Promise<void>;
  requiredMessage: string;
  savingMessage: string;
  wrapperClassName?: string;
}) {
  const schema = z.object({
    value: z.string().trim().min(1, requiredMessage),
  });

  const form = useAppForm({
    defaultValues: {
      value: initialValue,
    } satisfies InlineTextEditorValues,
    onSubmit: async ({ value }) => {
      const nextValue = value.value.trim();

      if (nextValue === initialValue) {
        return;
      }

      await onSave(nextValue);
    },
    validators: {
      onChange: schema,
    },
  });

  return (
    <form.AppForm>
      <form.AppField name="value">
        {(field) => {
          const hasError =
            field.state.meta.isTouched && !field.state.meta.isValid;

          return (
            <div className={wrapperClassName}>
              <input
                className={`${inputClassName} ${
                  hasError ? statusBorderColors.dangerFocus : ''
                }`}
                disabled={field.form.state.isSubmitting}
                onBlur={(event) => {
                  field.handleBlur();

                  if (event.target.value.trim()) {
                    void form.handleSubmit();
                  }
                }}
                onChange={(event) => field.handleChange(event.target.value)}
                value={field.state.value}
              />
              {hasError ? (
                <p className={`mt-2 text-sm ${statusTextColors.danger}`}>
                  {field.state.meta.errors
                    .flatMap((issue) => (issue?.message ? [issue.message] : []))
                    .join(', ')}
                </p>
              ) : field.form.state.isSubmitting ? (
                <p className="mt-2 text-sm text-base-content/70">
                  {savingMessage}
                </p>
              ) : null}
            </div>
          );
        }}
      </form.AppField>
    </form.AppForm>
  );
}
