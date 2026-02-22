import { Link } from 'react-router-dom';
import { useAuth } from '@/providers/auth-provider';
import { NotificationBell } from '@/features/notifications/components/notification-bell';
import { PushEnableButton } from '@/features/notifications/components/push-enable-button';

const ADMIN_GROUP = 'Admins';

interface AppLayoutProps {
  children: React.ReactNode;
}

export function AppLayout({ children }: AppLayoutProps) {
  const { user, logout } = useAuth();
  const isAdmin = user?.groups?.includes(ADMIN_GROUP) ?? false;

  return (
    <div className="min-h-screen bg-gray-50">
      <header className="border-b border-gray-200 bg-white">
        <div className="mx-auto flex max-w-4xl items-center justify-between px-6 py-4">
          <div className="flex items-center gap-6">
            <Link to="/" className="text-xl font-bold text-gray-900">
              HIVE Food Orders
            </Link>
            <nav className="flex gap-4">
              <Link to="/" className="text-sm font-medium text-gray-600 hover:text-gray-900">
                Order Rounds
              </Link>
              <Link to="/rounds/new" className="text-sm font-medium text-gray-600 hover:text-gray-900">
                New Round
              </Link>
              <Link to="/wsi" className="text-sm font-medium text-gray-600 hover:text-gray-900">
                WSI
              </Link>
              {isAdmin && (
                <Link to="/admin" className="text-sm font-medium text-gray-600 hover:text-gray-900">
                  Admin
                </Link>
              )}
            </nav>
          </div>
          <div className="flex items-center gap-3">
            <PushEnableButton />
            <NotificationBell />
            <span className="text-sm text-gray-600">
              {user?.email} ({user?.company})
            </span>
            <button
              onClick={logout}
              className="rounded-md bg-gray-200 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-300"
            >
              Log out
            </button>
          </div>
        </div>
      </header>
      <main>{children}</main>
    </div>
  );
}
