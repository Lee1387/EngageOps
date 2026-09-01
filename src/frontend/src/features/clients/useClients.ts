import { useQuery } from '@tanstack/react-query'
import { useInvalidateSessionOnUnauthorized } from '../auth/useInvalidateSessionOnUnauthorized'
import { getClients } from './api'

export function clientsQueryKey(userId: string, organisationId: string) {
  return ['clients', userId, organisationId] as const
}

export function useClients(
  userId: string,
  organisationId: string,
  page: number,
) {
  const clients = useQuery({
    queryKey: [...clientsQueryKey(userId, organisationId), page],
    queryFn: ({ signal }) => getClients(organisationId, page, signal),
    retry: false,
  })

  useInvalidateSessionOnUnauthorized(clients.error)

  return clients
}
