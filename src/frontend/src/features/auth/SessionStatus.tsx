import { useSession } from './useSession'
import { SignInForm } from './SignInForm'

export function SessionStatus() {
  const session = useSession()

  if (session.isPending) {
    return (
      <div
        className="flex flex-col items-center py-12 text-center"
        role="status"
      >
        <span
          aria-hidden="true"
          className="mb-4 size-8 animate-spin rounded-full border-3 border-blue-100 border-t-blue-600 motion-reduce:animate-none"
        />
        <p className="text-sm text-slate-600">Checking your session…</p>
      </div>
    )
  }

  if (session.isError) {
    return (
      <div className="py-8" role="alert">
        <h2 className="text-xl font-semibold text-slate-950">
          We couldn’t load your session
        </h2>
        <p className="mt-2 text-sm leading-6 text-slate-600">
          Check your connection and try again.
        </p>
        <button
          className="mt-6 cursor-pointer rounded-lg bg-blue-700 px-4 py-2.5 text-sm font-semibold text-white shadow-sm transition-all hover:-translate-y-0.5 hover:bg-blue-800 hover:shadow-md focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-700 active:translate-y-0 disabled:cursor-wait disabled:bg-blue-400 disabled:hover:translate-y-0"
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
    <div className="py-8">
      <div className="mb-5 grid size-11 place-items-center rounded-2xl bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200">
        <svg
          aria-hidden="true"
          className="size-5"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
        >
          <path d="m5 12 4 4L19 6" strokeLinecap="round" />
        </svg>
      </div>
      <p className="text-sm font-semibold text-emerald-700">Signed in</p>
      <h2 className="mt-2 text-2xl font-semibold tracking-tight text-slate-950">
        Welcome to EngageOps
      </h2>
      <p className="mt-3 text-sm leading-6 text-slate-600">
        {session.data.email
          ? `You are signed in as ${session.data.email}.`
          : 'Your account is authenticated.'}
      </p>
    </div>
  )
}
