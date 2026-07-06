import { fetchJson } from '../lib/api.ts';
import { useQuery } from '@tanstack/react-query';

export type User = {
  email: string;
  entraObjectId?: string;
  id: string;
  name: string;
  roles: string[];
};
type UserResponse = Omit<User, 'roles'> & { roles?: string[] };

function normalizeUser(user: UserResponse): User {
  return {
    ...user,
    roles: user.roles ?? [],
  };
}

export const meQueryOptions = () => ({
  queryFn: async (): Promise<User> => {
    const user = await fetchJson<UserResponse>('/api/user/me');
    return normalizeUser(user);
  },
  queryKey: ['users', 'me'] as const,
  staleTime: 5 * 60_000, // 5 minutes
});

export const useMeQuery = () => {
  return useQuery(meQueryOptions());
};
