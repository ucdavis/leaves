import { createFileRoute, Link } from '@tanstack/react-router';

export const Route = createFileRoute('/about')({
  component: About,
});

function About() {
  const search = new URLSearchParams(window.location.search);
  const isDevLogin = search.get('devLogin') === '1';

  if (isDevLogin) {
    return (
      <DevLoginPage
        error={search.get('error')}
        returnUrl={normalizeReturnUrl(search.get('returnUrl'))}
      />
    );
  }

  return (
    <div className="min-h-screen bg-linear-to-br from-primary/20 via-secondary/20 to-accent/20 relative overflow-hidden">
      {/* Homepage Link */}
      <div className="absolute top-4 left-4 z-10">
        <Link className="btn btn-ghost btn-sm" to="/">
          <svg
            className="w-4 h-4 mr-2"
            fill="none"
            stroke="currentColor"
            viewBox="0 0 24 24"
            xmlns="http://www.w3.org/2000/svg"
          >
            <path
              d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6"
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
            />
          </svg>
          Home
        </Link>
      </div>

      {/* Floating Elements */}
      <div className="absolute inset-0 overflow-hidden pointer-events-none">
        <div className="absolute top-40 right-20 w-24 h-24 bg-secondary/30 rounded-full animate-pulse"></div>
        <div className="absolute bottom-20 left-20 w-28 h-28 bg-accent/30 rounded-full animate-ping"></div>
      </div>

      {/* Main Content */}
      <div className="flex items-center justify-center min-h-screen px-4 py-12">
        <div className="max-w-4xl mx-auto text-center space-y-12">
          {/* Hero Section */}
          <div className="space-y-6">
            <h1 className="text-6xl font-bold bg-linear-to-r from-primary to-secondary bg-clip-text text-transparent animate-pulse">
              🚀 About Us
            </h1>
            <p className="text-xl text-base-content/80 max-w-2xl mx-auto leading-relaxed">
              UC Davis is the place to be for cutting-edge web development.
            </p>
          </div>
        </div>
      </div>

      {/* Footer */}
      <footer className="footer footer-center p-4 bg-base-300 text-base-content">
        <div>
          <p>Made with ❤️ at UC Davis</p>
        </div>
      </footer>
    </div>
  );
}

function DevLoginPage({
  error,
  returnUrl,
}: {
  error: string | null;
  returnUrl: string;
}) {
  const encodedReturnUrl = encodeURIComponent(returnUrl);
  const options = [
    {
      description: 'Grants the local Admin role for testing admin-only UI.',
      href: `/login?as=admin&returnUrl=${encodedReturnUrl}`,
      label: 'Login as Admin',
    },
    {
      description: 'Simulates a standard signed-in requester with no admin role.',
      href: `/login?as=requester&returnUrl=${encodedReturnUrl}`,
      label: 'Login as Requester',
    },
    {
      description: 'Signs in without app roles so you can verify unauthorized states.',
      href: `/login?as=unauthorized&returnUrl=${encodedReturnUrl}`,
      label: 'Login as Unauthorized User',
    },
    {
      description: 'Runs the normal Entra sign-in flow with your real account.',
      href: `/login?as=self&returnUrl=${encodedReturnUrl}`,
      label: 'Login as Self',
    },
  ];

  return (
    <div className="min-h-screen bg-base-200">
      <div className="container mx-auto flex min-h-screen max-w-3xl flex-col justify-center px-4 py-12">
        <div className="rounded-box border border-base-300 bg-base-100 p-8 shadow-xl">
          <p className="text-sm font-semibold uppercase tracking-[0.2em] text-primary">
            Local Development
          </p>
          <h1 className="mt-3 text-4xl font-bold text-base-content">
            Choose a login
          </h1>
          <p className="mt-3 max-w-2xl text-base-content/70">
            This page is rendered by the React app for local development. Pick
            a persona to test role-based behavior or continue with your real
            Entra sign-in.
          </p>

          {error ? (
            <div className="alert alert-error mt-6">
              <span>{error}</span>
            </div>
          ) : null}

          <div className="mt-8 grid gap-4">
            {options.map((option) => (
              <a
                className="card border border-base-300 bg-base-100 transition hover:border-primary hover:shadow-md"
                href={option.href}
                key={option.href}
              >
                <div className="card-body gap-2">
                  <h2 className="card-title">{option.label}</h2>
                  <p className="text-sm text-base-content/70">
                    {option.description}
                  </p>
                </div>
              </a>
            ))}
          </div>

          <div className="mt-8 flex flex-wrap gap-3">
            <a className="btn btn-primary" href={returnUrl}>
              Continue to {returnUrl}
            </a>
            <Link className="btn btn-ghost" to="/">
              Back home
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}

function normalizeReturnUrl(returnUrl: string | null) {
  if (!returnUrl || !returnUrl.startsWith('/') || returnUrl.startsWith('//')) {
    return '/';
  }

  return returnUrl;
}
