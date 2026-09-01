import { ReactNode } from 'react';
import { useFieldContext } from './formContext.tsx';
import { getValidationErrorMessage } from './validationError.ts';

interface FieldWrapperProps {
  children: ReactNode;
  helperText?: string;
  label: string;
  required?: boolean;
  wrapperClassName?: string;
}

/**
 * Common wrapper component for form fields that handles label, error display, and validation state
 */
export function FieldWrapper({
  children,
  helperText,
  label,
  required,
  wrapperClassName,
}: FieldWrapperProps) {
  const field = useFieldContext<string>();
  const hasError = field.state.meta.errors.length > 0;

  return (
    <div className={wrapperClassName ?? 'form-control w-full'}>
      <label className="label">
        <span className="label-text font-medium">
          {label}
          {required ? <span className="text-error"> *</span> : null}
        </span>
      </label>
      {children}
      {hasError && (
        <label className="label">
          <span className="label-text-alt text-error" role="alert">
            {field.state.meta.errors.map(getValidationErrorMessage).join(', ')}
          </span>
        </label>
      )}
      {!hasError && helperText ? (
        <label className="label">
          <span className="label-text-alt text-base-content/70">
            {helperText}
          </span>
        </label>
      ) : null}
      {field.state.meta.isValidating && (
        <span className="loading loading-spinner loading-xs ml-2"></span>
      )}
    </div>
  );
}
