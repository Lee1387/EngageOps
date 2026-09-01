import { useQuery } from '@tanstack/react-query'
import { useInvalidateSessionOnUnauthorized } from '../auth/useInvalidateSessionOnUnauthorized'
import { getOrganisations } from './api'

export function useOrganisations(userId: string) {
  const organisations = useQuery({
    queryKey: ['organisations', userId],
    queryFn: ({ signal }) => getOrganisations(signal),
    retry: false,
  })

  useInvalidateSessionOnUnauthorized(organisations.error)

  return organisations
}
