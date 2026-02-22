import { useVapidPublicKey, usePushSubscribe } from '../hooks/use-push-subscribe';

export function PushEnableButton() {
  const { data: vapidPublicKey, isLoading: keyLoading } = useVapidPublicKey();
  const subscribe = usePushSubscribe();

  if (keyLoading || !vapidPublicKey) return null;

  const handleClick = () => {
    subscribe.mutate(vapidPublicKey);
  };

  return (
    <button
      type="button"
      onClick={handleClick}
      disabled={subscribe.isPending}
      className="rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50"
    >
      {subscribe.isPending ? 'Enabling...' : 'Enable push notifications'}
    </button>
  );
}
