import { useQuery } from '@tanstack/react-query';
import { createFileRoute, Link, redirect } from '@tanstack/react-router';
import { fetchJson } from '@/lib/api.ts';
import { meQueryOptions, type User } from '@/queries/user.ts';
import { useUser } from '@/shared/auth/UserContext.tsx';
import { type RouterContext } from '@/main.tsx';

type AdminStatusResponse = {
  message: string;
};

export const Route = createFileRoute('/(authenticated)/admin')({
  beforeLoad: async ({ context }: { context: RouterContext }) => {
    const user = await context.queryClient.ensureQueryData(meQueryOptions());
    if (!hasAdminRole(user)) {
      throw redirect({ to: '/', replace: true });
    }
  },
  component: AdminRoute,
});

function AdminRoute() {
  const user = useUser();
  const statusQuery = useQuery({
    queryFn: () => fetchJson<AdminStatusResponse>('/api/admin/status'),
    queryKey: ['admin', 'status'],
    staleTime: 5 * 60_000,
  });

  return (
    <div className="min-h-screen bg-base-200 px-4 py-12 sm:px-6 lg:px-8">
      <div className="mx-auto flex w-full max-w-4xl flex-col gap-6">
        <div className="flex items-center justify-between">
          <div>
            <p className="text-sm font-semibold uppercase tracking-[0.2em] text-primary">
              Admin slice
            </p>
            <h1 className="mt-2 text-4xl font-bold text-base-content">
              Admin dashboard
            </h1>
            <p className="mt-3 max-w-2xl text-base-content/70">
              This route is protected by a client guard and backed by a server
              authorization policy.
            </p>
          </div>

          <Link className="btn btn-outline" to="/">
            Back home
          </Link>
        </div>

        <div className="grid gap-4 md:grid-cols-2">
          <section className="rounded-box border border-base-300 bg-base-100 p-6 shadow-sm">
            <h2 className="text-lg font-semibold text-base-content">
              Signed-in user
            </h2>
            <dl className="mt-4 space-y-2 text-sm text-base-content/70">
              <div className="flex justify-between gap-4">
                <dt>Name</dt>
                <dd className="font-medium text-base-content">{user.name}</dd>
              </div>
              <div className="flex justify-between gap-4">
                <dt>Email</dt>
                <dd className="font-medium text-base-content">{user.email}</dd>
              </div>
              <div className="flex justify-between gap-4">
                <dt>Roles</dt>
                <dd className="font-medium text-base-content">
                  {user.roles.length > 0 ? user.roles.join(', ') : 'None'}
                </dd>
              </div>
            </dl>
          </section>

          <section className="rounded-box border border-base-300 bg-base-100 p-6 shadow-sm">
            <h2 className="text-lg font-semibold text-base-content">
              Server policy check
            </h2>
            {statusQuery.isLoading ? (
              <div className="mt-4 flex items-center gap-3 text-sm text-base-content/70">
                <span className="loading loading-spinner loading-sm" />
                Checking access...
              </div>
            ) : (
              <p className="mt-4 text-sm text-base-content/70">
                {statusQuery.data?.message ?? 'Admin status is unavailable.'}
              </p>
            )}
          </section>
        </div>
      </div>
    </div>
  );
}

function hasAdminRole(user: User) {
  return user.roles.some((role) => role.toLowerCase() === 'admin');
}
