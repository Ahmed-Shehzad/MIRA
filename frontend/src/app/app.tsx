import { AppProvider } from './provider';
import { AppRoutes } from './routes';

export function App() {
  return (
    <AppProvider>
      <AppRoutes />
    </AppProvider>
  );
}
