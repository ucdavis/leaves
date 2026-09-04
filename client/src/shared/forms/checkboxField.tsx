import { useFieldContext } from './formContext.tsx';
import { getValidationErrorMessage } from './validationError.ts';

interface CheckboxFieldProps {
  description?: string;
  label: string;
}

export function CheckboxField({ description, label }: CheckboxFieldProps) {
  const field = useFieldContext<boolean>();
  const hasError = field.state.meta.errors.length > 0;

  return (
    <div className="form-control w-full">
      <label className="flex items-start gap-3 text-sm text-base-content">
        <input
          aria-invalid={hasError}
          checked={field.state.value}
          className={`checkbox mt-0.5 ${hasError ? 'checkbox-error' : ''}`}
          onBlur={field.handleBlur}
          onChange={(event) => field.handleChange(event.target.checked)}
          type="checkbox"
        />
        <span>
          <span className="block">{label}</span>
          {description ? (
            <span className="mt-1 block text-base-content/70">
              {description}
            </span>
          ) : null}
        </span>
      </label>
      {hasError ? (
        <label className="label">
          <span className="label-text-alt text-error" role="alert">
            {field.state.meta.errors
              .map(getValidationErrorMessage)
              .join(', ')}
          </span>
        </label>
      ) : null}
    </div>
  );
}
