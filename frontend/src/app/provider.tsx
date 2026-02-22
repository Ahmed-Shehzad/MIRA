import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AuthProvider } from '@/providers/auth-provider';
import { NotificationHubProvider } from '@/providers/notification-hub-provider';
import type { ReactNode } from 'react';

const queryClient = new QueryClient();

export function AppProvider({ children }: { children: ReactNode }) {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <NotificationHubProvider>{children}</NotificationHubProvider>
      </AuthProvider>
    </QueryClientProvider>
  );
}
