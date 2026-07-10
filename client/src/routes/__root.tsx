import { createRootRouteWithContext, Outlet } from '@tanstack/react-router';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { TanStackRouterDevtools } from '@tanstack/react-router-devtools';
import { RouterContext } from '../main.tsx';
import { AnalyticsListener } from '@/shared/analytics/AnalyticsListener.tsx';
import { PageErrorState } from '@/shared/errors/PageErrorState.tsx';

const RootLayout = () => (
  <>
    <AnalyticsListener />
    <Outlet />
    <ReactQueryDevtools buttonPosition="top-right" />
    <TanStackRouterDevtools position="bottom-right" />
  </>
);

export const Route = createRootRouteWithContext<RouterContext>()({
  component: RootLayout,
  notFoundComponent: () => (
    <PageErrorState
      action={{ label: 'Go home', to: '/' }}
      badge="Page missing"
      code="404"
      description="The page you tried to open does not exist, was moved, or is no longer available in this workspace."
      title="We could not find that page"
    />
  ),
});
