import { useQuery } from '@tanstack/react-query'
import { useInvalidateSessionOnUnauthorized } from '../auth/useInvalidateSessionOnUnauthorized'
import { getClients } from './api'

export function useClients(
  userId: string,
  organisationId: string,
  page: number,
) {
  const clients = useQuery({
    queryKey: ['clients', userId, organisationId, page],
    queryFn: ({ signal }) => getClients(organisationId, page, signal),
    retry: false,
  })

  useInvalidateSessionOnUnauthorized(clients.error)

  return clients
}
