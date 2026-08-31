import { useMutation, useQueryClient } from '@tanstack/react-query'
import { signOut } from './api'
import { sessionQueryKey } from './useSession'

export function useSignOut() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: signOut,
    onSuccess: () => {
      queryClient.setQueryData(sessionQueryKey, null)
    },
  })
}
