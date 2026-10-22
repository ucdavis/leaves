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
import {
  createMemoryHistory,
  createRouter,
  RouterProvider,
} from '@tanstack/react-router';
import { afterEach, beforeAll, expect, test } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mswUtils.ts';
import { adminDepartmentsQueryOptions } from '@/queries/adminDepartments.ts';
import { routeTree } from '@/routeTree.gen.ts';
import type {
  AdminCluster,
  AdminDepartment,
} from '@/shared/admin/adminData.ts';

const caoCandidates = [
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
];

const response = {
  clusters: [
    {
      caoName: 'Staff current CAO',
      caoUserId: 'current',
      id: '1',
      name: 'Test cluster',
    },
  ],
  departments: [
    {
      approvalMode: 'approval',
      chairName: null,
      chairUserId: null,
      clusterId: '1',
      code: 'DEPT',
      facultyCount: 3,
      hasFaculty: true,
      id: 'DEPT',
      name: 'Test department',
      routingEmails: [],
    },
  ],
  inactiveClusters: [] as AdminCluster[],
  inactiveDepartments: [] as AdminDepartment[],
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

function renderDepartments(
  data = response,
  departmentOptions: Array<{
    code: string;
    currentFacultyCount: number;
    description: string;
  }> = [],
  initialPath = '/admin/departments'
) {
  server.use(
    http.get('/api/user/me', () =>
      HttpResponse.json({
        email: 'admin@example.test',
        id: 'admin',
        isEmulating: false,
        name: 'Test Admin',
        roles: ['Admin'],
      })
    ),
    http.get('/api/admin/departments', () => HttpResponse.json(data)),
    http.get('/api/admin/departments/DEPT/faculty', () =>
      HttpResponse.json([
        { email: '', id: 'faculty', name: 'Faculty faculty' },
        { email: '', id: 'facadmin', name: 'Faculty admin' },
        { email: '', id: 'faccao', name: 'Faculty cao' },
      ])
    ),
    http.get('/api/admin/departments/cao-candidates', () =>
      HttpResponse.json(
        caoCandidates.filter((user) =>
          ['candidate', 'split', 'unknown'].includes(user.id)
        )
      )
    ),
    http.get('/api/admin/departments/options', () =>
      HttpResponse.json(departmentOptions)
    )
  );
  queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  const router = createRouter({
    context: { queryClient },
    history: createMemoryHistory({
      initialEntries: [initialPath],
    }),
    routeTree,
  });

  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  );
}

function getSelectField(label: string) {
  const select = screen
    .getByText(label)
    .closest('.form-control')
    ?.querySelector('select');

  if (!select) {
    throw new Error(`Unable to find the ${label} select field.`);
  }

  return select;
}

test('department roster includes faculty admins and CAOs, and excludes directory staff', async () => {
  renderDepartments();
  expect(await screen.findByText('CAO: Staff current CAO')).toBeInTheDocument();
  expect(screen.getByText('3 faculty')).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Faculty' }));
  const roster = await screen.findByRole('table');
  for (const role of ['faculty', 'admin', 'cao']) {
    expect(within(roster).getByText(`Faculty ${role}`)).toBeInTheDocument();
  }
  expect(within(roster).queryByText(/Staff/)).not.toBeInTheDocument();
});

test('loads only the selected department roster when its Faculty view opens', async () => {
  let rosterRequests = 0;
  renderDepartments();
  server.use(
    http.get('/api/admin/departments/DEPT/faculty', () => {
      rosterRequests += 1;
      return HttpResponse.json([
        { email: '', id: 'faculty', name: 'Faculty faculty' },
      ]);
    })
  );
  expect(await screen.findByText('3 faculty')).toBeInTheDocument();
  expect(rosterRequests).toBe(0);

  fireEvent.click(screen.getByRole('button', { name: 'Faculty' }));
  expect(await screen.findByText('Faculty faculty')).toBeInTheDocument();
  expect(rosterRequests).toBe(1);
});

test('direct department Faculty links request their roster', async () => {
  renderDepartments(response, [], '/admin/departments?peopleFor=DEPT');
  expect(await screen.findByRole('table')).toBeInTheDocument();
  expect(screen.getByText('Faculty faculty')).toBeInTheDocument();
});

test('waits to request department codes until opening the control', async () => {
  let optionRequests = 0;
  let releaseOptions!: () => void;
  const optionsReady = new Promise<void>((resolve) => {
    releaseOptions = resolve;
  });
  renderDepartments();
  server.use(
    http.get('/api/admin/departments/options', async () => {
      optionRequests += 1;
      await optionsReady;
      return HttpResponse.json([
        { code: 'PLS', currentFacultyCount: 3, description: 'Plant Sciences' },
      ]);
    })
  );
  const codeControl = await screen.findByLabelText('Department Code');
  expect(optionRequests).toBe(0);
  fireEvent.click(codeControl);
  expect(await screen.findByRole('status')).toHaveTextContent(
    'Loading departments'
  );
  expect(optionRequests).toBe(1);
  await act(async () => {
    releaseOptions();
  });
  expect(
    await screen.findByRole('option', {
      name: /PLS.*Plant Sciences.*3 faculty/,
    })
  ).toBeInTheDocument();
});

test('opens department options with the keyboard', async () => {
  renderDepartments(response, [
    { code: 'PLS', currentFacultyCount: 3, description: 'Plant Sciences' },
  ]);

  const codeControl = await screen.findByLabelText('Department Code');
  codeControl.focus();
  fireEvent.keyDown(codeControl, { key: 'ArrowDown' });

  expect(
    await screen.findByRole('option', {
      name: /PLS.*Plant Sciences.*3 faculty/,
    })
  ).toBeInTheDocument();
});

test('marks departments without faculty', async () => {
  renderDepartments({
    ...response,
    departments: [
      {
        ...response.departments[0],
        facultyCount: 0,
        hasFaculty: false,
      },
    ],
  });

  expect(
    await screen.findByLabelText('No faculty are assigned to this department')
  ).toBeInTheDocument();
});

test('department creation requires a selected UCPath Level 5 department and prefills its name', async () => {
  let saved: unknown;
  server.use(
    http.post('/api/admin/departments', async ({ request }) => {
      saved = await request.json();
      return new HttpResponse(null, { status: 204 });
    })
  );
  renderDepartments(response, [
    {
      code: 'PLS',
      currentFacultyCount: 3,
      description: 'Plant Sciences',
    },
  ]);

  const departmentSelect = await screen.findByLabelText('Department Code');
  fireEvent.click(departmentSelect);
  fireEvent.click(
    await screen.findByRole('option', {
      name: /PLS.*Plant Sciences.*3 faculty/,
    })
  );
  expect(screen.getByDisplayValue('Plant Sciences')).toBeInTheDocument();
  fireEvent.change(getSelectField('Cluster'), { target: { value: '1' } });
  fireEvent.click(screen.getByRole('button', { name: 'Add department' }));

  await waitFor(() =>
    expect(saved).toEqual({
      approvalMode: 'auto',
      clusterId: 1,
      code: 'PLS',
      name: 'Plant Sciences',
    })
  );
});

test('department creation requires a cluster before it submits', async () => {
  let didSubmit = false;
  server.use(
    http.post('/api/admin/departments', () => {
      didSubmit = true;
      return new HttpResponse(null, { status: 204 });
    })
  );
  renderDepartments(response, [
    {
      code: 'PLS',
      currentFacultyCount: 3,
      description: 'Plant Sciences',
    },
  ]);

  const departmentSelect = await screen.findByLabelText('Department Code');
  fireEvent.click(departmentSelect);
  fireEvent.click(
    await screen.findByRole('option', {
      name: /PLS.*Plant Sciences.*3 faculty/,
    })
  );
  fireEvent.click(screen.getByRole('button', { name: 'Add department' }));

  expect(await screen.findByText('Cluster is required.')).toBeInTheDocument();
  expect(didSubmit).toBe(false);
});

test('department creation reports reactivation failures and clears the prompt after success', async () => {
  let reactivationAttempts = 0;
  let reactivationInput: unknown;
  server.use(
    http.post('/api/admin/departments', () =>
      HttpResponse.json({ inactiveDepartmentCode: 'PLS' }, { status: 409 })
    ),
    http.post('/api/admin/departments/PLS/reactivate', async ({ request }) => {
      reactivationAttempts += 1;
      reactivationInput = await request.json();
      return reactivationAttempts === 1
        ? new HttpResponse('Unable to reactivate department.', { status: 500 })
        : new HttpResponse(null, { status: 204 });
    })
  );
  renderDepartments(response, [
    {
      code: 'PLS',
      currentFacultyCount: 3,
      description: 'Plant Sciences',
    },
  ]);

  const departmentSelect = await screen.findByLabelText('Department Code');
  fireEvent.click(departmentSelect);
  fireEvent.click(
    await screen.findByRole('option', {
      name: /PLS.*Plant Sciences.*3 faculty/,
    })
  );
  fireEvent.change(getSelectField('Cluster'), { target: { value: '1' } });
  fireEvent.click(screen.getByRole('button', { name: 'Add department' }));

  const reactivate = await screen.findByRole('button', {
    name: 'Reactivate department',
  });
  fireEvent.click(reactivate);
  await waitFor(() => expect(reactivationInput).toEqual({ clusterId: 1 }));
  expect(
    await screen.findByText('Unable to reactivate department.')
  ).toBeInTheDocument();

  fireEvent.click(
    screen.getByRole('button', { name: 'Reactivate department' })
  );

  await waitFor(() =>
    expect(
      screen.queryByRole('button', { name: /Reactivate department/ })
    ).not.toBeInTheDocument()
  );
  expect(departmentSelect).toHaveTextContent('Select a department');
});

test('inactive departments direct admins to the add department form', async () => {
  renderDepartments({
    ...response,
    inactiveDepartments: [
      {
        approvalMode: 'auto',
        chairName: null,
        chairUserId: null,
        clusterId: null,
        code: 'OLD',
        facultyCount: 0,
        hasFaculty: false,
        id: 'OLD',
        name: 'Former department',
        routingEmails: [],
      },
    ],
  });

  expect(
    await screen.findByText('Reactivate this department from the form above.')
  ).toBeInTheDocument();
  expect(
    screen.queryByRole('button', { name: 'Reactivate department' })
  ).not.toBeInTheDocument();
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
