import { useFieldContext } from './formContext.tsx';
import { FieldWrapper } from './fieldWrapper.tsx';

interface TextAreaFieldProps {
  label: string;
  placeholder?: string;
  required?: boolean;
  rows?: number;
  textareaClassName?: string;
}

export function TextAreaField({
  label,
  placeholder,
  required,
  rows = 3,
  textareaClassName,
}: TextAreaFieldProps) {
  const field = useFieldContext<string>();
  const hasError = field.state.meta.errors.length > 0;

  return (
    <FieldWrapper label={label} required={required}>
      <textarea
        aria-required={required}
        className={`${textareaClassName ?? 'textarea textarea-bordered w-full'} ${
          hasError ? 'textarea-error' : ''
        }`}
        onBlur={field.handleBlur}
        onChange={(e) => field.handleChange(e.target.value)}
        placeholder={placeholder ?? `Enter ${label.toLowerCase()}`}
        rows={rows}
        value={field.state.value}
      />
    </FieldWrapper>
  );
}
