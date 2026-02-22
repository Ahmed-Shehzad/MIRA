import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { ProtectedRoute } from '@/components/protected-route';
import { AdminProtectedRoute } from '@/components/admin-protected-route';
import { AppLayout } from '@/components/app-layout';
import { LoginPage } from '@/features/auth/routes/login-page';
import { RegisterPage } from '@/features/auth/routes/register-page';
import { OrderRoundsPage } from '@/features/order-rounds/routes/order-rounds-page';
import { CreateOrderRoundPage } from '@/features/order-rounds/routes/create-order-round-page';
import { OrderRoundDetailPage } from '@/features/order-rounds/routes/order-round-detail-page';
import { ExportSummaryPage } from '@/features/order-rounds/routes/export-summary-page';
import { AdminDashboardPage } from '@/features/admin/routes/admin-dashboard-page';

export function AppRoutes() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
        <Route
          path="/"
          element={
            <ProtectedRoute>
              <AppLayout>
                <OrderRoundsPage />
              </AppLayout>
            </ProtectedRoute>
          }
        />
        <Route
          path="/rounds/new"
          element={
            <ProtectedRoute>
              <AppLayout>
                <CreateOrderRoundPage />
              </AppLayout>
            </ProtectedRoute>
          }
        />
        <Route
          path="/rounds/:id"
          element={
            <ProtectedRoute>
              <AppLayout>
                <OrderRoundDetailPage />
              </AppLayout>
            </ProtectedRoute>
          }
        />
        <Route
          path="/rounds/:id/export"
          element={
            <ProtectedRoute>
              <AppLayout>
                <ExportSummaryPage />
              </AppLayout>
            </ProtectedRoute>
          }
        />
        <Route
          path="/admin"
          element={
            <AdminProtectedRoute>
              <AppLayout>
                <AdminDashboardPage />
              </AppLayout>
            </AdminProtectedRoute>
          }
        />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}
