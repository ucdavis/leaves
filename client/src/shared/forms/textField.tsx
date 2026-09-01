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

  return (
    <FieldWrapper helperText={helperText} label={label} required={required}>
      <input
        aria-required={required}
        className={`${inputClassName ?? 'input input-bordered w-full'} ${
          hasError ? 'input-error' : ''
        }`}
        onBlur={field.handleBlur}
        onChange={(e) => field.handleChange(e.target.value)}
        placeholder={placeholder ?? `Enter ${label.toLowerCase()}`}
        type={type ?? 'text'}
        value={field.state.value}
      />
    </FieldWrapper>
  );
}
