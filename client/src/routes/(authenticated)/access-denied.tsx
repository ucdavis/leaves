import { createFileRoute } from '@tanstack/react-router';

export const Route = createFileRoute('/(authenticated)/access-denied')({
  component: AccessDeniedPage,
});

function AccessDeniedPage() {
  return (
    <section className="container py-10 lg:py-16">
      <div className="mx-auto max-w-2xl rounded-2xl border border-base-300 bg-base-100 p-8 text-center shadow-sm sm:p-12">
        <p className="text-sm font-semibold uppercase tracking-[0.2em] text-base-content/50">
          Access needed
        </p>
        <h1 className="mt-4 text-3xl font-bold text-primary sm:text-4xl">
          You don’t have access to Leaves
        </h1>
        <p className="mt-4 text-base leading-7 text-base-content/70">
          If you need access, contact a Leaves administrator.
        </p>
      </div>
    </section>
  );
}
