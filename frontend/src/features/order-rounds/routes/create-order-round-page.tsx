import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useCreateOrderRound } from '@/features/order-rounds/hooks/use-order-rounds';

export function CreateOrderRoundPage() {
  const navigate = useNavigate();
  const [restaurantName, setRestaurantName] = useState('');
  const [restaurantUrl, setRestaurantUrl] = useState('');
  const [deadline, setDeadline] = useState('');
  const [error, setError] = useState('');
  const createMutation = useCreateOrderRound();

  const defaultDeadline = () => {
    const d = new Date();
    d.setHours(12, 0, 0, 0);
    d.setDate(d.getDate() + 1);
    return d.toISOString().slice(0, 16);
  };

  const handleSubmit: React.SubmitEventHandler<HTMLFormElement> = (e) => {
    e.preventDefault();
    void (async () => {
      setError('');
      try {
        const data = await createMutation.mutateAsync({
          restaurantName,
          restaurantUrl: restaurantUrl || null,
          deadline: new Date(deadline || defaultDeadline()).toISOString(),
        });
        navigate(`/rounds/${data.id}`);
      } catch {
        setError('Failed to create order round');
      }
    })();
  };

  return (
    <div className="mx-auto max-w-3xl px-6 py-8">
      <h1 className="mb-6 text-2xl font-bold text-gray-900">Create Order Round</h1>
      <form onSubmit={handleSubmit} className="rounded-lg bg-white p-6 shadow-sm">
        {error && <p className="mb-4 text-sm text-red-600">{error}</p>}
        <label className="mb-4 block">
          <span className="mb-2 block text-sm font-medium text-gray-700">Restaurant name</span>
          <input
            value={restaurantName}
            onChange={(e) => setRestaurantName(e.target.value)}
            className="w-full rounded-md border border-gray-300 px-3 py-2 text-gray-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            required
          />
        </label>
        <label className="mb-4 block">
          <span className="mb-2 block text-sm font-medium text-gray-700">
            Restaurant URL (optional)
          </span>
          <input
            type="url"
            value={restaurantUrl}
            onChange={(e) => setRestaurantUrl(e.target.value)}
            className="w-full rounded-md border border-gray-300 px-3 py-2 text-gray-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
          />
        </label>
        <label className="mb-4 block">
          <span className="mb-2 block text-sm font-medium text-gray-700">Deadline</span>
          <input
            type="datetime-local"
            value={deadline || defaultDeadline()}
            onChange={(e) => setDeadline(e.target.value)}
            className="w-full rounded-md border border-gray-300 px-3 py-2 text-gray-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
          />
        </label>
        <button
          type="submit"
          disabled={createMutation.isPending}
          className="rounded-md bg-blue-600 px-4 py-2 font-medium text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
        >
          {createMutation.isPending ? 'Creating...' : 'Create'}
        </button>
      </form>
    </div>
  );
}
