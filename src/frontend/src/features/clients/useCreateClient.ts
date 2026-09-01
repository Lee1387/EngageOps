import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useInvalidateSessionOnUnauthorized } from '../auth/useInvalidateSessionOnUnauthorized'
import { createClient } from './api'
import { clientsQueryKey } from './useClients'

export function useCreateClient(userId: string, organisationId: string) {
  const queryClient = useQueryClient()
  const mutation = useMutation({
    mutationFn: (name: string) => createClient(organisationId, name),
    onSuccess: async (result) => {
      if (result.outcome === 'created') {
        await queryClient.invalidateQueries({
          queryKey: clientsQueryKey(userId, organisationId),
        })
      }
    },
  })

  useInvalidateSessionOnUnauthorized(mutation.error)

  return mutation
}
