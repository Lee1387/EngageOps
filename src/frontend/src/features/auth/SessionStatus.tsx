import type { UseQueryResult } from '@tanstack/react-query'
import { FiAlertCircle } from 'react-icons/fi'
import type { Session } from './api'
import { SignInForm } from './SignInForm'

interface SessionStatusProps {
  session: UseQueryResult<Session | null>
}

export function SessionStatus({ session }: SessionStatusProps) {
  if (session.isError) {
    return (
      <div className="auth-form-enter py-8" role="alert">
        <FiAlertCircle
          aria-hidden="true"
          className="mb-5 size-7 text-red-700"
        />
        <h1 className="text-2xl font-semibold text-ink">
          We couldn’t load your session
        </h1>
        <p className="mt-3 text-sm leading-6 text-muted">
          Check your connection and try again.
        </p>
        <button
          className="button-primary mt-7 px-5 py-3"
          type="button"
          disabled={session.isFetching}
          onClick={() => void session.refetch()}
        >
          {session.isFetching ? 'Trying again…' : 'Try again'}
        </button>
      </div>
    )
  }

  return session.data === null ? <SignInForm /> : null
}
