import { isRecord } from '../../lib/json'
import { HttpError } from '../../lib/http'

export interface OrganisationSummary {
  id: string
  name: string
}

export async function getOrganisations(
  signal: AbortSignal,
): Promise<OrganisationSummary[]> {
  const response = await fetch('/api/organisations', {
    credentials: 'same-origin',
    headers: { Accept: 'application/json' },
    signal,
  })

  if (!response.ok) {
    throw new HttpError(
      `Organisations request failed with status ${response.status.toString()}.`,
      response.status,
    )
  }

  const organisations: unknown = await response.json()
  if (
    !Array.isArray(organisations) ||
    !organisations.every(isOrganisationSummary)
  ) {
    throw new Error('Organisations response was invalid.')
  }

  return organisations
}

function isOrganisationSummary(value: unknown): value is OrganisationSummary {
  return (
    isRecord(value) &&
    typeof value.id === 'string' &&
    typeof value.name === 'string'
  )
}
