import { useEffect } from 'react';

let lockCount = 0;
let previousBodyOverflow: string | null = null;
let previousBodyPaddingRight: string | null = null;

export function useLockBodyScroll(locked: boolean) {
  useEffect(() => {
    if (!locked) {
      return undefined;
    }

    if (typeof window === 'undefined' || typeof document === 'undefined') {
      return undefined;
    }

    const { body, documentElement } = document;

    if (lockCount === 0) {
      previousBodyOverflow = body.style.overflow;
      previousBodyPaddingRight = body.style.paddingRight;
    }

    lockCount += 1;

    const scrollbarWidth = Math.max(
      0,
      window.innerWidth - documentElement.clientWidth
    );

    body.style.overflow = 'hidden';
    body.style.paddingRight =
      scrollbarWidth > 0
        ? `${scrollbarWidth}px`
        : previousBodyPaddingRight ?? '';

    return () => {
      lockCount = Math.max(0, lockCount - 1);

      if (lockCount === 0) {
        body.style.overflow = previousBodyOverflow ?? '';
        body.style.paddingRight = previousBodyPaddingRight ?? '';
        previousBodyOverflow = null;
        previousBodyPaddingRight = null;
      }
    };
  }, [locked]);
}
