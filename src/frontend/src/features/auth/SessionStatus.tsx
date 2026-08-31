import { FiAlertCircle, FiCheck } from 'react-icons/fi'
import { useSession } from './useSession'
import { SignInForm } from './SignInForm'

export function SessionStatus() {
  const session = useSession()

  if (session.isPending) {
    return (
      <div
        className="auth-form-enter flex flex-col items-center py-12 text-center"
        role="status"
      >
        <span
          aria-hidden="true"
          className="mb-4 size-8 animate-spin rounded-full border-3 border-blue-100 border-t-brand-700 motion-reduce:animate-none"
        />
        <p className="text-sm text-muted">Checking your session…</p>
      </div>
    )
  }

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

  if (session.data === null) {
    return <SignInForm />
  }

  return (
    <div className="auth-form-enter py-8">
      <div className="mb-5 grid size-11 place-items-center rounded-control bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200">
        <FiCheck aria-hidden="true" className="size-5" strokeWidth={2.2} />
      </div>
      <p className="text-sm font-semibold text-emerald-700">Signed in</p>
      <h1 className="mt-2 text-3xl font-semibold tracking-tight text-ink">
        Welcome to EngageOps
      </h1>
      <p className="mt-3 text-sm leading-6 text-muted">
        {session.data.email
          ? `You are signed in as ${session.data.email}.`
          : 'Your account is authenticated.'}
      </p>
    </div>
  )
}
