import { createFormHookContexts, createFormHook } from '@tanstack/react-form';
import { CheckboxField } from './checkboxField.tsx';
import { TextField } from './textField.tsx';
import { TextAreaField } from './textAreaField.tsx';
import { SubscribeButton } from './submitButton.tsx';
import { SelectField } from './selectField.tsx';

// export useFieldContext for use in your custom components
export const { fieldContext, formContext, useFieldContext, useFormContext } =
  createFormHookContexts();

const { useAppForm } = createFormHook({
  fieldComponents: {
    // text, select, checkbox, etc.
    CheckboxField,
    SelectField,
    TextAreaField,
    TextField,
  },
  fieldContext,
  formComponents: {
    // submit buttons and such
    SubscribeButton,
  },
  formContext,
});

export { useAppForm };
