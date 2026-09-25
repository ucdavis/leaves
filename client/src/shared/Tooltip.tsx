import type { CSSProperties, ReactNode } from 'react';
import { useId, useState } from 'react';
import {
  arrow,
  autoUpdate,
  flip,
  FloatingPortal,
  offset,
  shift,
  useDismiss,
  useFloating,
  useFocus,
  useHover,
  useInteractions,
  useRole,
} from '@floating-ui/react';

type TooltipPlacement = 'bottom' | 'left' | 'right' | 'top';

export function Tooltip({
  children,
  content,
  placement = 'top',
}: {
  children: ReactNode;
  content: string;
  placement?: TooltipPlacement;
}) {
  const [open, setOpen] = useState(false);
  const [arrowElement, setArrowElement] = useState<HTMLDivElement | null>(null);
  const tooltipId = useId();
  const {
    context,
    floatingStyles,
    middlewareData,
    placement: resolvedPlacement,
    refs: { setFloating, setReference },
  } = useFloating({
    middleware: [
      offset(12),
      flip({ padding: 8 }),
      shift({ padding: 8 }),
      arrow({ element: arrowElement }),
    ],
    onOpenChange: setOpen,
    open,
    placement,
    whileElementsMounted: autoUpdate,
  });
  const hover = useHover(context, {
    delay: { close: 0, open: 150 },
    move: false,
  });
  const focus = useFocus(context);
  const dismiss = useDismiss(context);
  const role = useRole(context, { role: 'tooltip' });
  const { getFloatingProps, getReferenceProps } = useInteractions([
    hover,
    focus,
    dismiss,
    role,
  ]);

  const staticSideByPlacement = {
    bottom: 'top',
    left: 'right',
    right: 'left',
    top: 'bottom',
  } as const;
  const basePlacement = resolvedPlacement.split('-')[0] as TooltipPlacement;
  const arrowStyle: CSSProperties = {
    [staticSideByPlacement[basePlacement]]: '-5px',
  };

  if (middlewareData.arrow?.x != null) {
    arrowStyle.left = `${middlewareData.arrow.x}px`;
  }

  if (middlewareData.arrow?.y != null) {
    arrowStyle.top = `${middlewareData.arrow.y}px`;
  }

  return (
    <>
      <span
        {...getReferenceProps({
          className: 'tooltip-trigger',
          tabIndex: 0,
        })}
        ref={setReference}
      >
        {children}
      </span>
      {open ? (
        <FloatingPortal>
          <div
            {...getFloatingProps({
              className: 'floating-tooltip',
              id: tooltipId,
              style: floatingStyles,
            })}
            ref={setFloating}
          >
            <div
              aria-hidden="true"
              className="floating-tooltip-arrow"
              ref={setArrowElement}
              style={arrowStyle}
            />
            {content}
          </div>
        </FloatingPortal>
      ) : null}
    </>
  );
}
