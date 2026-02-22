import { useState, useRef, useEffect, type ReactNode } from 'react';
import { useNotifications, useMarkNotificationRead } from '../hooks/use-notifications';

function formatTime(iso: string) {
  const d = new Date(iso);
  const now = new Date();
  const diff = now.getTime() - d.getTime();
  if (diff < 60000) return 'Just now';
  if (diff < 3600000) return `${Math.floor(diff / 60000)}m ago`;
  if (diff < 86400000) return `${Math.floor(diff / 3600000)}h ago`;
  return d.toLocaleDateString();
}

export function NotificationBell() {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  const { data: notifications = [], isLoading } = useNotifications();
  const markRead = useMarkNotificationRead();

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const unreadCount = notifications.length;

  let notificationContent: ReactNode;
  if (isLoading) {
    notificationContent = (
      <div className="px-4 py-6 text-center text-sm text-gray-500">Loading...</div>
    );
  } else if (notifications.length === 0) {
    notificationContent = (
      <div className="px-4 py-6 text-center text-sm text-gray-500">No new notifications</div>
    );
  } else {
    notificationContent = (
      <ul className="divide-y divide-gray-100">
        {notifications.map((n) => (
          <li key={n.id} className="hover:bg-gray-50">
            <button
              type="button"
              className="w-full px-4 py-3 text-left"
              onClick={() => {
                markRead.mutate(n.id);
                setOpen(false);
              }}
            >
              <p className="text-sm font-medium text-gray-900">{n.title}</p>
              {n.body && <p className="mt-0.5 text-xs text-gray-600 line-clamp-2">{n.body}</p>}
              <p className="mt-1 text-xs text-gray-400">{formatTime(n.createdAt)}</p>
            </button>
          </li>
        ))}
      </ul>
    );
  }

  return (
    <div className="relative" ref={ref}>
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        className="relative rounded-full p-2 text-gray-600 hover:bg-gray-100 hover:text-gray-900"
        aria-label={unreadCount > 0 ? `Notifications (${unreadCount} unread)` : 'Notifications'}
      >
        <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeWidth={2}
            d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9"
          />
        </svg>
        {unreadCount > 0 && (
          <span className="absolute -right-0.5 -top-0.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-red-500 px-1 text-xs font-medium text-white">
            {unreadCount > 99 ? '99+' : unreadCount}
          </span>
        )}
      </button>

      {open && (
        <div className="absolute right-0 top-full z-50 mt-2 w-80 overflow-hidden rounded-lg border border-gray-200 bg-white shadow-lg">
          <div className="border-b border-gray-200 bg-gray-50 px-4 py-3">
            <h3 className="text-sm font-semibold text-gray-900">Notifications</h3>
          </div>
          <div className="max-h-80 overflow-y-auto">{notificationContent}</div>
        </div>
      )}
    </div>
  );
}
