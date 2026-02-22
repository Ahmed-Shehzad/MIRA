import { useState, useEffect } from 'react';
import { useParams, Link } from 'react-router-dom';
import { useAuth } from '@/providers/auth-provider';
import {
  useOrderRoundDetail,
  useAddOrderItem,
  useRemoveOrderItem,
  useCloseOrderRound,
} from '@/features/order-rounds/hooks/use-order-rounds';

export function OrderRoundDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { user } = useAuth();
  const { data: round, isLoading } = useOrderRoundDetail(id);
  const addItemMutation = useAddOrderItem(id);
  const removeItemMutation = useRemoveOrderItem(id);
  const closeMutation = useCloseOrderRound(id);

  const [desc, setDesc] = useState('');
  const [price, setPrice] = useState('');
  const [notes, setNotes] = useState('');

  const isCreator = round?.createdByUserEmail === user?.email;
  const isOpen = round?.status === 'Open';
  const deadlinePassed = round ? new Date(round.deadline) < new Date() : false;
  const canAddItems = isOpen && !deadlinePassed;

  const handleAddItem: React.SubmitEventHandler<HTMLFormElement> = (e) => {
    e.preventDefault();
    if (!id || !canAddItems) return;
    void (async () => {
      try {
        await addItemMutation.mutateAsync({
          description: desc,
          price: Number.parseFloat(price) || 0,
          notes: notes || null,
        });
        setDesc('');
        setPrice('');
        setNotes('');
      } catch {
        // ignore
      }
    })();
  };

  async function handleRemoveItem(itemId: number) {
    if (!id || !canAddItems) return;
    try {
      await removeItemMutation.mutateAsync(itemId);
    } catch {
      // ignore
    }
  }

  async function handleClose() {
    if (!id || !isCreator) return;
    try {
      await closeMutation.mutateAsync();
    } catch {
      // ignore
    }
  }

  const total = round?.items.reduce((sum, i) => sum + i.price, 0) ?? 0;
  const [timeLeft, setTimeLeft] = useState(0);

  useEffect(() => {
    if (!round || !isOpen || deadlinePassed) {
      const id = setTimeout(() => setTimeLeft(0), 0);
      return () => clearTimeout(id);
    }
    const update = () =>
      setTimeLeft(
        Math.max(0, Math.floor((new Date(round.deadline).getTime() - Date.now()) / 60000)),
      );
    const timeoutId = setTimeout(update, 0);
    const intervalId = setInterval(update, 60000);
    return () => {
      clearTimeout(timeoutId);
      clearInterval(intervalId);
    };
  }, [round, isOpen, deadlinePassed]);

  if (isLoading || !round) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-gray-100">
        <p className="text-gray-600">Loading...</p>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-3xl px-6 py-8">
      <header className="mb-6">
        <Link to="/" className="text-blue-600 hover:text-blue-700">
          ← Back to rounds
        </Link>
      </header>
      <h1 className="mb-2 text-2xl font-bold text-gray-900">{round.restaurantName}</h1>
      {round.restaurantUrl && (
        <p className="mb-2">
          <a
            href={round.restaurantUrl}
            target="_blank"
            rel="noreferrer"
            className="text-blue-600 hover:text-blue-700"
          >
            Menu
          </a>
        </p>
      )}
      <p className="mb-1 text-gray-600">Organized by {round.createdByUserEmail}</p>
      <p className="mb-1 text-gray-600">Deadline: {new Date(round.deadline).toLocaleString()}</p>
      {isOpen && !deadlinePassed && (
        <p className="mb-2 font-semibold text-red-600">
          Time left: {Math.floor(timeLeft / 60)}h {timeLeft % 60}m
        </p>
      )}
      <p className="mb-4">
        Status: <strong>{round.status}</strong>
      </p>

      {isCreator && isOpen && (
        <button
          onClick={handleClose}
          disabled={closeMutation.isPending}
          className="mb-4 rounded-md bg-blue-600 px-4 py-2 font-medium text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
        >
          {closeMutation.isPending ? 'Closing...' : 'Close order'}
        </button>
      )}

      {canAddItems && (
        <form onSubmit={handleAddItem} className="mb-6 rounded-lg bg-white p-4 shadow-sm">
          <h3 className="mb-4 text-lg font-medium text-gray-900">Add your order</h3>
          <div className="mb-4 flex flex-wrap gap-4">
            <input
              placeholder="Description"
              value={desc}
              onChange={(e) => setDesc(e.target.value)}
              className="min-w-[200px] flex-1 rounded-md border border-gray-300 px-3 py-2 text-gray-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
              required
            />
            <input
              type="number"
              step="0.01"
              placeholder="Price"
              value={price}
              onChange={(e) => setPrice(e.target.value)}
              className="w-24 rounded-md border border-gray-300 px-3 py-2 text-gray-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
            <input
              placeholder="Notes (optional)"
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              className="min-w-[200px] flex-1 rounded-md border border-gray-300 px-3 py-2 text-gray-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>
          <button
            type="submit"
            disabled={addItemMutation.isPending}
            className="rounded-md bg-blue-600 px-4 py-2 font-medium text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {addItemMutation.isPending ? 'Adding...' : 'Add item'}
          </button>
        </form>
      )}

      <h3 className="mb-4 text-lg font-medium text-gray-900">Order summary</h3>
      <ul className="mb-4 space-y-2">
        {round.items.map((item) => (
          <li
            key={item.id}
            className="flex flex-wrap items-center gap-2 rounded-lg bg-white p-3 shadow-sm"
          >
            <span className="font-medium text-gray-900">{item.description}</span>
            <span className="text-gray-600">€{item.price.toFixed(2)}</span>
            <span className="text-sm text-gray-500">{item.userEmail}</span>
            {item.notes && (
              <span className="w-full text-sm text-gray-500">{item.notes}</span>
            )}
            {canAddItems && item.userEmail === user?.email && (
              <button
                onClick={() => handleRemoveItem(item.id)}
                className="ml-auto rounded bg-red-600 px-2 py-1 text-sm text-white hover:bg-red-700"
              >
                Remove
              </button>
            )}
          </li>
        ))}
      </ul>
      <p className="text-xl font-bold text-gray-900">Total: €{total.toFixed(2)}</p>
      <p className="mt-4">
        <Link to={`/rounds/${id}/export`} className="text-blue-600 hover:text-blue-700">
          Export summary
        </Link>
      </p>
    </div>
  );
}
