import {
  createContext,
  useContext,
  type ReactNode,
} from 'react';
import {
  useMutation,
  useQueryClient,
  useSuspenseQuery,
} from '@tanstack/react-query';
import {
  adminFacultyQueryOptions,
  updateAdminFacultyUser,
} from '@/queries/adminFaculty.ts';
import type {
  AdminDepartment,
  AdminUser,
  UpdateUserInput,
} from '@/shared/admin/adminData.ts';

type AdminFacultyDataContextValue = {
  departments: AdminDepartment[];
  facultyUsers: AdminUser[];
  updateUser: (userId: string, updates: UpdateUserInput) => Promise<void>;
};

const AdminFacultyDataContext = createContext<AdminFacultyDataContextValue | null>(
  null
);

export function AdminFacultyDataProvider({
  children,
}: {
  children: ReactNode;
}) {
  const queryClient = useQueryClient();
  const { data } = useSuspenseQuery(adminFacultyQueryOptions());

  const updateUserMutation = useMutation({
    mutationFn: updateAdminFacultyUser,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['admin', 'faculty'] });
    },
  });

  return (
    <AdminFacultyDataContext.Provider
      value={{
        departments: data.departments,
        facultyUsers: data.facultyUsers,
        updateUser: async (userId, updates) => {
          await updateUserMutation.mutateAsync({ updates, userId });
        },
      }}
    >
      {children}
    </AdminFacultyDataContext.Provider>
  );
}

export function useAdminFacultyData() {
  const value = useContext(AdminFacultyDataContext);

  if (!value) {
    throw new Error(
      'useAdminFacultyData must be used within an AdminFacultyDataProvider.'
    );
  }

  return value;
}
