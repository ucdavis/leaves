import { useId } from 'react';
import { useFieldContext } from './formContext.tsx';
import { FieldWrapper } from './fieldWrapper.tsx';

interface TextFieldProps {
  helperText?: string;
  inputClassName?: string;
  label: string;
  placeholder?: string;
  required?: boolean;
  type?: 'date' | 'email' | 'text';
}

export function TextField({
  helperText,
  inputClassName,
  label,
  placeholder,
  required,
  type,
}: TextFieldProps) {
  const field = useFieldContext<string>();
  const hasError = field.state.meta.errors.length > 0;
  const inputId = useId();
  const descriptionId = hasError
    ? `${inputId}-error`
    : helperText
      ? `${inputId}-help`
      : undefined;

  return (
    <FieldWrapper
      controlId={inputId}
      helperText={helperText}
      label={label}
      required={required}
    >
      <input
        aria-describedby={descriptionId}
        aria-invalid={hasError}
        aria-required={required}
        className={`${inputClassName ?? 'input input-bordered w-full'} ${
          hasError ? 'input-error' : ''
        }`}
        id={inputId}
        onBlur={field.handleBlur}
        onChange={(e) => field.handleChange(e.target.value)}
        placeholder={placeholder ?? `Enter ${label.toLowerCase()}`}
        type={type ?? 'text'}
        value={field.state.value}
      />
    </FieldWrapper>
  );
}
