import type { SyntheticEvent } from 'react'
import { useSignIn } from './useSignIn'

export function SignInForm() {
  const signIn = useSignIn()
  const validationErrors =
    signIn.data?.outcome === 'invalidInput' ? signIn.data.errors : null
  const invalidCredentials = signIn.data?.outcome === 'invalidCredentials'

  function handleSubmit(event: SyntheticEvent<HTMLFormElement, SubmitEvent>) {
    event.preventDefault()

    const formData = new FormData(event.currentTarget)
    const email = formData.get('email')
    const password = formData.get('password')

    signIn.mutate({
      email: typeof email === 'string' ? email : '',
      password: typeof password === 'string' ? password : '',
    })
  }

  return (
    <div>
      <div className="mb-6 grid size-11 place-items-center rounded-2xl bg-blue-50 text-blue-700 ring-1 ring-blue-100">
        <svg
          aria-hidden="true"
          className="size-5"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.8"
        >
          <path
            d="M8 10V8a4 4 0 1 1 8 0v2m-9 0h10a2 2 0 0 1 2 2v7H5v-7a2 2 0 0 1 2-2Z"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
        </svg>
      </div>
      <p className="text-sm font-semibold text-blue-700">Welcome back</p>
      <h2 className="mt-2 text-3xl font-semibold tracking-tight text-slate-950">
        Sign in to EngageOps
      </h2>
      <p className="mt-3 text-sm leading-6 text-slate-600">
        Access your organisation’s clients, workers and assignments.
      </p>

      <form
        aria-label="Sign in"
        className="mt-8 space-y-5"
        onSubmit={handleSubmit}
      >
        <div>
          <label
            className="block text-sm font-medium text-slate-800"
            htmlFor="email"
          >
            Email address
          </label>
          <input
            className="mt-2 block w-full rounded-xl border border-slate-300 bg-slate-50/50 px-3.5 py-3 text-slate-950 shadow-sm transition-all outline-none placeholder:text-slate-400 hover:border-slate-400 hover:bg-white focus:border-blue-600 focus:bg-white focus:ring-4 focus:ring-blue-100 disabled:cursor-not-allowed disabled:bg-slate-100"
            id="email"
            name="email"
            type="email"
            autoComplete="email"
            required
            disabled={signIn.isPending}
            aria-invalid={Boolean(validationErrors?.email)}
            aria-describedby={
              validationErrors?.email ? 'email-errors' : undefined
            }
            onChange={() => {
              signIn.reset()
            }}
          />
          {validationErrors?.email && (
            <p className="mt-2 text-sm text-red-700" id="email-errors">
              {validationErrors.email.join(' ')}
            </p>
          )}
        </div>

        <div>
          <label
            className="block text-sm font-medium text-slate-800"
            htmlFor="password"
          >
            Password
          </label>
          <input
            className="mt-2 block w-full rounded-xl border border-slate-300 bg-slate-50/50 px-3.5 py-3 text-slate-950 shadow-sm transition-all outline-none placeholder:text-slate-400 hover:border-slate-400 hover:bg-white focus:border-blue-600 focus:bg-white focus:ring-4 focus:ring-blue-100 disabled:cursor-not-allowed disabled:bg-slate-100"
            id="password"
            name="password"
            type="password"
            autoComplete="current-password"
            required
            disabled={signIn.isPending}
            aria-invalid={Boolean(validationErrors?.password)}
            aria-describedby={
              validationErrors?.password ? 'password-errors' : undefined
            }
            onChange={() => {
              signIn.reset()
            }}
          />
          {validationErrors?.password && (
            <p className="mt-2 text-sm text-red-700" id="password-errors">
              {validationErrors.password.join(' ')}
            </p>
          )}
        </div>

        {invalidCredentials && (
          <p
            className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800"
            role="alert"
          >
            Invalid email or password.
          </p>
        )}

        {signIn.isError && (
          <p
            className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800"
            role="alert"
          >
            We couldn’t sign you in. Please try again.
          </p>
        )}

        <button
          className="group flex w-full cursor-pointer items-center justify-center gap-2 rounded-xl bg-blue-700 px-4 py-3.5 text-sm font-semibold text-white shadow-lg shadow-blue-700/20 transition-all hover:-translate-y-0.5 hover:bg-blue-800 hover:shadow-xl hover:shadow-blue-700/25 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-700 active:translate-y-0 active:shadow-md disabled:cursor-wait disabled:bg-blue-400 disabled:shadow-none disabled:hover:translate-y-0"
          type="submit"
          disabled={signIn.isPending}
        >
          {signIn.isPending ? (
            <>
              <span
                aria-hidden="true"
                className="size-4 animate-spin rounded-full border-2 border-white/40 border-t-white motion-reduce:animate-none"
              />
              Signing in…
            </>
          ) : (
            <>
              Sign in
              <svg
                aria-hidden="true"
                className="size-4 transition-transform group-hover:translate-x-0.5"
                viewBox="0 0 20 20"
                fill="none"
                stroke="currentColor"
                strokeWidth="1.8"
              >
                <path
                  d="M4 10h12m-4-4 4 4-4 4"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                />
              </svg>
            </>
          )}
        </button>
      </form>

      <div className="mt-6 flex items-center justify-center gap-2 border-t border-slate-100 pt-5 text-xs text-slate-500">
        <svg
          aria-hidden="true"
          className="size-3.5 text-slate-400"
          viewBox="0 0 20 20"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.8"
        >
          <path
            d="M5.5 8V6a4.5 4.5 0 0 1 9 0v2m-10 0h11v8h-11V8Z"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
        </svg>
        Secure access to your organisation
      </div>
    </div>
  )
}
