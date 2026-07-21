export function AppFooter() {
  return (
    <footer className="mt-16 py-10">
      <div className="container flex justify-center text-center">
        <div className="flex flex-col items-center">
          <a
            className="rounded-md focus:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
            href="https://caes.ucdavis.edu"
            rel="noopener noreferrer"
            target="_blank"
          >
            <img alt="CA&ES wordmark" className="w-52" src="/caes.svg" />
          </a>
          <p className="mt-2 text-sm text-base-content/70">
            created by
            <a
              className="ms-1 underline"
              href="https://computing.caes.ucdavis.edu/"
              rel="noopener noreferrer"
              target="_blank"
            >
              CRU
            </a>
          </p>
        </div>
      </div>
    </footer>
  );
}
