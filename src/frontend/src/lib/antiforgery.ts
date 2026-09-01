import { isRecord } from './json'

export async function getAntiforgeryToken(): Promise<string> {
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
