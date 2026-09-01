import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useState, type PropsWithChildren } from 'react'
import { createTestQueryClient } from './createTestQueryClient'

interface TestQueryClientProviderProps extends PropsWithChildren {
  client?: QueryClient
}

export function TestQueryClientProvider({
  children,
  client: providedClient,
}: TestQueryClientProviderProps) {
  const [queryClient] = useState(
    () => providedClient ?? createTestQueryClient(),
  )

  return (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  )
}
