import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'
import { HttpError } from '../../lib/http'
import { sessionQueryKey } from './useSession'

export function useInvalidateSessionOnUnauthorized(error: Error | null) {
  const queryClient = useQueryClient()

  useEffect(() => {
    if (error instanceof HttpError && error.status === 401) {
      void queryClient.invalidateQueries(
        { queryKey: sessionQueryKey },
        { cancelRefetch: false },
      )
    }
  }, [error, queryClient])
}
