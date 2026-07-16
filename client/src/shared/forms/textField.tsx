import { useFieldContext } from './formContext.tsx';
import { FieldWrapper } from './fieldWrapper.tsx';

interface TextFieldProps {
  helperText?: string;
  inputClassName?: string;
  label: string;
  placeholder?: string;
  type?: 'email' | 'text';
}

export function TextField({
  helperText,
  inputClassName,
  label,
  placeholder,
  type,
}: TextFieldProps) {
  const field = useFieldContext<string>();
  const hasError = field.state.meta.isTouched && !field.state.meta.isValid;

  return (
    <FieldWrapper helperText={helperText} label={label}>
      <input
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
