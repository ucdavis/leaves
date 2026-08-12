import { createFileRoute } from '@tanstack/react-router';
import { FacultyDashboard } from '@/shared/faculty/FacultyDashboard.tsx';

export const Route = createFileRoute('/(authenticated)/')({
  component: RouteComponent,
});

function RouteComponent() {
  return <FacultyDashboard />;
}
