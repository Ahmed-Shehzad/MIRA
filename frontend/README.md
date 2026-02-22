# MIRA Frontend

React SPA for the HIVE Food Ordering Coordination System. Tech: React (TypeScript), Vite, React Router, TailwindCSS, React Query, Axios.

## Setup

```bash
npm install
```

Create `.env`: `VITE_API_URL=http://localhost:5000`, `VITE_USE_COGNITO=false`. For Cognito, add `VITE_COGNITO_USER_POOL_ID`, `VITE_COGNITO_CLIENT_ID`, `VITE_COGNITO_DOMAIN`, `VITE_COGNITO_REGION`.

## Development

```bash
npm run dev
```

Runs at http://localhost:5173.

## Build

```bash
npm run build
```

Output in `dist/`. Deploy to S3 + CloudFront for production.

## Features

Auth (Cognito or dev token), Order Rounds, WSI upload/viewer, protected routes, error boundaries.

See [docs/production-config.md](../docs/production-config.md) and [README.md](../README.md).
