import { useId } from 'react';
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
  const textareaId = useId();
  const descriptionId = hasError ? `${textareaId}-error` : undefined;

  return (
    <FieldWrapper controlId={textareaId} label={label} required={required}>
      <textarea
        aria-describedby={descriptionId}
        aria-invalid={hasError}
        aria-required={required}
        className={`${textareaClassName ?? 'textarea textarea-bordered w-full'} ${
          hasError ? 'textarea-error' : ''
        }`}
        id={textareaId}
        onBlur={field.handleBlur}
        onChange={(e) => field.handleChange(e.target.value)}
        placeholder={placeholder ?? `Enter ${label.toLowerCase()}`}
        rows={rows}
        value={field.state.value}
      />
    </FieldWrapper>
  );
}
