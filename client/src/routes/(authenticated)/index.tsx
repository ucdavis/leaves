import { useUser } from '@/shared/auth/UserContext.tsx';
import { createFileRoute } from '@tanstack/react-router';

export const Route = createFileRoute('/(authenticated)/')({
  component: RouteComponent,
});

function RouteComponent() {
  const user = useUser();

  return (
    <div className="min-h-screen bg-base-100">
      <div className="container mx-auto max-w-6xl px-4 py-16 sm:px-6 lg:px-8">
        <header className="mb-16 text-center">
          <div className="mb-8">
            <img
              alt="CAES"
              className="mx-auto"
              height={77}
              src="/caes.svg"
              width={419}
            />
          </div>
          <div className="mx-auto max-w-3xl">
            <h1 className="mb-4 text-5xl font-bold">Hello {user.name}!</h1>
          </div>
        </header>
      </div>
    </div>
  );
}
