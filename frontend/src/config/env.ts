export const config = {
  apiUrl: import.meta.env.VITE_API_URL ?? 'http://localhost:5000',
  webSocketUrl: import.meta.env.VITE_WEBSOCKET_URL ?? '',
  useCognito: import.meta.env.VITE_USE_COGNITO !== 'false',
  cognito: {
    userPoolId: import.meta.env.VITE_COGNITO_USER_POOL_ID ?? '',
    clientId: import.meta.env.VITE_COGNITO_CLIENT_ID ?? '',
    domain: import.meta.env.VITE_COGNITO_DOMAIN ?? '',
    region: import.meta.env.VITE_COGNITO_REGION ?? 'us-east-1',
  },
} as const;
