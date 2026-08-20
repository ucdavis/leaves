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
  const hasError = field.state.meta.isTouched && !field.state.meta.isValid;

  return (
    <FieldWrapper helperText={helperText} label={label} required={required}>
      <select
        aria-required={required}
        className={`${selectClassName ?? 'select select-bordered w-full'} ${
          hasError ? 'select-error' : ''
        }`}
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
