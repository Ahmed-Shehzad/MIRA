import { useState, useEffect } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '@/providers/auth-provider';
import { config } from '@/config/env';
import {
  getCognitoLoginUrl,
  parseCognitoRedirectHash,
  clearCognitoHash,
} from '@/lib/cognito';

const COGNITO_ERROR_MESSAGES: Record<string, string> = {
  access_denied: 'Access denied.',
  invalid_scope: 'Invalid scope requested.',
};

interface TestTokenResponse {
  token: string;
}

export function LoginPage() {
  const [error, setError] = useState('');
  const [email, setEmail] = useState('');
  const [company, setCompany] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const { loginWithToken } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const from = (location.state as { from?: { pathname: string } })?.from?.pathname ?? '/';
  const useCognito = config.useCognito;

  useEffect(() => {
    if (!useCognito) return;
    const cognitoTokens = parseCognitoRedirectHash();
    if (cognitoTokens) {
      loginWithToken(cognitoTokens.idToken).then(() => {
        clearCognitoHash();
        navigate(from, { replace: true });
      });
      return;
    }
    const params = new URLSearchParams(location.hash.slice(1));
    const err = params.get('error');
    if (err && COGNITO_ERROR_MESSAGES[err]) {
      setError(COGNITO_ERROR_MESSAGES[err]);
      clearCognitoHash();
    }
  }, [useCognito, location.hash, loginWithToken, navigate, from]);

  async function handleDevLogin(e: React.FormEvent) {
    e.preventDefault();
    setError('');
    setSubmitting(true);
    try {
      const res = await fetch(`${config.apiUrl}/api/v1/auth/test-token`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, company }),
      });
      if (!res.ok) {
        const text = await res.text();
        setError(text || 'Failed to get test token');
        return;
      }
      const data = (await res.json()) as TestTokenResponse;
      await loginWithToken(data.token);
      navigate(from, { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Network error');
    } finally {
      setSubmitting(false);
    }
  }

  function handleCognitoLogin() {
    window.location.href = getCognitoLoginUrl();
  }

  return (
    <div className="mx-auto mt-16 max-w-md rounded-lg bg-white p-8 shadow-md">
      <h1 className="mb-2 text-2xl font-bold text-gray-900">HIVE Food Orders</h1>
      <h2 className="mb-6 text-lg text-gray-600">Log in</h2>
      <div className="flex flex-col gap-4">
        {error && <p className="text-sm text-red-600">{error}</p>}
        {useCognito ? (
          <button
            type="button"
            onClick={handleCognitoLogin}
            className="rounded-md bg-amber-600 px-4 py-2 font-medium text-white hover:bg-amber-700"
          >
            Sign in with Cognito
          </button>
        ) : (
          <form onSubmit={handleDevLogin} className="flex flex-col gap-4">
            <input
              type="email"
              placeholder="Email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              className="rounded-md border border-gray-300 px-3 py-2"
            />
            <input
              type="text"
              placeholder="Company"
              value={company}
              onChange={(e) => setCompany(e.target.value)}
              required
              className="rounded-md border border-gray-300 px-3 py-2"
            />
            <button
              type="submit"
              disabled={submitting}
              className="rounded-md bg-amber-600 px-4 py-2 font-medium text-white hover:bg-amber-700 disabled:opacity-50"
            >
              {submitting ? 'Signing in…' : 'Sign in (dev)'}
            </button>
          </form>
        )}
      </div>
    </div>
  );
}
