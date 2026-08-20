import { createFileRoute } from '@tanstack/react-router';
import { HttpError } from '@/lib/api.ts';
import { meQueryOptions, type User } from '@/queries/user.ts';
import { AdminLayout } from '@/shared/admin/adminLayout.tsx';
import { hasAdminRole as userHasAdminRole } from '@/shared/auth/roleAccess.ts';
import { type RouterContext } from '@/main.tsx';

export const Route = createFileRoute('/(authenticated)/admin')({
  beforeLoad: async ({
    context,
  }: {
    context: RouterContext;
  }) => {
    const user = await context.queryClient.ensureQueryData(meQueryOptions());
    if (!hasAdminRole(user)) {
      throw new HttpError(403, '/admin');
    }
  },
  component: AdminRoute,
});

function AdminRoute() {
  return <AdminLayout />;
}

function hasAdminRole(user: User) {
  return userHasAdminRole(user.roles);
}
