import { useState } from 'react';
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
  onReactivateDepartment,
}: {
  clusters: AdminCluster[];
  formatError: (error: unknown) => string;
  onCreateCluster: (name: string) => Promise<void>;
  onCreateDepartment: (input: CreateDepartmentInput) => Promise<void>;
  onReactivateDepartment: (departmentId: string) => Promise<void>;
}) {
  const [clusterError, setClusterError] = useState<string | null>(null);
  const [departmentError, setDepartmentError] = useState<string | null>(null);
  const [inactiveDepartmentId, setInactiveDepartmentId] = useState<string | null>(null);
  const [isReactivatingDepartment, setIsReactivatingDepartment] =
    useState(false);
  const [selectedDepartment, setSelectedDepartment] =
    useState<DepartmentOption | null>(null);

  const clusterForm = useAppForm({
    defaultValues: {
      name: '',
    },
    onSubmit: async ({ value }) => {
      setClusterError(null);

      try {
        await onCreateCluster(value.name.trim());
        clusterForm.reset();
      } catch (error) {
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
        if (error instanceof HttpError && error.body && typeof error.body === 'object') {
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

  const departmentOptions = useQuery(
    departmentOptionsQueryOptions()
  );

  const selectDepartment = (code: string) => {
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
  };

  const reactivateDepartment = async () => {
    if (!inactiveDepartmentId) {
      return;
    }

    setIsReactivatingDepartment(true);

    try {
      await onReactivateDepartment(inactiveDepartmentId);
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
                        <select
                          className="select select-bordered w-full bg-white"
                          disabled={departmentOptions.isFetching}
                          id="department-code"
                          onChange={(event) => selectDepartment(event.target.value)}
                          value={selectedDepartment?.code ?? ''}
                        >
                          <option value="">Select a department</option>
                          {departmentOptions.data?.map((departmentOption) => (
                            <option key={departmentOption.code} value={departmentOption.code}>
                              {departmentOption.code} —{' '}
                              {departmentOption.description ?? 'No description'} ({' '}
                              {departmentOption.currentFacultyCount} faculty)
                            </option>
                          ))}
                        </select>
                      </div>
                      {departmentOptions.isFetching ? (
                        <p className="mt-2 text-sm text-base-content/70">
                          Loading departments…
                        </p>
                      ) : null}
                      {departmentOptions.isError ? (
                        <p className="mt-2 text-sm text-error">
                          {formatError(departmentOptions.error)}
                        </p>
                      ) : null}
                      {departmentOptions.data?.length === 0 ? (
                        <p className="mt-2 text-sm text-base-content/70">
                         Department list is not available.
                        </p>
                      ) : null}
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
