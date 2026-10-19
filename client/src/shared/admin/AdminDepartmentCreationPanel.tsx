import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import type { AdminCluster, ApprovalMode } from '@/shared/admin/adminData.ts';
import {
  departmentCandidatesQueryOptions,
  type CreateDepartmentInput,
  type DepartmentCandidate,
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
  clusterId: z.string(),
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
  const [departmentQuery, setDepartmentQuery] = useState('');
  const [selectedDepartment, setSelectedDepartment] =
    useState<DepartmentCandidate | null>(null);

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
          clusterId: value.clusterId || null,
          code: value.code.trim().toUpperCase(),
          name: value.name.trim(),
        });
        departmentForm.reset();
        setDepartmentQuery('');
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

  const departmentCandidates = useQuery(
    departmentCandidatesQueryOptions(departmentQuery)
  );

  const selectDepartment = (candidate: DepartmentCandidate) => {
    setSelectedDepartment(candidate);
    setDepartmentQuery(`${candidate.code} — ${candidate.description ?? ''}`);
    departmentForm.setFieldValue('code', candidate.code);
    departmentForm.setFieldValue('name', candidate.description ?? '');
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
                      <label className="form-control w-full">
                        <span className="label-text">
                          UCPath Level 5 department
                        </span>
                        <input
                          className="input input-bordered w-full bg-white"
                          onChange={(event) => {
                            setDepartmentQuery(event.target.value);
                            setSelectedDepartment(null);
                            departmentForm.setFieldValue('code', '');
                          }}
                          placeholder="Search code or description"
                          type="search"
                          value={departmentQuery}
                        />
                      </label>
                      {departmentQuery.trim().length > 0 &&
                      departmentQuery.trim().length < 2 ? (
                        <p className="mt-2 text-sm text-base-content/70">
                          Type at least 2 characters.
                        </p>
                      ) : null}
                      {departmentCandidates.isFetching ? (
                        <p className="mt-2 text-sm text-base-content/70">
                          Searching UCPath departments…
                        </p>
                      ) : null}
                      {departmentCandidates.data && !selectedDepartment ? (
                        <div className="mt-2 max-h-56 overflow-y-auto rounded-lg border border-base-300 bg-base-100 p-1">
                          {departmentCandidates.data.map((candidate) => (
                            <button
                              className="flex w-full items-center justify-between gap-4 rounded-md px-3 py-2 text-left hover:bg-base-200"
                              key={candidate.code}
                              onClick={() => selectDepartment(candidate)}
                              type="button"
                            >
                              <span>
                                <span className="font-mono font-semibold">
                                  {candidate.code}
                                </span>
                                <span className="text-base-content/70">
                                  {' — '}
                                  {candidate.description ?? 'No description'}
                                </span>
                              </span>
                            </button>
                          ))}
                          {departmentCandidates.data.length === 0 ? (
                            <p className="px-3 py-2 text-sm text-base-content/70">
                              No matching UCPath Level 5 departments.
                            </p>
                          ) : null}
                        </div>
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
                          placeholder="No cluster"
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
                          onClick={() => {
                            void onReactivateDepartment(inactiveDepartmentId);
                          }}
                          type="button"
                        >
                          Reactivate department
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
