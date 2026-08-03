import { useEffect, useId, useRef, type ReactNode } from 'react';

const focusableSelector = [
  'a[href]',
  'button:not([disabled])',
  'input:not([disabled]):not([type="hidden"])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[tabindex]:not([tabindex="-1"])',
].join(',');

export function AdminModalFrame({
  children,
  description,
  maxWidthClassName,
  onRequestClose,

  title,
}: {
  children: ReactNode;
  description?: string;
  maxWidthClassName?: string;
  onRequestClose?: () => void;
  title: string;
}) {
  const titleId = useId();
  const descriptionId = useId();
  const dialogRef = useRef<HTMLDialogElement | null>(null);
  const previouslyFocusedElementRef = useRef<Element | null>(null);

  useEffect(() => {
    previouslyFocusedElementRef.current = document.activeElement;

    const dialogElement = dialogRef.current;
    if (!dialogElement) {
      return undefined;
    }

    if (!dialogElement.open) {
      dialogElement.showModal();
    }

    const focusableElements = getFocusableElements(dialogElement);
    (focusableElements[0] ?? dialogElement).focus({ preventScroll: true });

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';

    return () => {
      document.body.style.overflow = previousOverflow;
      if (dialogElement.open) {
        dialogElement.close();
      }
      if (isFocusableElement(previouslyFocusedElementRef.current)) {
        previouslyFocusedElementRef.current.focus({ preventScroll: true });
      }
    };
  }, []);

  useEffect(() => {
    const dialogElement = dialogRef.current;
    const close = onRequestClose;
    if (!dialogElement || !close) {
      return undefined;
    }

    function handleCancel(event: Event) {
      event.preventDefault();
      close();
    }

    dialogElement.addEventListener('cancel', handleCancel);
    return () => dialogElement.removeEventListener('cancel', handleCancel);
  }, [onRequestClose]);

  return (
    <dialog
      aria-describedby={description ? descriptionId : undefined}
      aria-labelledby={titleId}
      aria-modal="true"
      className="fixed inset-0 z-50 m-0 flex h-dvh w-dvw max-h-none max-w-none items-center justify-center border-0 bg-transparent p-4 backdrop:bg-slate-950/40"
      onClick={(event) => {
        if (event.target === event.currentTarget) {
          onRequestClose?.();
        }
      }}
      ref={dialogRef}
      tabIndex={-1}
    >
      <div
        className={`max-h-[90vh] w-full overflow-y-auto rounded-[1.5rem] border border-base-300 bg-base-100 p-6 shadow-2xl outline-none ${
          maxWidthClassName ?? 'max-w-2xl'
        }`}
      >
        <div className="mb-6">
          <h2 className="text-xl font-semibold text-primary" id={titleId}>
            {title}
          </h2>
          {description ? (
            <p className="mt-2 text-sm text-base-content/70" id={descriptionId}>
              {description}
            </p>
          ) : null}
        </div>
        {children}
      </div>
    </dialog>
  );
}

function getFocusableElements(container: HTMLElement) {
  return Array.from(container.querySelectorAll<HTMLElement>(focusableSelector))
    .filter((element) => element.getClientRects().length > 0)
    .filter((element) => !element.hasAttribute('disabled'));
}

function isFocusableElement(
  element: Element | null
): element is HTMLElement {
  return element !== null && 'focus' in element;
}
