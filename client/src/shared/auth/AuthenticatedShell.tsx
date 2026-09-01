import { Link } from '@tanstack/react-router';
import { useRouterState } from '@tanstack/react-router';
import { useQuery } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import { approvalWorkspaceQueryOptions } from '@/queries/approvals.ts';
import { AppFooter } from '@/shared/AppFooter.tsx';
import { LocalRoleSwitcher } from './LocalRoleSwitcher.tsx';
import {
  canAccessApprovalWorkspace,
  canAccessFacultyWorkspace,
  hasAdminRole,
  hasCaoRole,
} from './roleAccess.ts';
import { useUser } from './UserContext.tsx';

export const AuthenticatedShell = ({
  children,
}: {
  children: ReactNode;
}) => {
  const user = useUser();
  const location = useRouterState({
    select: (state) => state.location,
  });
  const pathname = location.pathname;
  const isAdmin = hasAdminRole(user.roles);
  const canApproveLeave = canAccessApprovalWorkspace(user.roles);
  const isLocalDevelopment = import.meta.env.DEV;
  const approvalWorkspaceQuery = useQuery({
    ...approvalWorkspaceQueryOptions(),
    enabled: canApproveLeave,
  });
  const pendingApprovalCount =
    approvalWorkspaceQuery.data?.pendingRequests.length ?? 0;
  const calendarLabel = hasCaoRole(user.roles)
    ? 'CAO Calendar'
    : 'Team Calendar';
  const items = isAdmin
    ? [{ label: 'Admin', to: '/admin' as const }]
    : [
        ...(canAccessFacultyWorkspace(user.roles)
          ? [
              { label: 'Dashboard', to: '/' as const },
              { label: 'History', to: '/history' as const },
            ]
          : []),
        ...(canApproveLeave
          ? [
              { label: calendarLabel, to: '/team-calendar' as const },
              { label: 'Approvals', to: '/approvals' as const },
            ]
          : []),
      ];
  const showSecondaryNav = !pathname.startsWith('/admin');
  const initials = user.name
    .split(' ')
    .filter(Boolean)
    .map((part) => part[0])
    .join('')
    .slice(0, 2)
    .toUpperCase();
  const currentReturnUrl = `${location.pathname}${location.search}`;

  return (
    <div className="flex min-h-screen flex-col bg-base-200">
      <header className="border-b border-primary/80 bg-primary text-primary-content shadow-sm py-6">
        <div className="container">
          <div className="flex items-center justify-between gap-4">
            <Link className="flex items-center gap-3" to="/">
              <img alt="Leaves" className="h-10 w-10" src="/leaves-logo.svg" />
              <div className="leading-tight">
                <div className="text font-semibold uppercase text-secondary">
                  Leaves
                </div>
                <div className="text-sm text-primary-content/80 uppercase">
                  CAES Administrative workspace
                </div>
              </div>
            </Link>

            {isLocalDevelopment ? (
              <LocalRoleSwitcher
                currentReturnUrl={currentReturnUrl}
                roles={user.roles}
                userName={user.name}
              />
            ) : (
              <div className="flex items-center gap-3">
                <div className="flex items-center gap-3 text-right">
                  <div className="hidden sm:block">
                    <div className="text-sm font-semibold">{user.name}</div>
                    <div className="text-xs font-semibold uppercase tracking-[0.16em] text-secondary">
                      SIGNED IN
                    </div>
                  </div>
                  <div className="flex h-11 w-11 items-center justify-center rounded-full bg-secondary text-sm font-bold text-primary">
                    {initials || '?'}
                  </div>
                </div>
              </div>
            )}
          </div>
        </div>
      </header>

      {showSecondaryNav ? (
        <nav className="border-b border-base-300 bg-base-100">
          <div className="container flex gap-1 overflow-x-auto">
            {items.map((item) => (
              <Link
                className="border-b-2 border-transparent px-5 py-4 text-sm font-semibold text-base-content/60 transition hover:text-base-content data-[status=active]:border-primary data-[status=active]:text-primary"
                key={item.to}
                to={item.to}
              >
                <span>{item.label}</span>
                {item.to === '/approvals' && pendingApprovalCount > 0 ? (
                  <span className="ml-2 rounded-full bg-secondary px-2 py-0.5 text-xs font-bold text-primary">
                    {pendingApprovalCount}
                  </span>
                ) : null}
              </Link>
            ))}
          </div>
        </nav>
      ) : null}

      <main className="flex-1 pt-6 lg:pt-8">{children}</main>
      <AppFooter />
    </div>
  );
};
