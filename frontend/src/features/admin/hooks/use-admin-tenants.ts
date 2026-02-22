import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';

export interface Tenant {
  id: number;
  name: string;
  slug: string;
  isActive: boolean;
}

export function useAdminTenants() {
  return useQuery({
    queryKey: ['admin', 'tenants'],
    queryFn: async () => {
      const { data } = await api.get<Tenant[]>('/admin/tenants');
      return data;
    },
  });
}
