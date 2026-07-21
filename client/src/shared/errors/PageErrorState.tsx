type PageErrorStateProps = {
  badge: string;
  code: string;
  description: string;
  title: string;
};

export function PageErrorState({
  badge,
  code,
  description,
  title,
}: PageErrorStateProps) {
  return (
    <main className="min-h-screen bg-base-100 py-16">
      <section className="container flex min-h-[calc(100vh-8rem)] flex-col items-center justify-center text-center">
        <div className="text-7xl font-black tracking-tight text-primary">
          {code}
        </div>
        <p className="mt-3 text-sm font-semibold uppercase tracking-[0.2em] text-base-content/50">
          {badge}
        </p>
        <h1 className="mt-4 text-3xl font-bold text-primary">
          {title}
        </h1>
        <p className="mt-3 text-base leading-7 text-base-content/70">
          {description}
        </p>
      </section>
    </main>
  );
}
