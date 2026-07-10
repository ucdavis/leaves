import { Link } from '@tanstack/react-router';

type PageErrorAction = {
  label: string;
  href?: string;
  to?: string;
  onClick?: () => void;
};

type PageErrorStateProps = {
  action?: PageErrorAction;
  badge: string;
  code: string;
  description: string;
  secondaryAction?: PageErrorAction;
  title: string;
};

export function PageErrorState({
  action,
  badge,
  code,
  description,
  secondaryAction,
  title,
}: PageErrorStateProps) {
  return (
    <main className="min-h-screen bg-white px-4 py-16">
      <section className="mx-auto flex min-h-[calc(100vh-8rem)] w-full max-w-xl flex-col items-center justify-center text-center">
        <div className="text-7xl font-black tracking-tight text-[var(--admin-blue)]">
          {code}
        </div>
        <p className="mt-3 text-sm font-semibold uppercase tracking-[0.2em] text-[var(--admin-ink-soft)]">
          {badge}
        </p>
        <h1 className="mt-4 text-3xl font-bold text-[var(--admin-blue)]">
          {title}
        </h1>
        <p className="mt-3 text-base leading-7 text-[var(--admin-ink-muted)]">
          {description}
        </p>
      </section>
    </main>
  );
}
