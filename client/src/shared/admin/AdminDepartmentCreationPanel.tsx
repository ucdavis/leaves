import { useRef, useState } from 'react';
import {
  autoUpdate,
  flip,
  FloatingFocusManager,
  offset,
  shift,
  size,
  useClick,
  useDismiss,
  useFloating,
  useInteractions,
  useListNavigation,
  useRole,
} from '@floating-ui/react';
import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import type { AdminCluster, ApprovalMode } from '@/shared/admin/adminData.ts';
import {
  departmentOptionsQueryOptions,
  type CreateDepartmentInput,
  type DepartmentOption,
} from '@/queries/adminDepartments.ts';
import { useAppForm } from '@/shared/forms/formContext.tsx';
import { HttpError } from '@/lib/api.ts';

const DEPARTMENT_CODE_MAX_LENGTH = 10;
const DEPARTMENT_NAME_MAX_LENGTH = 100;
const CLUSTER_NAME_MAX_LENGTH = 100;

const clusterFormSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, 'Cluster name is required.')
    .max(
      CLUSTER_NAME_MAX_LENGTH,
      `Cluster name must be ${CLUSTER_NAME_MAX_LENGTH} characters or fewer.`
    ),
});

const departmentFormSchema = z.object({
  approvalMode: z.enum(['approval', 'auto']),
  clusterId: z.string().min(1, 'Cluster is required.'),
  code: z
    .string()
    .trim()
    .min(1, 'Department code is required.')
    .max(
      DEPARTMENT_CODE_MAX_LENGTH,
      `Department code must be ${DEPARTMENT_CODE_MAX_LENGTH} characters or fewer.`
    ),
  name: z
    .string()
    .trim()
    .min(1, 'Department name is required.')
    .max(
      DEPARTMENT_NAME_MAX_LENGTH,
      `Department name must be ${DEPARTMENT_NAME_MAX_LENGTH} characters or fewer.`
    ),
});

export function AdminDepartmentCreationPanel({
  clusters,
  formatError,
  onCreateCluster,
  onCreateDepartment,
  onReactivateCluster,
  onReactivateDepartment,
}: {
  clusters: AdminCluster[];
  formatError: (error: unknown) => string;
  onCreateCluster: (name: string) => Promise<void>;
  onCreateDepartment: (input: CreateDepartmentInput) => Promise<void>;
  onReactivateCluster: (clusterId: string) => Promise<void>;
  onReactivateDepartment: (
    departmentId: string,
    clusterId: string
  ) => Promise<void>;
}) {
  const [clusterError, setClusterError] = useState<string | null>(null);
  const [inactiveClusterId, setInactiveClusterId] = useState<string | null>(
    null
  );
  const [isReactivatingCluster, setIsReactivatingCluster] = useState(false);
  const [departmentError, setDepartmentError] = useState<string | null>(null);
  const [inactiveDepartmentId, setInactiveDepartmentId] = useState<
    string | null
  >(null);
  const [isReactivatingDepartment, setIsReactivatingDepartment] =
    useState(false);
  const [selectedDepartment, setSelectedDepartment] =
    useState<DepartmentOption | null>(null);
  const [isDepartmentCodeOpen, setIsDepartmentCodeOpen] = useState(false);
  const [hasRequestedDepartmentOptions, setHasRequestedDepartmentOptions] =
    useState(false);
  const [activeDepartmentOptionIndex, setActiveDepartmentOptionIndex] =
    useState<number | null>(null);
  const departmentOptionElements = useRef<Array<HTMLElement | null>>([]);
  const { context, floatingStyles, refs } = useFloating({
    middleware: [
      offset(4),
      flip({ padding: 8 }),
      shift({ padding: 8 }),
      size({
        apply({ elements, rects }) {
          elements.floating.style.width = `${rects.reference.width}px`;
        },
      }),
    ],
    onOpenChange: (open) => {
      setIsDepartmentCodeOpen(open);
      setActiveDepartmentOptionIndex(null);
      if (open) {
        setHasRequestedDepartmentOptions(true);
      }
    },
    open: isDepartmentCodeOpen,
    placement: 'bottom-start',
    whileElementsMounted: autoUpdate,
  });
  const click = useClick(context);
  const dismiss = useDismiss(context);
  const role = useRole(context, { role: 'select' });
  const listNavigation = useListNavigation(context, {
    activeIndex: activeDepartmentOptionIndex,
    enabled: true,
    listRef: departmentOptionElements,
    loop: true,
    onNavigate: setActiveDepartmentOptionIndex,
  });
  const { getFloatingProps, getItemProps, getReferenceProps } = useInteractions(
    [click, dismiss, role, listNavigation]
  );

  const clusterForm = useAppForm({
    defaultValues: {
      name: '',
    },
    onSubmit: async ({ value }) => {
      setClusterError(null);
      setInactiveClusterId(null);

      try {
        await onCreateCluster(value.name.trim());
        clusterForm.reset();
      } catch (error) {
        if (
          error instanceof HttpError &&
          error.body &&
          typeof error.body === 'object'
        ) {
          const body = error.body as { inactiveClusterId?: number };
          if (body.inactiveClusterId !== undefined) {
            setInactiveClusterId(String(body.inactiveClusterId));
            setClusterError('This cluster was previously deactivated.');
            return;
          }
        }
        setClusterError(formatError(error));
      }
    },
    validators: {
      onChange: clusterFormSchema,
    },
  });

  const departmentForm = useAppForm({
    defaultValues: {
      approvalMode: 'auto' as ApprovalMode,
      clusterId: '',
      code: '',
      name: '',
    },
    onSubmit: async ({ value }) => {
      setDepartmentError(null);
      setInactiveDepartmentId(null);

      try {
        await onCreateDepartment({
          approvalMode: value.approvalMode,
          clusterId: value.clusterId,
          code: value.code.trim().toUpperCase(),
          name: value.name.trim(),
        });
        departmentForm.reset();
        setSelectedDepartment(null);
      } catch (error) {
        if (
          error instanceof HttpError &&
          error.body &&
          typeof error.body === 'object'
        ) {
          const body = error.body as { inactiveDepartmentCode?: string };
          if (body.inactiveDepartmentCode) {
            setInactiveDepartmentId(body.inactiveDepartmentCode);
            setDepartmentError('This department was previously deactivated.');
            return;
          }
        }
        setDepartmentError(formatError(error));
      }
    },
    validators: {
      onChange: departmentFormSchema,
    },
  });

  const departmentOptions = useQuery({
    ...departmentOptionsQueryOptions(),
    enabled: hasRequestedDepartmentOptions,
  });

  const selectDepartment = (code: string) => {
    setDepartmentError(null);
    setInactiveDepartmentId(null);

    const departmentOption = departmentOptions.data?.find(
      (item) => item.code === code
    );

    if (!departmentOption) {
      setSelectedDepartment(null);
      departmentForm.setFieldValue('code', '');
      departmentForm.setFieldValue('name', '');
      return;
    }

    setSelectedDepartment(departmentOption);
    departmentForm.setFieldValue('code', departmentOption.code);
    departmentForm.setFieldValue('name', departmentOption.description ?? '');
    setIsDepartmentCodeOpen(false);
    setActiveDepartmentOptionIndex(null);
  };

  const reactivateDepartment = async () => {
    if (!inactiveDepartmentId) {
      return;
    }

    setIsReactivatingDepartment(true);

    try {
      await onReactivateDepartment(
        inactiveDepartmentId,
        departmentForm.state.values.clusterId
      );
      setDepartmentError(null);
      setInactiveDepartmentId(null);
      departmentForm.reset();
      setSelectedDepartment(null);
    } catch (error) {
      setDepartmentError(formatError(error));
    } finally {
      setIsReactivatingDepartment(false);
    }
  };

  const reactivateCluster = async () => {
    if (!inactiveClusterId) {
      return;
    }

    setIsReactivatingCluster(true);

    try {
      await onReactivateCluster(inactiveClusterId);
      setClusterError(null);
      setInactiveClusterId(null);
      clusterForm.reset();
    } catch (error) {
      setClusterError(formatError(error));
    } finally {
      setIsReactivatingCluster(false);
    }
  };

  return (
    <>
      <h2 className="text-2xl font-semibold text-primary">
        Add departments and clusters
      </h2>

      <section className="card border border-main-border bg-base-100">
        <div className="card-body p-6">
          <div className="grid gap-8 xl:grid-cols-2">
            <div>
              <h3 className="text-base font-semibold text-base-content">
                Add cluster
              </h3>
              <form
                className="mt-4"
                onSubmit={(event) => {
                  event.preventDefault();
                  void clusterForm.handleSubmit();
                }}
              >
                <clusterForm.AppForm>
                  <clusterForm.AppField name="name">
                    {(field) => (
                      <field.TextField
                        inputClassName="input input-bordered w-full bg-white"
                        label="Cluster name"
                        placeholder="Arts and Humanities"
                      />
                    )}
                  </clusterForm.AppField>

                  {clusterError ? (
                    <div className="mt-4 rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">
                      {clusterError}
                      {inactiveClusterId ? (
                        <button
                          className="btn btn-sm ml-3"
                          disabled={isReactivatingCluster}
                          onClick={() => void reactivateCluster()}
                          type="button"
                        >
                          {isReactivatingCluster
                            ? 'Reactivating cluster...'
                            : 'Reactivate cluster'}
                        </button>
                      ) : null}
                    </div>
                  ) : null}

                  <div className="mt-5 flex justify-end">
                    <clusterForm.SubscribeButton
                      className="btn btn-primary"
                      label="Add cluster"
                      loadingLabel="Adding cluster..."
                    />
                  </div>
                </clusterForm.AppForm>
              </form>
            </div>

            <div>
              <h3 className="text-base font-semibold text-base-content">
                Add department
              </h3>

              <form
                className="mt-4"
                onSubmit={(event) => {
                  event.preventDefault();
                  void departmentForm.handleSubmit();
                }}
              >
                <departmentForm.AppForm>
                  <div className="grid gap-4 sm:grid-cols-2">
                    <div className="sm:col-span-2">
                      <div className="form-control w-full">
                        <label className="label" htmlFor="department-code">
                          <span className="label-text font-medium">
                            Department Code
                          </span>
                        </label>
                        <button
                          className="select select-bordered flex w-full items-center bg-white text-left"
                          id="department-code"
                          ref={refs.setReference}
                          type="button"
                          {...getReferenceProps()}
                        >
                          {selectedDepartment
                            ? `${selectedDepartment.code} — ${selectedDepartment.description ?? 'No description'} (${selectedDepartment.currentFacultyCount} faculty)`
                            : 'Select a department'}
                        </button>
                        {isDepartmentCodeOpen ? (
                          <FloatingFocusManager
                            context={context}
                            initialFocus={-1}
                            modal={false}
                          >
                            <div
                              className="z-30 max-h-64 overflow-y-auto rounded-lg border border-base-300 bg-base-100 shadow-lg"
                              ref={refs.setFloating}
                              style={floatingStyles}
                              {...getFloatingProps()}
                            >
                              {departmentOptions.isPending ? (
                                <div
                                  className="flex items-center gap-2 px-4 py-3 text-sm"
                                  role="status"
                                >
                                  <span
                                    aria-hidden="true"
                                    className="loading loading-spinner loading-sm"
                                  />
                                  Loading departments…
                                </div>
                              ) : departmentOptions.isError ? (
                                <button
                                  className="w-full px-4 py-3 text-left text-sm"
                                  onClick={() =>
                                    void departmentOptions.refetch()
                                  }
                                  type="button"
                                >
                                  Could not load departments. Try again
                                </button>
                              ) : departmentOptions.data.length === 0 ? (
                                <div
                                  className="px-4 py-3 text-sm"
                                  role="status"
                                >
                                  Department list is not available.
                                </div>
                              ) : (
                                departmentOptions.data.map(
                                  (departmentOption, index) => (
                                    <button
                                      aria-selected={
                                        selectedDepartment?.code ===
                                        departmentOption.code
                                      }
                                      className="block w-full px-4 py-3 text-left text-sm hover:bg-base-200 focus:bg-base-200"
                                      key={departmentOption.code}
                                      ref={(node) => {
                                        departmentOptionElements.current[
                                          index
                                        ] = node;
                                      }}
                                      type="button"
                                      {...getItemProps({
                                        onClick: () =>
                                          selectDepartment(
                                            departmentOption.code
                                          ),
                                      })}
                                      role="option"
                                    >
                                      {departmentOption.code} —{' '}
                                      {departmentOption.description ??
                                        'No description'}{' '}
                                      ({departmentOption.currentFacultyCount}{' '}
                                      faculty)
                                    </button>
                                  )
                                )
                              )}
                            </div>
                          </FloatingFocusManager>
                        ) : null}
                      </div>
                    </div>

                    <departmentForm.AppField name="name">
                      {(field) => (
                        <field.TextField
                          inputClassName="input input-bordered w-full bg-white"
                          label="Department name"
                          placeholder="Agricultural Sciences"
                        />
                      )}
                    </departmentForm.AppField>

                    <departmentForm.AppField name="clusterId">
                      {(field) => (
                        <field.SelectField
                          label="Cluster"
                          options={clusters.map((cluster) => ({
                            label: cluster.name,
                            value: cluster.id,
                          }))}
                          placeholder="Select a cluster"
                          selectClassName="select select-bordered w-full bg-white"
                        />
                      )}
                    </departmentForm.AppField>

                    <departmentForm.AppField name="approvalMode">
                      {(field) => (
                        <field.SelectField
                          label="Approval mode"
                          options={[
                            {
                              label: 'Required',
                              value: 'approval',
                            },
                            {
                              label: 'Auto-approve',
                              value: 'auto',
                            },
                          ]}
                          selectClassName="select select-bordered w-full bg-white"
                        />
                      )}
                    </departmentForm.AppField>
                  </div>

                  {departmentError ? (
                    <div className="mt-4 rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">
                      {departmentError}
                      {inactiveDepartmentId ? (
                        <button
                          className="btn btn-sm ml-3"
                          disabled={isReactivatingDepartment}
                          onClick={() => void reactivateDepartment()}
                          type="button"
                        >
                          {isReactivatingDepartment
                            ? 'Reactivating department...'
                            : 'Reactivate department'}
                        </button>
                      ) : null}
                    </div>
                  ) : null}

                  <div className="mt-5 flex justify-end">
                    <departmentForm.SubscribeButton
                      className="btn btn-primary"
                      label="Add department"
                      loadingLabel="Adding department..."
                    />
                  </div>
                </departmentForm.AppForm>
              </form>
            </div>
          </div>
        </div>
      </section>
    </>
  );
}
