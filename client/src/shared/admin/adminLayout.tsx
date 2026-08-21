import { Link, Outlet, useRouterState } from '@tanstack/react-router';
import {
  BuildingOffice2Icon,
  ChartBarSquareIcon,
  ShieldCheckIcon,
  UserGroupIcon,
} from '@heroicons/react/24/outline';

const adminTabs = [
  { icon: ChartBarSquareIcon, label: 'Status', to: '/admin' },
  { icon: UserGroupIcon, label: 'Faculty', to: '/admin/faculty' },
  { icon: ShieldCheckIcon, label: 'Manage users', to: '/admin/manage-users' },
  { icon: BuildingOffice2Icon, label: 'Departments', to: '/admin/departments' },
] as const;

export function AdminLayout() {
  const pathname = useRouterState({
    select: (state) => state.location.pathname,
  });

  return (
    <div className="bg-base-200">
      <section className="py-8">
        <div className="container">
          <nav className="overflow-x-auto">
            <div className="inline-flex bg-base-100 border border-primary/10 min-w-full gap-2 rounded-sm p-1">
              {adminTabs.map((tab) => {
                const isActive = pathname.startsWith(tab.to);
                const Icon = tab.icon;

                return (
                <Link
                    activeOptions={{ exact: tab.to === '/admin' }}
                    className={`admin-tab flex flex-1 items-center justify-center gap-2 rounded-sm px-4 py-3 text-center font-semibold transition ${
                      isActive
                        ? 'bg-primary text-primary-content'
                        : 'text-base-content/70 hover:bg-base-200 hover:text-base-content'
                    }`}
                    key={tab.to}
                    to={tab.to}
                  >
                    <Icon aria-hidden="true" className="h-5 w-5 shrink-0" />
                    <span>{tab.label}</span>
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
