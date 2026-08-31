import { FiAlertCircle, FiLogOut } from 'react-icons/fi'
import type { Session } from '../features/auth/api'
import { useSignOut } from '../features/auth/useSignOut'
import { Wordmark } from './Wordmark'

interface AppShellProps {
  session: Session
}

export function AppShell({ session }: AppShellProps) {
  const signOut = useSignOut()

  return (
    <div className="min-h-screen bg-canvas">
      <header className="border-b border-line bg-surface">
        <div className="mx-auto flex min-h-18 max-w-7xl items-center justify-between gap-4 px-5 sm:px-8">
          <Wordmark />

          <div className="flex min-w-0 items-center gap-4">
            <div className="hidden min-w-0 text-right sm:block">
              <p className="text-xs font-medium text-muted">Signed in as</p>
              <p className="max-w-72 truncate text-sm font-semibold text-ink">
                {session.email ?? 'Authenticated account'}
              </p>
            </div>
            <button
              className="inline-flex min-h-11 cursor-pointer items-center justify-center gap-2 rounded-control border border-line bg-surface px-3.5 text-sm font-semibold text-ink transition-colors duration-200 hover:border-slate-400 hover:bg-slate-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-700 disabled:cursor-wait disabled:text-muted"
              type="button"
              disabled={signOut.isPending}
              onClick={() => {
                signOut.mutate()
              }}
            >
              {signOut.isPending ? (
                <>
                  <span
                    aria-hidden="true"
                    className="size-4 animate-spin rounded-full border-2 border-slate-300 border-t-brand-700 motion-reduce:animate-none"
                  />
                  Signing out…
                </>
              ) : (
                <>
                  <FiLogOut aria-hidden="true" className="size-4" />
                  Sign out
                </>
              )}
            </button>
          </div>
        </div>
      </header>

      <main className="mx-auto w-full max-w-7xl px-5 py-10 sm:px-8 sm:py-14">
        {signOut.isError && (
          <div
            className="mb-6 flex max-w-2xl items-start gap-2 rounded-control border border-red-200 bg-red-50 px-4 py-3 text-sm leading-5 text-red-800"
            role="alert"
          >
            <FiAlertCircle
              aria-hidden="true"
              className="mt-0.5 size-4 shrink-0"
            />
            We couldn’t sign you out. Check your connection and try again.
          </div>
        )}

        <section
          aria-labelledby="workspace-heading"
          className="rounded-panel border border-line bg-surface p-6 shadow-panel sm:p-8"
        >
          <p className="text-xs font-semibold tracking-[0.12em] text-brand-700 uppercase">
            Workspace
          </p>
          <h1
            className="mt-3 text-3xl font-semibold tracking-[-0.03em] text-ink sm:text-4xl"
            id="workspace-heading"
          >
            Welcome to EngageOps
          </h1>
          <p className="mt-4 max-w-2xl text-base leading-7 text-muted">
            Manage your organisation’s workforce operations from one secure
            workspace.
          </p>
        </section>
      </main>
    </div>
  )
}
