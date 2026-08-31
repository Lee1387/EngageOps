import { useMutation, useQueryClient } from '@tanstack/react-query'
import { signIn } from './api'
import { sessionQueryKey } from './useSession'

export function useSignIn() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: signIn,
    onSuccess: async (result) => {
      if (result.outcome === 'authenticated') {
        await queryClient.invalidateQueries({ queryKey: sessionQueryKey })
      }
    },
  })
}
