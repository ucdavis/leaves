import { useState } from 'react';
import { z } from 'zod';
import type {
  AdminCluster,
  ApprovalMode,
} from '@/shared/admin/adminData.ts';
import type { CreateDepartmentInput } from '@/queries/adminDepartments.ts';
import { useAppForm } from '@/shared/forms/formContext.tsx';

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
  approvalMode: z.enum(['notification', 'approval', 'auto']),
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
      approvalMode: 'notification' as ApprovalMode,
      clusterId: '',
      code: '',
      name: '',
    },
    onSubmit: async ({ value }) => {
      setDepartmentError(null);

      try {
        await onCreateDepartment({
          approvalMode: value.approvalMode,
          clusterId: value.clusterId || null,
          code: value.code.trim().toUpperCase(),
          name: value.name.trim(),
        });
        departmentForm.reset();
      } catch (error) {
        setDepartmentError(formatError(error));
      }
    },
    validators: {
      onChange: departmentFormSchema,
    },
  });

  return (
    <section className="rounded-[1.25rem] border border-[var(--admin-border)] bg-white p-6 shadow-sm">
      <div className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold text-[var(--admin-blue)]">
          Add departments and clusters
        </h2>
      </div>

      <div className="mt-6 grid gap-6 xl:grid-cols-2">
        <div className="rounded-2xl bg-[var(--admin-sand)] p-5">
          <h3 className="text-sm font-semibold uppercase tracking-[0.2em] text-[var(--admin-gold-deep)]">
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
                  className="btn border-0 bg-[var(--admin-gold)] text-[var(--admin-blue)] hover:bg-[var(--admin-gold)]/85"
                  label="Add cluster"
                  loadingLabel="Adding cluster..."
                />
              </div>
            </clusterForm.AppForm>
          </form>
        </div>

        <div className="rounded-2xl bg-[var(--admin-sand)] p-5">
          <h3 className="text-sm font-semibold uppercase tracking-[0.2em] text-[var(--admin-gold-deep)]">
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
                          label: 'Notification only',
                          value: 'notification',
                        },
                        {
                          label: 'Approval required',
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
                  className="btn border-0 bg-[var(--admin-gold)] text-[var(--admin-blue)] hover:bg-[var(--admin-gold)]/85"
                  label="Add department"
                  loadingLabel="Adding department..."
                />
              </div>
            </departmentForm.AppForm>
          </form>
        </div>
      </div>
    </section>
  );
}
