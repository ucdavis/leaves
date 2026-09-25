import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeAll, expect, test } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mswUtils.ts';
import { Route } from '@/routes/(authenticated)/admin.manage-users.tsx';
import { adminRolesQueryOptions } from '@/queries/adminRoles.ts';

beforeAll(() => {
  HTMLDialogElement.prototype.showModal = function () {
    this.open = true;
  };
  HTMLDialogElement.prototype.close = function () {
    this.open = false;
  };
});
let queryClient: QueryClient;
afterEach(() => {
  cleanup();
  queryClient?.clear();
});

test('admin picker searches on demand and retains the selected identity across a roles refetch', async () => {
  const queries: string[] = [];
  let saved: unknown;
  server.use(
    http.get('/api/admin/roles', () =>
      HttpResponse.json({
        assignments: [],
        clusters: [],
        departments: [],
        users: [],
      })
    ),
    http.get('/api/admin/roles/admin-candidates', ({ request }) => {
      queries.push(new URL(request.url).searchParams.get('query')!);
      return HttpResponse.json([
        {
          departmentId: null,
          departmentName: null,
          departmentOptions: [],
          email: 'split@example.test',
          iamId: 'split00001',
          name: 'Split Appointment',
        },
      ]);
    }),
    http.post('/api/admin/roles/admins', async ({ request }) => {
      saved = await request.json();
      return new HttpResponse(null, { status: 204 });
    })
  );
  queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  const Component = Route.options.component!;
  render(
    <QueryClientProvider client={queryClient}>
      <Component />
    </QueryClientProvider>
  );
  const input = await screen.findByRole('combobox', { name: 'Person' });
  expect(queries).toEqual([]);
  await act(async () => {
    fireEvent.change(input, { target: { value: 's' } });
  });
  expect(queries).toEqual([]);
  fireEvent.change(input, { target: { value: 'split00001' } });
  fireEvent.click(
    await screen.findByRole('option', { name: /Split Appointment/ })
  );
  expect(queries).toEqual(['split00001']);
  await act(async () => {
    await queryClient.invalidateQueries({
      queryKey: adminRolesQueryOptions().queryKey,
    });
  });
  expect(input).toHaveValue('Split Appointment');
  fireEvent.click(screen.getByRole('button', { name: 'Add admin' }));
  expect(
    screen.getByText('Split Appointment', { selector: 'strong' })
  ).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Add role' }));
  await waitFor(() => expect(saved).toEqual({ iamId: 'split00001' }));
  await waitFor(() => expect(input).toHaveValue(''));
});
