import { Link } from '@tanstack/react-router';
import { useUser } from './UserContext.tsx';

const navigationItems = [
  { label: 'Home', to: '/' },
  { label: 'My ID', to: '/me' },
  { label: 'Table', to: '/fetch' },
  { label: 'Form', to: '/form' },
  { label: 'Notification', to: '/notification' },
  { label: 'Style Guide', to: '/styles' },
] as const;

export const AuthenticatedShell = ({
  children,
}: {
  children: React.ReactNode;
}) => {
  const user = useUser();
  const isAdmin = user.roles.some((role) => role.toLowerCase() === 'admin');
  const items = isAdmin
    ? [...navigationItems, { label: 'Admin', to: '/admin' as const }]
    : navigationItems;

  return (
    <div className="min-h-screen bg-base-200">
      <header className="border-b border-base-300 bg-base-100/90 backdrop-blur">
        <div className="navbar container mx-auto px-4">
          <div className="navbar-start">
            <Link className="btn btn-ghost text-lg font-semibold" to="/">
              Leaves
            </Link>
          </div>

          <div className="navbar-center hidden lg:flex">
            <ul className="menu menu-horizontal px-1 text-sm">
              {items.map((item) => (
                <li key={item.to}>
                  <Link to={item.to}>{item.label}</Link>
                </li>
              ))}
            </ul>
          </div>

          <div className="navbar-end gap-3">
            <div className="hidden sm:flex flex-col items-end text-right">
              <span className="text-sm font-medium">{user.name}</span>
              <span className="text-xs text-base-content/60">{user.email}</span>
            </div>
          </div>
        </div>
      </header>

      <main>{children}</main>
    </div>
  );
};
