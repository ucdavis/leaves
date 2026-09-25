import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
  within,
  waitFor,
} from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeAll, expect, test } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mswUtils.ts';
import { Route } from '@/routes/(authenticated)/admin.departments.tsx';
import { adminDepartmentsQueryOptions } from '@/queries/adminDepartments.ts';
import type { AdminUser } from '@/shared/admin/adminData.ts';

const facultyUser = (id: string, role: AdminUser['role']): AdminUser => ({
  active: true,
  departmentId: 'DEPT',
  departmentOverrideEndDate: '',
  departmentOverrideId: '',
  departmentOverrideStartDate: '',
  designation: role === 'faculty' ? 'fy' : role,
  email: '',
  employeeId: '',
  hasAppUser: false,
  iamId: id,
  id,
  name: `Faculty ${role}`,
  position: 'Professor',
  role,
});

const response = {
  caoUsers: [
    {
      active: true,
      designation: 'cao',
      email: '',
      id: 'current',
      name: 'Staff current CAO',
    },
    {
      active: true,
      designation: 'nfa',
      email: 'staff@example.test',
      id: 'candidate',
      name: 'Staff candidate',
    },
    {
      active: true,
      designation: 'faculty',
      email: '',
      id: 'split',
      name: 'Staff with faculty appointment',
    },
    {
      active: false,
      designation: 'nfa',
      email: '',
      id: 'inactive',
      name: 'Staff inactive',
    },
    {
      active: true,
      designation: 'admin',
      email: '',
      id: 'admin',
      name: 'Staff admin',
    },
    {
      active: true,
      designation: 'chair',
      email: '',
      id: 'chair',
      name: 'Staff chair',
    },
    {
      active: true,
      designation: 'faculty',
      email: '',
      id: 'unknown',
      name: 'Staff unknown',
    },
  ],
  clusters: [{ caoUserId: 'current', id: '1', name: 'Test cluster' }],
  departments: [
    {
      approvalMode: 'approval',
      chairUserId: null,
      clusterId: '1',
      code: 'DEPT',
      id: 'DEPT',
      name: 'Test department',
      routingEmails: [],
    },
  ],
  facultyUsers: [
    facultyUser('faculty', 'faculty'),
    facultyUser('facadmin', 'admin'),
    facultyUser('faccao', 'cao'),
  ],
};

// jsdom does not implement native dialog methods used by WarningModal.
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

function renderDepartments() {
  server.use(
    http.get('/api/admin/departments', () =>
      HttpResponse.json({
        ...response,
        caoUsers: response.caoUsers.filter((user) => user.id === 'current'),
      })
    ),
    http.get('/api/admin/departments/cao-candidates', () =>
      HttpResponse.json(
        response.caoUsers.filter((user) =>
          ['candidate', 'split', 'unknown'].includes(user.id)
        )
      )
    )
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

test('department roster includes faculty admins and CAOs, and excludes directory staff', async () => {
  renderDepartments();
  expect(await screen.findByText('CAO: Staff current CAO')).toBeInTheDocument();
  expect(screen.getByText('3 active users')).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Linked users' }));
  const roster = screen.getByRole('table');
  for (const role of ['faculty', 'admin', 'cao']) {
    expect(within(roster).getByText(`Faculty ${role}`)).toBeInTheDocument();
  }
  expect(within(roster).queryByText(/Staff/)).not.toBeInTheDocument();
});

test.each([
  ['Staff candidate', 'candidate'],
  ['Staff with faculty appointment', 'split'],
  ['Staff unknown', 'unknown'],
])(
  'CAO picker accepts %s and preserves selection through refetch and confirmation',
  async (name, id) => {
    let saved: unknown;
    server.use(
      http.patch('/api/admin/departments/clusters/1', async ({ request }) => {
        saved = await request.json();
        return new HttpResponse(null, { status: 204 });
      })
    );
    renderDepartments();
    fireEvent.click(await screen.findByRole('button', { name: 'Edit CAO' }));
    const search = screen.getByPlaceholderText('Search people');
    fireEvent.change(search, { target: { value: 'Staff' } });
    const candidate = await screen.findByRole('button', {
      name: new RegExp(name),
    });
    for (const name of [
      'Staff inactive',
      'Staff admin',
      'Staff chair',
      'Staff current CAO',
    ]) {
      expect(
        screen.queryByRole('button', { name: new RegExp(name) })
      ).not.toBeInTheDocument();
    }
    fireEvent.click(candidate);
    await act(async () => {
      await queryClient.invalidateQueries({
        queryKey: adminDepartmentsQueryOptions().queryKey,
      });
    });
    expect(search).toHaveValue(name);
    expect(screen.getByRole('button', { name: 'Confirm' })).toBeEnabled();
    fireEvent.click(screen.getByRole('button', { name: 'Confirm' }));
    expect(
      screen.getByText('Staff current CAO', { selector: 'strong' })
    ).toBeInTheDocument();
    expect(screen.getByText(name, { selector: 'strong' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Change CAO' }));
    await waitFor(() =>
      expect(saved).toEqual({ caoUserId: id, caoUserIdSet: true })
    );
    await waitFor(() =>
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    );
  }
);

test('CAO picker waits for two characters and clears a selection when the query changes', async () => {
  renderDepartments();
  fireEvent.click(await screen.findByRole('button', { name: 'Edit CAO' }));
  const search = screen.getByPlaceholderText('Search people');
  await act(async () => {
    fireEvent.change(search, { target: { value: 'S' } });
  });
  expect(screen.getByText('Type at least 2 characters.')).toBeInTheDocument();
  expect(queryClient.isFetching({ queryKey: ['admin', 'caoCandidates'] })).toBe(
    0
  );
  expect(
    screen.queryByRole('button', { name: /Staff candidate/ })
  ).not.toBeInTheDocument();
  fireEvent.change(search, { target: { value: 'Staff' } });
  fireEvent.click(
    await screen.findByRole('button', { name: /Staff candidate/ })
  );
  expect(screen.getByRole('button', { name: 'Confirm' })).toBeEnabled();
  fireEvent.change(search, { target: { value: 'Someone else' } });
  expect(screen.getByRole('button', { name: 'Confirm' })).toBeDisabled();
});

test('CAO picker keeps results from the current query when an older request finishes late', async () => {
  renderDepartments();
  let releaseOld!: () => void;
  let oldStarted!: () => void;
  const oldResponse = new Promise<void>((resolve) => {
    releaseOld = resolve;
  });
  const started = new Promise<void>((resolve) => {
    oldStarted = resolve;
  });
  server.use(
    http.get('/api/admin/departments/cao-candidates', async ({ request }) => {
      const query = new URL(request.url).searchParams.get('query');
      if (query === 'Old') {
        oldStarted();
        await oldResponse;
        return HttpResponse.json([
          {
            active: true,
            designation: 'faculty',
            email: '',
            id: 'old',
            name: 'Old match',
          },
        ]);
      }
      return HttpResponse.json([
        {
          active: true,
          designation: 'faculty',
          email: '',
          id: 'new',
          name: 'New match',
        },
      ]);
    })
  );
  fireEvent.click(await screen.findByRole('button', { name: 'Edit CAO' }));
  const search = screen.getByPlaceholderText('Search people');
  fireEvent.change(search, { target: { value: 'Old' } });
  await started;
  fireEvent.change(search, { target: { value: 'New' } });
  expect(
    await screen.findByRole('button', { name: /New match/ })
  ).toBeInTheDocument();
  await act(async () => {
    releaseOld();
    await oldResponse;
  });
  expect(
    screen.queryByRole('button', { name: /Old match/ })
  ).not.toBeInTheDocument();
  expect(screen.getByRole('button', { name: /New match/ })).toBeInTheDocument();
});
