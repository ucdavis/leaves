import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { userEvent } from '@testing-library/user-event';
import { afterEach, expect, test } from 'vitest';
import { http, HttpResponse } from 'msw';
import { Route } from '@/routes/(authenticated)/admin.faculty.tsx';
import { server } from '@/test/mswUtils.ts';

let queryClient: QueryClient;

afterEach(() => {
  cleanup();
  queryClient?.clear();
});

function facultyUser({
  departmentId = 'DEPT',
  departmentOverrideId = '',
  id,
  isActiveInIam,
  name,
}: {
  departmentId?: string;
  departmentOverrideId?: string;
  id: string;
  isActiveInIam: boolean;
  name: string;
}) {
  return {
    active: true,
    departmentId,
    departmentOverrideEndDate: null,
    departmentOverrideId,
    departmentOverrideStartDate: null,
    designation: 'fy',
    email: '',
    employeeId: id,
    hasAppUser: false,
    iamId: id,
    id,
    isActiveInIam,
    name,
    position: 'Professor',
    role: 'faculty',
  };
}

function renderRoute(departmentId = 'DEPT') {
  server.use(
    http.get('/api/admin/faculty', () =>
      HttpResponse.json({
        departments: [
          {
            approvalMode: 'auto',
            chairUserId: null,
            clusterId: null,
            code: 'DEPT',
            id: 'DEPT',
            name: 'Test department',
            routingEmails: [],
          },
        ],
        facultyUsers: [
          facultyUser({
            departmentId,
            departmentOverrideId: 'DEPT',
            id: 'faculty001',
            isActiveInIam: true,
            name: 'Active faculty with override',
          }),
        ],
      })
    ),
    http.get('/api/admin/faculty/with-overrides', () => {
      const { designation: _, ...inactiveFaculty } = facultyUser({
        departmentOverrideId: 'DEPT',
        id: 'former0001',
        isActiveInIam: false,
        name: 'Former faculty with override',
      });

      return HttpResponse.json([inactiveFaculty]);
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
}

test('loads everyone with current department overrides separately', async () => {
  renderRoute();

  expect(
    await screen.findByText('Active faculty with override')
  ).toBeInTheDocument();
  expect(
    screen.getByRole('combobox', { name: 'Filter faculty by department' })
  ).toBeInTheDocument();
  fireEvent.click(
    screen.getByRole('checkbox', {
      name: 'Show Current Department Overrides',
    })
  );

  expect(
    await screen.findByText('Former faculty with override')
  ).toBeInTheDocument();
  expect(
    screen.queryByText('Former faculty without override')
  ).not.toBeInTheDocument();
  expect(
    screen.queryByText('Active faculty with override')
  ).not.toBeInTheDocument();
});

test('explains that an unmapped department is not configured in Leaves', async () => {
  renderRoute('EXTERNAL');

  const notMapped = await screen.findByText('Not mapped');
  await userEvent.setup().hover(notMapped);

  expect(await screen.findByRole('tooltip')).toHaveTextContent(
    'Department is not configured in Leaves.'
  );
});
