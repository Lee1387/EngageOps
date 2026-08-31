import { useState, type SyntheticEvent } from 'react'
import { FiAlertCircle, FiEye, FiEyeOff, FiLock, FiMail } from 'react-icons/fi'
import { useSignIn } from './useSignIn'

interface ClientValidationErrors {
  email?: string
  password?: string
}

export function SignInForm() {
  const [isPasswordVisible, setIsPasswordVisible] = useState(false)
  const [clientErrors, setClientErrors] = useState<ClientValidationErrors>({})
  const signIn = useSignIn()
  const serverErrors =
    signIn.data?.outcome === 'invalidInput' ? signIn.data.errors : null
  const emailError = clientErrors.email ?? serverErrors?.email?.join(' ')
  const passwordError =
    clientErrors.password ?? serverErrors?.password?.join(' ')
  const authenticationFailed =
    signIn.data?.outcome === 'invalidCredentials' || signIn.isError

  function handleSubmit(event: SyntheticEvent<HTMLFormElement, SubmitEvent>) {
    event.preventDefault()
    signIn.reset()

    const form = event.currentTarget
    const formData = new FormData(form)
    const email = formData.get('email')
    const password = formData.get('password')
    const emailValue = typeof email === 'string' ? email.trim() : ''
    const passwordValue = typeof password === 'string' ? password : ''
    const emailInput = form.elements.namedItem('email')
    const nextErrors: ClientValidationErrors = {}

    if (!emailValue) {
      nextErrors.email = 'Enter your email address'
    } else if (
      emailInput instanceof HTMLInputElement &&
      emailInput.validity.typeMismatch
    ) {
      nextErrors.email = 'Enter a valid email address'
    }

    if (!passwordValue) {
      nextErrors.password = 'Enter your password'
    }

    setClientErrors(nextErrors)
    if (Object.keys(nextErrors).length > 0) {
      const firstInvalidField = nextErrors.email
        ? emailInput
        : form.elements.namedItem('password')

      if (firstInvalidField instanceof HTMLInputElement) {
        firstInvalidField.focus()
      }

      return
    }

    signIn.mutate({
      email: emailValue,
      password: passwordValue,
    })
  }

  function clearFieldError(field: keyof ClientValidationErrors) {
    setClientErrors((current) => ({ ...current, [field]: undefined }))
    signIn.reset()
  }

  return (
    <div>
      <h1 className="text-3xl font-semibold tracking-[-0.03em] text-ink sm:text-4xl">
        Welcome back
      </h1>
      <p className="mt-3 text-base text-muted">
        Sign in to continue to EngageOps
      </p>

      <form
        aria-label="Sign in"
        className="mt-9"
        noValidate
        onSubmit={handleSubmit}
      >
        <div>
          <label
            className="block text-sm font-semibold text-ink"
            htmlFor="email"
          >
            Email address
          </label>
          <div className="relative mt-2">
            <FiMail
              aria-hidden="true"
              className="pointer-events-none absolute top-1/2 left-4 size-5 -translate-y-1/2 text-muted"
            />
            <input
              className="form-control pl-12"
              id="email"
              name="email"
              type="email"
              autoComplete="email"
              autoCapitalize="none"
              spellCheck={false}
              maxLength={256}
              placeholder="you@example.com"
              disabled={signIn.isPending}
              aria-invalid={Boolean(emailError)}
              aria-describedby={emailError ? 'email-error' : undefined}
              onChange={() => {
                clearFieldError('email')
              }}
            />
          </div>
          <div className="min-h-6 pt-1" aria-live="polite">
            {emailError && (
              <p
                className="field-error flex items-center gap-1.5 text-sm text-red-700"
                id="email-error"
              >
                <FiAlertCircle aria-hidden="true" className="size-4 shrink-0" />
                {emailError}
              </p>
            )}
          </div>
        </div>

        <div className="mt-2">
          <div className="flex items-center justify-between gap-4">
            <label
              className="block text-sm font-semibold text-ink"
              htmlFor="password"
            >
              Password
            </label>
            <button
              className="cursor-not-allowed border-0 bg-transparent p-0 text-sm font-medium text-brand-700 opacity-70"
              type="button"
              disabled
              title="Password recovery is not available yet"
            >
              Forgot password?
            </button>
          </div>
          <div className="relative mt-2">
            <FiLock
              aria-hidden="true"
              className="pointer-events-none absolute top-1/2 left-4 size-5 -translate-y-1/2 text-muted"
            />
            <input
              className="form-control pr-12 pl-12"
              id="password"
              name="password"
              type={isPasswordVisible ? 'text' : 'password'}
              autoComplete="current-password"
              maxLength={256}
              placeholder="Enter your password"
              disabled={signIn.isPending}
              aria-invalid={Boolean(passwordError)}
              aria-describedby={passwordError ? 'password-error' : undefined}
              onChange={() => {
                clearFieldError('password')
              }}
            />
            <button
              aria-label={isPasswordVisible ? 'Hide password' : 'Show password'}
              aria-pressed={isPasswordVisible}
              className="absolute top-1/2 right-2 grid size-10 -translate-y-1/2 cursor-pointer place-items-center rounded-lg text-muted transition-colors duration-200 hover:bg-slate-100 hover:text-ink focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-brand-700 disabled:cursor-not-allowed"
              type="button"
              disabled={signIn.isPending}
              onClick={() => {
                setIsPasswordVisible((visible) => !visible)
              }}
            >
              {isPasswordVisible ? (
                <FiEyeOff aria-hidden="true" className="size-5" />
              ) : (
                <FiEye aria-hidden="true" className="size-5" />
              )}
            </button>
          </div>
          <div className="min-h-6 pt-1" aria-live="polite">
            {passwordError && (
              <p
                className="field-error flex items-center gap-1.5 text-sm text-red-700"
                id="password-error"
              >
                <FiAlertCircle aria-hidden="true" className="size-4 shrink-0" />
                {passwordError}
              </p>
            )}
          </div>
        </div>

        <div
          className={`auth-error-region ${authenticationFailed ? 'auth-error-region-visible' : ''}`}
        >
          <div>
            {authenticationFailed && (
              <p
                className="flex items-start gap-2 rounded-control border border-red-200 bg-red-50 px-4 py-3 text-sm leading-5 text-red-800"
                role="alert"
              >
                <FiAlertCircle
                  aria-hidden="true"
                  className="mt-0.5 size-4 shrink-0"
                />
                We couldn't sign you in. Check your email and password and try
                again.
              </p>
            )}
          </div>
        </div>

        <button
          className="button-primary w-full px-4 py-3.5"
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
            'Sign in'
          )}
        </button>
      </form>
    </div>
  )
}
