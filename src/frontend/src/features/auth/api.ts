import { isRecord } from '../../lib/json'
import { getAntiforgeryToken } from '../../lib/antiforgery'
import { getValidationProblemFieldErrors } from '../../lib/validationProblem'

export interface Session {
  userId: string
  email: string | null
}

export interface SignInCredentials {
  email: string
  password: string
}

interface SignInValidationErrors {
  email?: string[]
  password?: string[]
}

export type SignInResult =
  | { outcome: 'authenticated' }
  | { outcome: 'invalidCredentials' }
  | { outcome: 'invalidInput'; errors: SignInValidationErrors }

export async function getSession(signal: AbortSignal): Promise<Session | null> {
  const response = await fetch('/api/auth/session', {
    credentials: 'same-origin',
    headers: { Accept: 'application/json' },
    signal,
  })

  if (response.status === 401) {
    return null
  }

  if (!response.ok) {
    throw new Error(
      `Session request failed with status ${response.status.toString()}.`,
    )
  }

  const session: unknown = await response.json()
  if (!isSession(session)) {
    throw new Error('Session response was invalid.')
  }

  return session
}

export async function signIn(
  credentials: SignInCredentials,
): Promise<SignInResult> {
  const antiforgeryToken = await getAntiforgeryToken()
  const response = await fetch('/api/auth/sign-in', {
    method: 'POST',
    credentials: 'same-origin',
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
      'X-CSRF-TOKEN': antiforgeryToken,
    },
    body: JSON.stringify(credentials),
  })

  if (response.status === 204) {
    return { outcome: 'authenticated' }
  }

  if (response.status === 401) {
    return { outcome: 'invalidCredentials' }
  }

  if (response.status === 400) {
    const responseBody: unknown = await response.json()
    const errors = getSignInValidationErrors(responseBody)
    if (errors) {
      return { outcome: 'invalidInput', errors }
    }
  }

  throw new Error(
    `Sign-in request failed with status ${response.status.toString()}.`,
  )
}

export async function signOut(): Promise<void> {
  const antiforgeryToken = await getAntiforgeryToken()
  const response = await fetch('/api/auth/sign-out', {
    method: 'POST',
    credentials: 'same-origin',
    headers: {
      Accept: 'application/json',
      'X-CSRF-TOKEN': antiforgeryToken,
    },
  })

  if (response.status !== 204 && response.status !== 401) {
    throw new Error(
      `Sign-out request failed with status ${response.status.toString()}.`,
    )
  }
}

function getSignInValidationErrors(
  responseBody: unknown,
): SignInValidationErrors | null {
  const errors: SignInValidationErrors = {}
  const emailErrors = getValidationProblemFieldErrors(responseBody, 'email')
  const passwordErrors = getValidationProblemFieldErrors(
    responseBody,
    'password',
  )

  if (emailErrors) {
    errors.email = emailErrors
  }

  if (passwordErrors) {
    errors.password = passwordErrors
  }

  return emailErrors || passwordErrors ? errors : null
}
function isSession(value: unknown): value is Session {
  return (
    isRecord(value) &&
    typeof value.userId === 'string' &&
    (typeof value.email === 'string' || value.email === null)
  )
}
