import { XMarkIcon } from '@heroicons/react/24/outline';
import { useEffect, useId, useRef, type ReactNode } from 'react';

const focusableSelector = [
  'a[href]',
  'button:not([disabled])',
  'input:not([disabled]):not([type="hidden"])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[tabindex]:not([tabindex="-1"])',
].join(',');

export function Modal({
  children,
  onClose,
  title,
}: {
  children: ReactNode;
  onClose: () => void;
  title: string;
}) {
  const titleId = useId();
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
    if (!dialogElement) {
      return undefined;
    }

    function handleCancel(event: Event) {
      event.preventDefault();
      onClose();
    }

    dialogElement.addEventListener('cancel', handleCancel);
    return () => dialogElement.removeEventListener('cancel', handleCancel);
  }, [onClose]);

  return (
    <dialog
      aria-labelledby={titleId}
      aria-modal="true"
      className="fixed inset-0 z-50 m-0 flex h-dvh w-dvw max-h-none max-w-none items-center justify-center border-0 bg-transparent p-4 backdrop:bg-black/50"
      onClick={(event) => {
        if (event.target === event.currentTarget) {
          onClose();
        }
      }}
      ref={dialogRef}
      tabIndex={-1}
    >
      <div className="max-h-[85vh] w-full max-w-2xl overflow-hidden rounded-2xl bg-base-100 shadow-2xl outline-none">
        <div className="flex items-center justify-between border-b border-base-300 px-6 py-4">
          <h2 className="text-lg font-bold text-primary" id={titleId}>
            {title}
          </h2>
          <button
            aria-label="Close"
            className="btn btn-ghost btn-sm btn-circle"
            onClick={onClose}
            type="button"
          >
            <XMarkIcon className="h-5 w-5" />
          </button>
        </div>
        <div className="max-h-[calc(85vh-4rem)] overflow-y-auto p-6">
          {children}
        </div>
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
