import { config } from '@/config/env';

const COGNITO_SCOPES = 'openid email profile';

/**
 * Builds the Cognito Hosted UI authorization URL.
 * Redirect the user here to sign in with Cognito.
 */
export function getCognitoLoginUrl(): string {
  const { domain, clientId } = config.cognito;
  if (!domain || !clientId) {
    throw new Error('Cognito domain and client ID must be configured when VITE_USE_COGNITO is true');
  }
  const redirectUri = `${globalThis.location.origin}/login`;
  const params = new URLSearchParams({
    client_id: clientId,
    response_type: 'token',
    scope: COGNITO_SCOPES,
    redirect_uri: redirectUri,
  });
  const protocol = globalThis.location.protocol === 'https:' ? 'https' : 'http';
  return `${protocol}://${domain}/oauth2/authorize?${params.toString()}`;
}

/**
 * Parses the Cognito redirect hash for id_token and access_token.
 * Call this on the login page when returning from Cognito Hosted UI.
 */
export function parseCognitoRedirectHash(): { idToken: string; accessToken: string } | null {
  const hash = globalThis.location.hash.slice(1);
  if (!hash) return null;
  const params = new URLSearchParams(hash);
  const idToken = params.get('id_token');
  const accessToken = params.get('access_token');
  if (idToken && accessToken) {
    return { idToken, accessToken };
  }
  return null;
}

/**
 * Clears the Cognito tokens from the URL hash without reloading.
 */
export function clearCognitoHash(): void {
  if (globalThis.location.hash) {
    globalThis.history.replaceState(null, '', globalThis.location.pathname + globalThis.location.search);
  }
}
