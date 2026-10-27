import { createFileRoute } from '@tanstack/react-router';

type SupportLink = {
  description: string;
  href: string;
  label: string;
};

const supportResources: SupportLink[] = [
  {
    description:
      'Search the knowledge base for guides, answers, and other Leaves documentation.',
    href: 'https://computing.caes.ucdavis.edu/documentation/leaves',
    label: 'Knowledge Base',
  },
  {
    description:
      'For questions, problems, or other help requests, contact the support team.',
    href: 'https://caeshelp.ucdavis.edu/?appname=Leaves',
    label: 'Help Form',
  },
];

export const Route = createFileRoute('/(authenticated)/help')({
  component: HelpPage,
});

function HelpPage() {
  return (
    <div className="container py-8 lg:py-10">
      <div className="mx-auto max-w-3xl space-y-6">
        <div className="space-y-2">
          <h1 className="text-2xl font-bold text-primary sm:text-3xl">Help</h1>
          <p className="text-lg text-base-content/80">
            Find answers in the knowledge base or contact the support team for
            help with Leaves.
          </p>
        </div>

        <div className="grid gap-4">
          {supportResources.map((resource) => (
            <section
              className="rounded-lg border border-base-300 bg-base-100 p-6 shadow-sm"
              key={resource.label}
            >
              <h2 className="text-lg font-bold text-primary">
                {resource.label}
              </h2>
              <p className="mt-2 text-base-content/80">
                {resource.description}
              </p>
              <div className="mt-5">
                <a
                  className="btn btn-primary"
                  href={resource.href}
                  rel="noopener noreferrer"
                  target="_blank"
                >
                  Open {resource.label}
                </a>
              </div>
            </section>
          ))}
        </div>
      </div>
    </div>
  );
}
