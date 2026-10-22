import { ReactNode } from 'react';
import { useFieldContext } from './formContext.tsx';
import { getValidationErrorMessage } from './validationError.ts';

interface FieldWrapperProps {
  children: ReactNode;
  controlId: string;
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
  controlId,
  helperText,
  label,
  required,
  wrapperClassName,
}: FieldWrapperProps) {
  const field = useFieldContext<string>();
  const hasError = field.state.meta.errors.length > 0;

  return (
    <div className={wrapperClassName ?? 'form-control w-full'}>
      <label className="label" htmlFor={controlId}>
        <span className="label-text font-medium text-base-content">
          {label}
          {required ? <span className="text-error"> *</span> : null}
        </span>
      </label>
      {children}
      {hasError && (
        <p className="label" id={`${controlId}-error`}>
          <span className="label-text-alt text-error" role="alert">
            {field.state.meta.errors.map(getValidationErrorMessage).join(', ')}
          </span>
        </p>
      )}
      {!hasError && helperText ? (
        <p className="label" id={`${controlId}-help`}>
          <span className="label-text-alt text-base-content/70">
            {helperText}
          </span>
        </p>
      ) : null}
      {field.state.meta.isValidating && (
        <span className="loading loading-spinner loading-xs ml-2"></span>
      )}
    </div>
  );
}
