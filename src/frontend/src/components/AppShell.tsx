import { FiAlertCircle, FiBriefcase, FiLogOut } from 'react-icons/fi'
import { NavLink, Outlet } from 'react-router'
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
      <header className="h-18 border-b border-line bg-surface">
        <div className="mx-auto flex h-full max-w-7xl items-center justify-between gap-4 px-5 sm:px-8">
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

      <div className="mx-auto flex min-h-[calc(100vh-4.5rem)] w-full max-w-7xl flex-col lg:grid lg:grid-cols-[15rem_minmax(0,1fr)]">
        <aside className="border-b border-line bg-surface lg:border-r lg:border-b-0">
          <nav aria-label="Primary" className="px-5 py-3 sm:px-8 lg:p-5">
            <NavLink
              className={({ isActive }) =>
                `flex min-h-11 items-center gap-3 rounded-control px-3.5 text-sm font-semibold transition-colors duration-200 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-700 ${
                  isActive
                    ? 'bg-blue-50 text-brand-700'
                    : 'text-muted hover:bg-slate-100 hover:text-ink'
                }`
              }
              to="/organisations"
            >
              <FiBriefcase aria-hidden="true" className="size-4" />
              Organisations
            </NavLink>
          </nav>
        </aside>

        <main className="min-w-0 flex-1 px-5 py-8 sm:px-8 sm:py-10 lg:px-10 lg:py-12">
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

          <Outlet />
        </main>
      </div>
    </div>
  )
}
