import {
  createFileRoute,
  Outlet,
  type ErrorComponentProps,
} from '@tanstack/react-router';
import { HttpError } from '../../lib/api.ts';
import { RouterContext } from '../../main.tsx';
import { meQueryOptions } from '../../queries/user.ts';
import { AuthenticatedShell } from '@/shared/auth/AuthenticatedShell.tsx';
import { UserProvider } from '@/shared/auth/UserContext.tsx';
import { PageErrorState } from '@/shared/errors/PageErrorState.tsx';

export const Route = createFileRoute('/(authenticated)')({
  beforeLoad: async ({ context }: { context: RouterContext }) => {
    await context.queryClient.ensureQueryData(meQueryOptions());
  },
  component: () => (
    <UserProvider>
      <AuthenticatedShell>
        <Outlet />
      </AuthenticatedShell>
    </UserProvider>
  ),
  errorComponent: AuthenticatedRouteError,
});

function AuthenticatedRouteError({ error }: ErrorComponentProps<unknown>) {
  if (error instanceof HttpError && error.status === 403) {
    return (
      <PageErrorState
        badge="Restricted access"
        code="403"
        description="This account does not have permission to view the requested area."
        title="This area is restricted"
      />
    );
  }

  return (
    <PageErrorState
      badge="Unavailable"
      code="500"
      description="We could not load this page right now. The service may be temporarily unavailable or the page data may still be loading."
      title="This page is temporarily unavailable"
    />
  );
}
