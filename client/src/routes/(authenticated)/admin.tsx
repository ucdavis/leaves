import { createFileRoute, redirect } from '@tanstack/react-router';
import { meQueryOptions, type User } from '@/queries/user.ts';
import { AdminDataProvider } from '@/shared/admin/adminData.tsx';
import { AdminLayout } from '@/shared/admin/adminLayout.tsx';
import { type RouterContext } from '@/main.tsx';

export const Route = createFileRoute('/(authenticated)/admin')({
  beforeLoad: async ({
    context,
    location,
  }: {
    context: RouterContext;
    location: { pathname: string };
  }) => {
    const user = await context.queryClient.ensureQueryData(meQueryOptions());
    if (!hasAdminRole(user)) {
      throw redirect({ to: '/', replace: true });
    }

    if (location.pathname === '/admin' || location.pathname === '/admin/') {
      throw redirect({ to: '/admin/status', replace: true });
    }
  },
  component: AdminRoute,
});

function AdminRoute() {
  return (
    <AdminDataProvider>
      <AdminLayout />
    </AdminDataProvider>
  );
}

function hasAdminRole(user: User) {
  return user.roles.some((role) => role.toLowerCase() === 'admin');
}
