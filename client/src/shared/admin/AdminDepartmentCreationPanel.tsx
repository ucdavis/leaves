import { useEffect, useRef, useState } from 'react';
import { z } from 'zod';
import type { AdminCluster, ApprovalMode } from '@/shared/admin/adminData.ts';
import type { CreateDepartmentInput } from '@/queries/adminDepartments.ts';
import { HttpError } from '@/lib/api.ts';
import { useAppForm } from '@/shared/forms/formContext.tsx';

const DEPARTMENT_CODE_MAX_LENGTH = 10;
const DEPARTMENT_NAME_MAX_LENGTH = 100;
const CLUSTER_NAME_MAX_LENGTH = 100;
const INACTIVE_DEPARTMENT_CODE_CONFLICT = 'inactive_department_code';
const HELP_FORM_URL = 'https://caeshelp.ucdavis.edu/?appname=Leaves';

function isInactiveDepartmentCodeConflict(error: unknown): boolean {
  if (!(error instanceof HttpError) || !error.body || typeof error.body !== 'object') {
    return false;
  }

  const body = error.body as { code?: unknown };
  return body.code === INACTIVE_DEPARTMENT_CODE_CONFLICT;
}

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
}: {
  clusters: AdminCluster[];
  formatError: (error: unknown) => string;
  onCreateCluster: (name: string) => Promise<void>;
  onCreateDepartment: (input: CreateDepartmentInput) => Promise<void>;
}) {
  const [clusterError, setClusterError] = useState<string | null>(null);
  const [departmentError, setDepartmentError] = useState<string | null>(null);
  const [inactiveDepartmentCode, setInactiveDepartmentCode] = useState<
    string | null
  >(null);
  const inactiveDepartmentCodeDialogRef = useRef<HTMLDialogElement | null>(
    null
  );

  useEffect(() => {
    const dialog = inactiveDepartmentCodeDialogRef.current;
    if (!dialog || !inactiveDepartmentCode || dialog.open) {
      return;
    }

    dialog.showModal();
  }, [inactiveDepartmentCode]);

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
      setInactiveDepartmentCode(null);

      try {
        await onCreateDepartment({
          approvalMode: value.approvalMode,
          clusterId: value.clusterId || null,
          code: value.code.trim().toUpperCase(),
          name: value.name.trim(),
        });
        departmentForm.reset();
      } catch (error) {
        if (isInactiveDepartmentCodeConflict(error)) {
          setInactiveDepartmentCode(value.code.trim().toUpperCase());
          return;
        }

        setDepartmentError(formatError(error));
      }
    },
    validators: {
      onChange: departmentFormSchema,
    },
  });

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
                    <departmentForm.AppField name="code">
                      {(field) => (
                        <field.TextField
                          inputClassName="input input-bordered w-full bg-white uppercase"
                          label="Department code"
                          placeholder="000000"
                        />
                      )}
                    </departmentForm.AppField>

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

      {inactiveDepartmentCode ? (
        <dialog
          aria-labelledby="inactive-department-code-title"
          aria-modal="true"
          className="fixed inset-0 z-50 m-0 flex h-dvh w-dvw max-h-none max-w-none items-center justify-center border-0 bg-transparent p-4 backdrop:bg-slate-950/40"
          onCancel={() => setInactiveDepartmentCode(null)}
          onClick={(event) => {
            if (event.target === event.currentTarget) {
              setInactiveDepartmentCode(null);
            }
          }}
          ref={inactiveDepartmentCodeDialogRef}
        >
          <div className="w-full max-w-lg rounded-[1.5rem] border border-base-300 bg-base-100 p-6 shadow-2xl">
            <h2
              className="text-xl font-semibold text-base-content"
              id="inactive-department-code-title"
            >
              Department code is inactive
            </h2>
            <p className="mt-3 text-sm text-base-content/80">
              The department code <strong>{inactiveDepartmentCode}</strong> is associated
              with a de-activated department. Contact support to request
              department reactivation.
            </p>
            <div className="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
              <button
                className="btn btn-ghost"
                onClick={() => setInactiveDepartmentCode(null)}
                type="button"
              >
                Dismiss
              </button>
              <a
                className="btn btn-primary"
                href={HELP_FORM_URL}
                rel="noopener noreferrer"
                target="_blank"
              >
                Open Help Form
              </a>
            </div>
          </div>
        </dialog>
      ) : null}
    </>
  );
}
