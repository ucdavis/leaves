import { HttpError } from '@/lib/api.ts';

export function getAdminMutationErrorMessage(error: unknown) {
  if (error instanceof HttpError) {
    if (typeof error.body === 'string' && error.body.trim()) {
      return error.body;
    }

    if (error.body && typeof error.body === 'object') {
      const body = error.body as {
        detail?: string;
        title?: string;
      };

      if (body.detail) {
        return body.detail;
      }

      if (body.title) {
        return body.title;
      }
    }

    return 'Unable to save the change. Please try again.';
  }

  if (error instanceof Error && error.message) {
    return error.message;
  }

  return 'Unable to save the change. Please try again.';
}
