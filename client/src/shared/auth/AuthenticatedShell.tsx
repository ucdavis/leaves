import { Link } from '@tanstack/react-router';
import { useRouterState } from '@tanstack/react-router';
import { useUser } from './UserContext.tsx';

const navigationItems = [
  { label: 'Home', to: '/' },
] as const;

export const AuthenticatedShell = ({
  children,
}: {
  children: React.ReactNode;
}) => {
  const user = useUser();
  const pathname = useRouterState({
    select: (state) => state.location.pathname,
  });
  const isAdmin = user.roles.some((role) => role.toLowerCase() === 'admin');
  const items = isAdmin
    ? [...navigationItems, { label: 'Admin', to: '/admin' as const }]
    : navigationItems;
  const showSecondaryNav = !pathname.startsWith('/admin');
  const initials = user.name
    .split(' ')
    .filter(Boolean)
    .map((part) => part[0])
    .join('')
    .slice(0, 2)
    .toUpperCase();
  const roleLabel = isAdmin ? 'ADMIN · LOCAL' : 'SIGNED IN';

  return (
    <div className="min-h-screen bg-base-200">
      <header className="border-b border-black/10 bg-[var(--admin-blue)] text-white shadow-sm">
        <div className="container mx-auto flex items-center justify-between gap-4 px-4 py-3 sm:px-6 lg:px-8">
          <Link className="flex items-center gap-3" to="/">
            <img
              alt="CAES"
              className="h-10 w-10 rounded-full bg-white/10 object-cover p-1"
              src="/caes.svg"
            />
            <div className="leading-tight">
              <div className="text-sm font-semibold uppercase tracking-[0.2em] text-[var(--admin-gold)]">
                Leaves
              </div>
              <div className="text-xs text-white/70">Administrative workspace</div>
            </div>
          </Link>

          <div className="flex items-center gap-3">
            <div className="flex items-center gap-3 text-right">
              <div className="hidden sm:block">
                <div className="text-sm font-semibold">{user.name}</div>
                <div className="text-xs font-semibold uppercase tracking-[0.16em] text-[var(--admin-gold)]">
                  {roleLabel}
                </div>
              </div>
              <div className="flex h-11 w-11 items-center justify-center rounded-full bg-[var(--admin-gold)] text-sm font-bold text-[var(--admin-blue)]">
                {initials || '?'}
              </div>
            </div>
            <span className="hidden text-white/70 sm:block">▾</span>
          </div>
        </div>
      </header>

      {showSecondaryNav ? (
        <nav className="border-b border-base-300 bg-base-100">
          <div className="container mx-auto flex gap-1 overflow-x-auto px-4 sm:px-6 lg:px-8">
            {items.map((item) => (
              <Link
                activeOptions={{ exact: item.to === '/' }}
                className="border-b-2 border-transparent px-5 py-4 text-sm font-semibold text-base-content/60 transition hover:text-base-content data-[status=active]:border-[var(--admin-blue)] data-[status=active]:text-[var(--admin-blue)]"
                key={item.to}
                to={item.to}
              >
                {item.label}
              </Link>
            ))}
          </div>
        </nav>
      ) : null}

      <main>{children}</main>
    </div>
  );
};
