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

async function getAntiforgeryToken(): Promise<string> {
  const response = await fetch('/api/auth/csrf', {
    credentials: 'same-origin',
    headers: { Accept: 'application/json' },
  })

  if (!response.ok) {
    throw new Error(
      `Antiforgery request failed with status ${response.status.toString()}.`,
    )
  }

  const responseBody: unknown = await response.json()
  if (!isRecord(responseBody) || typeof responseBody.token !== 'string') {
    throw new Error('Antiforgery response was invalid.')
  }

  return responseBody.token
}

function getSignInValidationErrors(
  responseBody: unknown,
): SignInValidationErrors | null {
  if (!isRecord(responseBody) || !isRecord(responseBody.errors)) {
    return null
  }

  const errors: SignInValidationErrors = {}
  const emailErrors = getStringArray(responseBody.errors, 'email')
  const passwordErrors = getStringArray(responseBody.errors, 'password')

  if (emailErrors) {
    errors.email = emailErrors
  }

  if (passwordErrors) {
    errors.password = passwordErrors
  }

  return emailErrors || passwordErrors ? errors : null
}

function getStringArray(
  value: Record<string, unknown>,
  propertyName: string,
): string[] | null {
  const propertyValue = value[propertyName]

  return Array.isArray(propertyValue) &&
    propertyValue.length > 0 &&
    propertyValue.every((item) => typeof item === 'string')
    ? propertyValue
    : null
}

function isSession(value: unknown): value is Session {
  return (
    isRecord(value) &&
    typeof value.userId === 'string' &&
    (typeof value.email === 'string' || value.email === null)
  )
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null
}
