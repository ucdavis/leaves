import { createFileRoute, redirect } from '@tanstack/react-router';

export const Route = createFileRoute('/(authenticated)/admin/status')({
  beforeLoad: () => {
    throw redirect({ replace: true, to: '/admin' });
  },
});
