import { afterEach, describe, expect, it, vi } from 'vitest';
import { fetchJson, HttpError } from '@/lib/api.ts';

describe('fetchJson', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('parses RFC 7807 problem responses as JSON', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(
        JSON.stringify({
          errors: {
            department: ['This department has been deactivated.'],
          },
          status: 400,
        }),
        {
          headers: { 'Content-Type': 'application/problem+json' },
          status: 400,
        }
      )
    );

    await expect(fetchJson('/api/faculty/requests')).rejects.toMatchObject({
      body: {
        errors: {
          department: ['This department has been deactivated.'],
        },
        status: 400,
      },
      status: 400,
    } satisfies Partial<HttpError>);
  });
});
