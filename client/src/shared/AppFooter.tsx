import { BlueLeaves } from './BlueLeaves.js';
import { GoldLeaves } from './GoldLeaves.js';

export function AppFooter() {
  return (
    <footer className="relative mt-16 overflow-hidden py-16">
      <div className="pointer-events-none absolute -left-7 -top-1 hidden md:block">
        <GoldLeaves />
      </div>

      <div className="flex flex-1 justify-center">
        <div className="flex flex-col">
          <a
            className="rounded-md focus:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
            href="https://caes.ucdavis.edu"
            rel="noopener noreferrer"
            target="_blank"
          >
            <img alt="CA&ES wordmark" className="w-76" src="/caes.svg" />
          </a>
          <p className="mt-2 text-center text-sm text-base-content/70">
            created by
            <a
              className="ms-1 underline"
              href="https://computing.caes.ucdavis.edu/"
              rel="noopener noreferrer"
              target="_blank"
            >
              CRU
            </a>
            <span className="mx-2 text-base-content/40">|</span>
            <a
              className="underline"
              href="https://caeshelp.ucdavis.edu/?appname=Leaves"
              rel="noopener noreferrer"
              target="_blank"
            >
              Help
            </a>
          </p>
        </div>
      </div>

      <div className="pointer-events-none absolute -right-10 -bottom-22 hidden md:block">
        <BlueLeaves />
      </div>
    </footer>
  );
}
