import { createFileRoute } from '@tanstack/react-router';
import { AdminStatusContent } from '@/shared/admin/AdminStatusContent.tsx';
import { useAdminData } from '@/shared/admin/adminData.tsx';

export const Route = createFileRoute('/(authenticated)/admin/status')({
  component: AdminStatusRoute,
});

function AdminStatusRoute() {
  const { dataSources, departments, statusSnapshot } = useAdminData();

  return (
    <AdminStatusContent
      dataSources={dataSources}
      departmentCount={departments.length}
      statusSnapshot={statusSnapshot}
    />
  );
}
