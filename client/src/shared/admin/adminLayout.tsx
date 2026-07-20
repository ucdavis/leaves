import { Link, Outlet, useRouterState } from '@tanstack/react-router';

const adminTabs = [
  { label: 'Status', to: '/admin/status' },
  { label: 'Users', to: '/admin/users' },
  { label: 'Roles', to: '/admin/roles' },
  { label: 'Departments', to: '/admin/departments' },
] as const;

export function AdminLayout() {
  const pathname = useRouterState({
    select: (state) => state.location.pathname,
  });

  return (
    <div className="min-h-screen bg-[var(--admin-sand)]">
      <section className="border-b border-[var(--admin-border)] bg-[var(--admin-blue)] text-white shadow-sm">
        <div className="mx-auto max-w-7xl px-4 py-4 sm:px-6 lg:px-8">
          <nav className="overflow-x-auto">
            <div className="inline-flex min-w-full gap-2 rounded-2xl border border-white/10 bg-white/6 p-1.5">
              {adminTabs.map((tab) => {
                const isActive = pathname.startsWith(tab.to);

                return (
                  <Link
                    activeOptions={{ exact: tab.to === '/admin/status' }}
                    className={`admin-tab flex-1 rounded-xl px-4 py-2.5 text-center text-sm font-semibold transition ${
                      isActive
                        ? 'bg-white text-[var(--admin-blue)] shadow-sm'
                        : 'text-white/78 hover:bg-white/10 hover:text-white'
                    }`}
                    key={tab.to}
                    to={tab.to}
                  >
                    {tab.label}
                  </Link>
                );
              })}
            </div>
          </nav>
        </div>
      </section>

      <main className="mx-auto max-w-7xl px-4 py-6 sm:px-6 lg:px-8">
        <Outlet />
      </main>
    </div>
  );
}
