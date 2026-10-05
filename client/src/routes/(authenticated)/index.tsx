import { createFileRoute, redirect } from '@tanstack/react-router';
import type { RouterContext } from '@/main.tsx';
import { meQueryOptions } from '@/queries/user.ts';
import {
  canAccessApprovalWorkspace,
  canAccessFacultyWorkspace,
  hasAdminRole,
} from '@/shared/auth/roleAccess.ts';

export const Route = createFileRoute('/(authenticated)/')({
  beforeLoad: async ({ context }: { context: RouterContext }) => {
    const user = await context.queryClient.ensureQueryData(meQueryOptions());

    if (canAccessFacultyWorkspace(user.roles)) {
      throw redirect({ replace: true, to: '/dashboard' });
    }

    if (canAccessApprovalWorkspace(user.roles)) {
      throw redirect({ replace: true, to: '/team-calendar' });
    }

    throw redirect({
      replace: true,
      to: hasAdminRole(user.roles) ? '/admin' : '/access-denied',
    });
  },
});
