import { useUser } from '@/shared/auth/UserContext.tsx';
import { createFileRoute } from '@tanstack/react-router';

export const Route = createFileRoute('/(authenticated)/')({
  component: RouteComponent,
});

function RouteComponent() {
  const user = useUser();

  return (
    <div className="bg-base-100">
      <div className="container py-16">
        <header className="mb-16 text-center">
          <div className="mb-8">
            <img
              alt="Leaves"
              className="mx-auto"
              height={77}
              src="/leaves-logo.svg"
              width={419}
            />
          </div>
          <div>
            <h1 className="mb-4 text-5xl font-bold">Hello {user.name}!</h1>
          </div>
        </header>
      </div>
    </div>
  );
}
