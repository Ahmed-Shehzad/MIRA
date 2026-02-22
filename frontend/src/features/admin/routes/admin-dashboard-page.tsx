import { useAdminTenants } from '../hooks/use-admin-tenants';
import { useAdminUsers, useAssignAdmin } from '../hooks/use-admin-users';

const ADMIN_GROUP = 'Admins';

export function AdminDashboardPage() {
  const { data: tenants = [], isLoading: tenantsLoading } = useAdminTenants();
  const { data: users = [], isLoading: usersLoading } = useAdminUsers();
  const assignAdmin = useAssignAdmin();

  return (
    <div className="mx-auto max-w-4xl px-6 py-8">
      <header className="mb-8">
        <h1 className="text-2xl font-bold text-gray-900">Admin Dashboard</h1>
      </header>

      <section className="mb-10">
        <h2 className="mb-4 text-lg font-semibold text-gray-800">Tenants</h2>
        {tenantsLoading ? (
          <p className="text-gray-600">Loading tenants...</p>
        ) : (
          <div className="overflow-hidden rounded-lg border border-gray-200 bg-white shadow">
            <table className="min-w-full divide-y divide-gray-200">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500">Name</th>
                  <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500">Slug</th>
                  <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200 bg-white">
                {tenants.map((t) => (
                  <tr key={t.id}>
                    <td className="px-4 py-3 text-sm text-gray-900">{t.name}</td>
                    <td className="px-4 py-3 text-sm text-gray-600">{t.slug}</td>
                    <td className="px-4 py-3">
                      <span
                        className={`inline-flex rounded-full px-2 py-1 text-xs font-medium ${
                          t.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'
                        }`}
                      >
                        {t.isActive ? 'Active' : 'Inactive'}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section>
        <h2 className="mb-4 text-lg font-semibold text-gray-800">Users</h2>
        {usersLoading ? (
          <p className="text-gray-600">Loading users...</p>
        ) : (
          <div className="overflow-hidden rounded-lg border border-gray-200 bg-white shadow">
            <table className="min-w-full divide-y divide-gray-200">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500">Email</th>
                  <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500">Company</th>
                  <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500">Tenant</th>
                  <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500">Groups</th>
                  <th className="px-4 py-3 text-left text-xs font-medium uppercase text-gray-500">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200 bg-white">
                {users.map((u) => {
                  const isAdmin = u.groups.includes(ADMIN_GROUP);
                  return (
                    <tr key={u.id}>
                      <td className="px-4 py-3 text-sm text-gray-900">{u.email}</td>
                      <td className="px-4 py-3 text-sm text-gray-600">{u.company}</td>
                      <td className="px-4 py-3 text-sm text-gray-600">{u.tenantName}</td>
                      <td className="px-4 py-3 text-sm text-gray-600">{u.groups.join(', ')}</td>
                      <td className="px-4 py-3">
                        {!isAdmin && (
                          <button
                            type="button"
                            onClick={() => assignAdmin.mutate(u.id)}
                            disabled={assignAdmin.isPending}
                            className="rounded bg-blue-600 px-3 py-1 text-xs font-medium text-white hover:bg-blue-700 disabled:opacity-50"
                          >
                            Assign Admin
                          </button>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}
