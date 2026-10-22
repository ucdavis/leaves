import { useId } from 'react';
import { useFieldContext } from './formContext.tsx';
import { FieldWrapper } from './fieldWrapper.tsx';

interface SelectFieldProps {
  allowEmptyOption?: boolean;
  helperText?: string;
  label: string;
  options: Array<{ label: string; value: string }>;
  placeholder?: string;
  required?: boolean;
  selectClassName?: string;
}

export function SelectField({
  allowEmptyOption,
  helperText,
  label,
  options,
  placeholder,
  required,
  selectClassName,
}: SelectFieldProps) {
  const field = useFieldContext<string>();
  const hasError = field.state.meta.errors.length > 0;
  const selectId = useId();
  const descriptionId = hasError
    ? `${selectId}-error`
    : helperText
      ? `${selectId}-help`
      : undefined;

  return (
    <FieldWrapper
      controlId={selectId}
      helperText={helperText}
      label={label}
      required={required}
    >
      <select
        aria-describedby={descriptionId}
        aria-invalid={hasError}
        aria-required={required}
        className={`${selectClassName ?? 'select select-bordered w-full'} ${
          hasError ? 'select-error' : ''
        }`}
        id={selectId}
        onBlur={field.handleBlur}
        onChange={(e) => field.handleChange(e.target.value)}
        value={field.state.value || ''}
      >
        <option disabled={!allowEmptyOption} value="">
          {placeholder ?? `Pick a ${label.toLowerCase()}`}
        </option>
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    </FieldWrapper>
  );
}
