import { createFileRoute } from '@tanstack/react-router';
import { AdminStatusContent } from '@/shared/admin/AdminStatusContent.tsx';
import {
  AdminDataProvider,
  useAdminData,
} from '@/shared/admin/adminData.tsx';

export const Route = createFileRoute('/(authenticated)/admin/status')({
  component: AdminStatusRoute,
});

function AdminStatusRoute() {
  return (
    <AdminDataProvider>
      <AdminStatusRouteContent />
    </AdminDataProvider>
  );
}

function AdminStatusRouteContent() {
  const { clusters, dataSources, departments, statusSnapshot } = useAdminData();

  return (
    <AdminStatusContent
      clusterCount={clusters.length}
      clustersMissingCaos={clusters.filter((cluster) => !cluster.caoUserId).length}
      dataSources={dataSources}
      departmentCount={departments.length}
      departmentsMissingChairs={departments.filter((department) => !department.chairUserId).length}
      statusSnapshot={statusSnapshot}
    />
  );
}
