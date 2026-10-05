import { createFileRoute, redirect } from '@tanstack/react-router';
import { z } from 'zod';
import type { RouterContext } from '@/main.tsx';
import { meQueryOptions } from '@/queries/user.ts';
import {
  canAccessApprovalWorkspace,
  canAccessFacultyWorkspace,
  hasAdminRole,
} from '@/shared/auth/roleAccess.ts';

const rootSearchSchema = z.object({
  calendarDate: z.iso.date().optional(),
});

export const Route = createFileRoute('/(authenticated)/')({
  beforeLoad: async ({
    context,
    location,
  }: {
    context: RouterContext;
    location: { search: Record<string, unknown> };
  }) => {
    const user = await context.queryClient.ensureQueryData(meQueryOptions());
    const parsedSearch = rootSearchSchema.safeParse(location.search);
    const search = parsedSearch.success ? parsedSearch.data : {};

    if (canAccessFacultyWorkspace(user.roles)) {
      throw redirect({ replace: true, search, to: '/dashboard' });
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
