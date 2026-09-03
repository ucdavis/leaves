import { useEffect, useRef, useState } from 'react';
import {
  canAccessFacultyWorkspace,
  hasAdminRole,
  hasCaoRole,
} from './roleAccess.ts';

const DEV_ROLE_SWITCH_OPTIONS = [
  {
    description: 'Open the admin workspace.',
    label: 'Admin',
    returnUrl: '/admin',
    value: 'admin',
  },
  {
    description: 'Open the faculty dashboard.',
    label: 'Faculty',
    returnUrl: '/',
    value: 'faculty',
  },
  {
    description: 'Open the chair dashboard.',
    label: 'Chair',
    returnUrl: '/',
    value: 'chair',
  },
  {
    description: 'Open the approval calendar.',
    label: 'CAO',
    returnUrl: '/team-calendar',
    value: 'cao',
  },
  {
    description: 'Use your real Entra sign-in.',
    label: 'Self',
    returnUrl: '/',
    value: 'self',
  },
] as const;

export function LocalRoleSwitcher({
  currentReturnUrl,
  roles,
  userName,
}: {
  currentReturnUrl: string;
  roles: readonly string[];
  userName: string;
}) {
  const [isOpen, setIsOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement | null>(null);
  const roleLabel = getUserRoleLabel(roles);
  const authLabel = `${roleLabel} · LOCAL DEV`;

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    const handlePointerDown = (event: PointerEvent) => {
      if (!containerRef.current?.contains(event.target as Node)) {
        setIsOpen(false);
      }
    };

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setIsOpen(false);
      }
    };

    document.addEventListener('pointerdown', handlePointerDown);
    document.addEventListener('keydown', handleKeyDown);

    return () => {
      document.removeEventListener('pointerdown', handlePointerDown);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [isOpen]);

  return (
    <div className="dropdown dropdown-end" ref={containerRef}>
      <button
        aria-expanded={isOpen}
        aria-label={`Open local role switcher for ${userName}`}
        className="flex cursor-pointer items-center gap-3 rounded-2xl border border-primary-content/10 bg-primary-content/5 px-3 py-2 text-left transition hover:bg-primary-content/10"
        onClick={() => setIsOpen((open) => !open)}
        type="button"
      >
        <div className="hidden min-w-0 sm:block">
          <div className="truncate text-sm font-semibold leading-tight">
            {userName}
          </div>
          <div className="truncate text-xs font-semibold uppercase tracking-[0.16em] text-secondary">
            {authLabel}
          </div>
        </div>
        <span className="flex h-8 w-8 items-center justify-center rounded-full bg-secondary text-xs font-bold text-primary sm:hidden">
          {getInitials(userName)}
        </span>
        <span className="hidden shrink-0 text-primary-content/70 sm:block">
          ▾
        </span>
      </button>

      {isOpen ? (
        <ul className="menu dropdown-content z-30 mt-3 w-80 rounded-2xl border border-base-300 bg-base-100 p-2 text-base-content shadow-2xl">
          <li className="menu-title px-3 pt-2 pb-1 text-[0.7rem] font-bold uppercase tracking-[0.18em] text-base-content/50">
            Switch local role
          </li>
          {DEV_ROLE_SWITCH_OPTIONS.map((option) => {
            const href = `/login?as=${encodeURIComponent(
              option.value
            )}&returnUrl=${encodeURIComponent(
              option.value === 'self' ? currentReturnUrl : option.returnUrl
            )}`;
            const isCurrentRole =
              option.value !== 'self' &&
              option.label.toLowerCase() === roleLabel.toLowerCase();

            return (
              <li key={option.value}>
                <a
                  className="rounded-xl px-3 py-3"
                  href={href}
                  onClick={() => setIsOpen(false)}
                >
                  <div className="flex w-full items-center justify-between gap-3">
                    <div>
                      <div className="font-semibold">{option.label}</div>
                      <div className="text-xs text-base-content/60">
                        {option.description}
                      </div>
                    </div>
                    {isCurrentRole ? (
                      <span className="badge badge-outline badge-sm">
                        Current
                      </span>
                    ) : null}
                  </div>
                </a>
              </li>
            );
          })}
        </ul>
      ) : null}
    </div>
  );
}

function getInitials(name: string) {
  const initials = name
    .split(' ')
    .filter(Boolean)
    .map((part) => part[0])
    .join('')
    .slice(0, 2)
    .toUpperCase();

  return initials || '?';
}

function getUserRoleLabel(roles: readonly string[]) {
  if (hasAdminRole(roles)) {
    return 'Admin';
  }

  if (hasCaoRole(roles)) {
    return 'CAO';
  }

  if (roles.some((role) => role.toLowerCase() === 'chair')) {
    return 'Chair';
  }

  if (canAccessFacultyWorkspace(roles)) {
    return 'Faculty';
  }

  return 'Signed In';
}
