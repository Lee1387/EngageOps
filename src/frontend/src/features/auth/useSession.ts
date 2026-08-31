import { useQuery } from '@tanstack/react-query'
import { getSession } from './api'

export const sessionQueryKey = ['auth', 'session'] as const

export function useSession() {
  return useQuery({
    queryKey: sessionQueryKey,
    queryFn: ({ signal }) => getSession(signal),
    retry: false,
  })
}
