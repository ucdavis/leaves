import { useFieldContext } from './formContext.tsx';

interface CheckboxFieldProps {
  description?: string;
  label: string;
}

export function CheckboxField({ description, label }: CheckboxFieldProps) {
  const field = useFieldContext<boolean>();

  return (
    <label className="flex items-start gap-3 text-sm text-base-content">
      <input
        checked={field.state.value}
        className="checkbox mt-0.5"
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
  );
}
