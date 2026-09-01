import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'
import { HttpError } from '../../lib/http'
import { sessionQueryKey } from '../auth/useSession'
import { getOrganisations } from './api'

export function useOrganisations(userId: string) {
  const queryClient = useQueryClient()
  const organisations = useQuery({
    queryKey: ['organisations', userId],
    queryFn: ({ signal }) => getOrganisations(signal),
    retry: false,
  })

  useEffect(() => {
    if (
      organisations.error instanceof HttpError &&
      organisations.error.status === 401
    ) {
      void queryClient.invalidateQueries({ queryKey: sessionQueryKey })
    }
  }, [organisations.error, queryClient])

  return organisations
}
