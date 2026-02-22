import { Link } from 'react-router-dom';
import { useOrderRounds } from '@/features/order-rounds/hooks/use-order-rounds';

export function OrderRoundsPage() {
  const { data: rounds = [], isLoading } = useOrderRounds();

  return (
    <div className="mx-auto max-w-3xl px-6 py-8">
      <div className="mb-6 flex items-center justify-between">
        <h1 className="text-2xl font-bold text-gray-900">Order Rounds</h1>
        <Link
          to="/rounds/new"
          className="inline-block rounded-md bg-blue-600 px-4 py-2 font-medium text-white hover:bg-blue-700"
        >
          Create Order Round
        </Link>
      </div>
      {isLoading ? (
        <p className="text-gray-600">Loading...</p>
      ) : rounds.length === 0 ? (
        <p className="text-gray-600">No order rounds yet. Create one to get started.</p>
      ) : (
        <ul className="space-y-2">
          {rounds.map((r) => (
            <li key={r.id}>
              <Link
                to={`/rounds/${r.id}`}
                className="block rounded-lg bg-white p-4 shadow-sm transition-shadow hover:shadow-md"
              >
                <strong className="block text-gray-900">{r.restaurantName}</strong>
                <span className="block text-sm text-gray-500">
                  Deadline: {new Date(r.deadline).toLocaleString()}
                </span>
                <span className="block text-sm text-gray-500">Status: {r.status}</span>
                <span className="block text-sm text-gray-500">Items: {r.itemCount ?? 0}</span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
