import { useSuspenseQuery } from '@tanstack/react-query';
import { createFileRoute } from '@tanstack/react-router';
import { adminStatusQueryOptions } from '@/queries/adminStatus.ts';
import { AdminStatusContent } from '@/shared/admin/AdminStatusContent.tsx';

export const Route = createFileRoute('/(authenticated)/admin/')({
  component: AdminIndexRoute,
  loader: ({ context }) =>
    context.queryClient.ensureQueryData(adminStatusQueryOptions()),
  pendingComponent: () => (
    <section className="rounded-[1.25rem] border border-[var(--admin-border)] bg-white p-6 shadow-sm">
      <div className="flex items-center gap-4">
        <span
          aria-hidden="true"
          className="loading loading-spinner loading-lg text-primary"
        />
        <div>
          <h2 className="text-lg font-semibold text-[var(--admin-blue)]">
            Loading Status
          </h2>
        </div>
      </div>
    </section>
  ),
});

function AdminIndexRoute() {
  const { data } = useSuspenseQuery(adminStatusQueryOptions());

  return (
    <AdminStatusContent
      clusterCount={data.clusterCount}
      clustersMissingCaos={data.clustersMissingCaos}
      dataSources={data.dataSources}
      departmentCount={data.departmentCount}
      departmentsMissingChairs={data.departmentsMissingChairs}
      statusSnapshot={data.statusSnapshot}
    />
  );
}
