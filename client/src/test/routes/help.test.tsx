import { describe, expect, it } from 'vitest';
import { screen, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mswUtils.ts';
import { renderRoute } from '@/test/routerUtils.tsx';

const user = {
  email: 'test@example.com',
  employeeId: '1000',
  id: 'user-1',
  isEmulating: false,
  kerberos: 'testuser',
  name: 'Test User',
  roles: [],
};

describe('help route', () => {
  it('shows knowledge base and help form resources', async () => {
    server.use(http.get('/api/user/me', () => HttpResponse.json(user)));

    const { cleanup } = renderRoute({ initialPath: '/help' });

    try {
      await screen.findByRole('heading', { level: 1, name: 'Help' });

      const headings = screen.getAllByRole('heading', { level: 2 });
      expect(headings.map((heading) => heading.textContent)).toEqual([
        'Knowledge Base',
        'Help Form',
      ]);

      const knowledgeBaseCard = headings[0].closest('section');
      const helpFormCard = headings[1].closest('section');

      expect(knowledgeBaseCard).not.toBeNull();
      expect(helpFormCard).not.toBeNull();
      expect(
        within(knowledgeBaseCard!).getByRole('link', {
          name: 'Open Knowledge Base',
        })
      ).toHaveAttribute(
        'href',
        'https://computing.caes.ucdavis.edu/documentation/leaves'
      );
      expect(
        within(helpFormCard!).getByRole('link', { name: 'Open Help Form' })
      ).toHaveAttribute(
        'href',
        'https://caeshelp.ucdavis.edu/?appname=Leaves'
      );
    } finally {
      cleanup();
    }
  });
});
