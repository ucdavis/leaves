import { Link, Outlet, useRouterState } from '@tanstack/react-router';

const adminTabs = [
  { label: 'Status', to: '/admin/status' },
  { label: 'Users', to: '/admin/users' },
  { label: 'Departments', to: '/admin/departments' },
] as const;

export function AdminLayout() {
  const pathname = useRouterState({
    select: (state) => state.location.pathname,
  });

  return (
    <div className="bg-base-200">
      <section className="border-b border-base-300 bg-primary text-primary-content py-4 mb-8">
        <div className="container py-4">
          <nav className="overflow-x-auto">
            <div className="inline-flex min-w-full gap-2 rounded-sm p-1">
              {adminTabs.map((tab) => {
                const isActive = pathname.startsWith(tab.to);

                return (
                  <Link
                    activeOptions={{ exact: tab.to === '/admin/status' }}
                    className={`admin-tab flex-1 rounded-sm px-4 py-3 text-center font-semibold transition ${
                      isActive
                        ? 'bg-primary-content/90 text-primary shadow-sm'
                        : 'text-primary-content/70 hover:bg-primary-content/10 hover:text-primary-content'
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

      <main className="container">
        <Outlet />
      </main>
    </div>
  );
}
