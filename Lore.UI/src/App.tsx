import {
  QueryClient,
  QueryClientProvider,
} from '@tanstack/react-query'
import { Toaster } from "sonner";
import { AppShell } from "./components/layout/AppShell";

const queryClient = new QueryClient()

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <AppShell />
      <Toaster position="bottom-right" richColors />
    </QueryClientProvider>
  );
}
