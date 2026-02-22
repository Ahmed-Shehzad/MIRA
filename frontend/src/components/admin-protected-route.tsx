import { useAuth } from "@/providers/use-auth";
import { Navigate, useLocation } from "react-router-dom";

const ADMIN_GROUP = "Admins";

export function AdminProtectedRoute({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  const { user, loading } = useAuth();
  const location = useLocation();

  if (loading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-gray-100">
        <p className="text-gray-600">Loading...</p>
      </div>
    );
  }

  if (!user) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  const isAdmin = user.groups?.includes(ADMIN_GROUP) ?? false;
  if (!isAdmin) {
    return <Navigate to="/" replace />;
  }

  return <>{children}</>;
}
