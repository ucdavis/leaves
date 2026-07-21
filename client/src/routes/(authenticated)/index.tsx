import { useUser } from '@/shared/auth/UserContext.tsx';
import { createFileRoute } from '@tanstack/react-router';

export const Route = createFileRoute('/(authenticated)/')({
  component: RouteComponent,
});

function RouteComponent() {
  const user = useUser();

  return (
    <div className="container py-16">
      <header className="mb-16 text-center mt-2">
        <div>
          <h1 className="mb-4 text-2xl font-bold">Hello {user.name}!</h1>
        </div>
      </header>
    </div>
  );
}
