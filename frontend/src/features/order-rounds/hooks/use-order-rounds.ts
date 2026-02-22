import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';
import type {
  OrderRoundResponse,
  OrderRoundDetailResponse,
} from '@/features/order-rounds/types/order-round';

export const orderRoundKeys = {
  all: ['orderrounds'] as const,
  list: () => [...orderRoundKeys.all, 'list'] as const,
  detail: (id: string) => [...orderRoundKeys.all, id] as const,
  export: (id: string) => [...orderRoundKeys.all, id, 'export'] as const,
};

export function useOrderRounds() {
  return useQuery({
    queryKey: orderRoundKeys.list(),
    queryFn: async () => {
      const { data } = await api.get<OrderRoundResponse[]>('/orderrounds');
      return data;
    },
  });
}

export function useOrderRoundDetail(id: string | undefined) {
  return useQuery({
    queryKey: orderRoundKeys.detail(id ?? ''),
    queryFn: async () => {
      const { data } = await api.get<OrderRoundDetailResponse>(`/orderrounds/${id}`);
      return data;
    },
    enabled: !!id,
  });
}

export function useOrderRoundExport(id: string | undefined) {
  return useQuery({
    queryKey: orderRoundKeys.export(id ?? ''),
    queryFn: async () => {
      const { data } = await api.get<OrderRoundDetailResponse>(`/orderrounds/${id}/export`);
      return data;
    },
    enabled: !!id,
  });
}

export function useCreateOrderRound() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: {
      restaurantName: string;
      restaurantUrl: string | null;
      deadline: string;
    }) => {
      const { data } = await api.post<{ id: number }>('/orderrounds', payload);
      return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: orderRoundKeys.list() });
    },
  });
}

export function useAddOrderItem(orderRoundId: string | undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: {
      description: string;
      price: number;
      notes: string | null;
    }) => {
      await api.post(`/orderrounds/${orderRoundId}/items`, payload);
    },
    onSuccess: () => {
      if (orderRoundId) {
        queryClient.invalidateQueries({ queryKey: orderRoundKeys.detail(orderRoundId) });
      }
    },
  });
}

export function useRemoveOrderItem(orderRoundId: string | undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (itemId: number) => {
      await api.delete(`/orderrounds/${orderRoundId}/items/${itemId}`);
    },
    onSuccess: () => {
      if (orderRoundId) {
        queryClient.invalidateQueries({ queryKey: orderRoundKeys.detail(orderRoundId) });
      }
    },
  });
}

export function useCloseOrderRound(orderRoundId: string | undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      await api.put(`/orderrounds/${orderRoundId}`, { close: true });
    },
    onSuccess: () => {
      if (orderRoundId) {
        queryClient.invalidateQueries({ queryKey: orderRoundKeys.detail(orderRoundId) });
        queryClient.invalidateQueries({ queryKey: orderRoundKeys.list() });
      }
    },
  });
}
