export function AppFooter() {
  return (
    <footer className="relative mt-16 overflow-hidden py-10">
      <div className="pointer-events-none absolute -left-32 hidden md:block">
        <GoldLeavesSvg />
      </div>

      <div className="flex flex-1 justify-center">
        <div className="flex flex-col">
          <a
            className="rounded-md focus:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
            href="https://caes.ucdavis.edu"
            rel="noopener noreferrer"
            target="_blank"
          >
            <img alt="CA&ES wordmark" className="w-52" src="/caes.svg" />
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

      <div className="pointer-events-none absolute -right-32 -bottom-62 hidden md:block">
        <BlueLeavesSvg />
      </div>
    </footer>
  );
}

function GoldLeavesSvg() {
  return (
    <svg
      aria-hidden="true"
      height={257}
      viewBox="0 0 393 257"
      width={393}
      xmlns="http://www.w3.org/2000/svg"
    >
      <title>Decorative gold leaves</title>
      <g fill="none" fillRule="evenodd">
        <path
          d="M93 257C39 230-6 169-25 84c91 14 160 68 193 145-21 14-47 24-75 28Z"
          fill="#B58E18"
        />
        <path
          d="M91 250C59 201 25 158-11 119"
          opacity=".3"
          stroke="#8C6A00"
          strokeLinecap="round"
          strokeWidth="2"
        />
        <path
          d="M151 257C80 190 78 91 169 7c72 88 68 184-3 250h-15Z"
          fill="#DAAA00"
        />
        <g
          opacity=".32"
          stroke="#8C6A00"
          strokeLinecap="round"
          strokeLinejoin="round"
          strokeWidth="2"
        >
          <path d="M157 254c1-73 6-146 12-220" />
          <path d="M161 202c-15-15-29-30-42-47M161 202c17-17 34-34 51-49" />
          <path d="M164 157c-13-15-25-30-36-45M164 157c15-18 30-35 45-53" />
          <path d="M167 111c-10-12-19-24-27-36M167 111c12-15 23-29 34-43" />
        </g>
        <path
          d="M162 257c27-98 109-161 231-146-15 91-100 145-231 146Z"
          fill="#FFCD00"
        />
        <g
          opacity=".3"
          stroke="#A67C00"
          strokeLinecap="round"
          strokeLinejoin="round"
          strokeWidth="2"
        >
          <path d="M177 252c54-51 117-92 189-124" />
          <path d="M235 211c0-14 3-27 8-40M235 211c15 4 30 9 45 16" />
          <path d="M286 176c2-12 6-24 12-36M286 176c14 1 29 5 43 10" />
        </g>
        <path
          d="M151 257c-16-63-4-129 37-192 40 69 35 136-22 192h-15Z"
          fill="#ECBB00"
        />
        <g
          opacity=".32"
          stroke="#A67C00"
          strokeLinecap="round"
          strokeLinejoin="round"
          strokeWidth="2"
        >
          <path d="M160 252c5-58 14-112 28-167" />
          <path d="M168 200c-9-9-17-18-24-28M168 200c12-11 24-21 36-30" />
          <path d="M178 151c-6-8-12-16-17-24M178 151c9-10 18-19 27-28" />
        </g>
      </g>
    </svg>
  );
}

function BlueLeavesSvg() {
  return (
    <svg
      aria-hidden="true"
      fill="none"
      height={479}
      viewBox="0 0 504 479"
      width={504}
      xmlns="http://www.w3.org/2000/svg"
    >
      <title>Decorative blue leaves</title>
      <g fill="none" fillRule="evenodd">
        <path
          d="M444 399C362 350 296 249 261 94c103 67 164 169 183 305Z"
          fill="#1D4A83"
        />
        <path
          d="M268 108c54 89 108 179 161 269"
          opacity=".35"
          stroke="#6C8EAE"
          strokeLinecap="round"
          strokeWidth="2.25"
        />
        <path
          d="M457 398C412 280 414 151 493 35c58 142 48 264-36 363Z"
          fill="#007A98"
        />
        <path
          d="M482 58c-13 108-23 214-30 319"
          opacity=".35"
          stroke="#9BC2D0"
          strokeLinecap="round"
          strokeWidth="2.25"
        />
        <path
          d="M77 112c111-17 268 51 379 286-168-11-315-116-379-286Z"
          fill="#002855"
        />
        <g
          opacity=".38"
          stroke="#6C8EAE"
          strokeLinecap="round"
          strokeLinejoin="round"
          strokeWidth="2.25"
        >
          <path d="M92 119c113 79 220 167 323 264" />
          <path d="M171 177c-12 12-22 25-31 38M171 177c13-10 26-20 39-29" />
          <path d="M231 223c-13 14-24 28-34 43M231 223c16-13 32-25 48-37" />
          <path d="M294 276c-11 14-20 28-28 42M294 276c17-12 34-24 51-35" />
        </g>
        <path
          d="M147 145c84 14 180 86 285 234-119-32-218-110-285-234Z"
          fill="#0F386C"
        />
        <g
          opacity=".32"
          stroke="#6C8EAE"
          strokeLinecap="round"
          strokeLinejoin="round"
          strokeWidth="2"
        >
          <path d="M158 153c85 66 168 138 249 216" />
          <path d="M231 211c-10 10-19 21-27 32M231 211c13-8 26-16 39-23" />
        </g>

        <g id="blue_float">
          <path
            d="M23 111c13-20 34-28 57-19-10 23-31 36-57 19Z"
            fill="#002855"
          />
          <path
            d="M28 110c14-7 28-12 44-16M41 104l-1-6M41 104l6 3M52 100l1-7M52 100l7 3M63 96l2-5M63 96l6 2"
            opacity=".45"
            stroke="#6C8EAE"
            strokeLinecap="round"
            strokeWidth="1.5"
          />
        </g>
      </g>
    </svg>
  );
}
