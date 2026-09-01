import { getAntiforgeryToken } from '../../lib/antiforgery'
import { HttpError } from '../../lib/http'
import { isRecord } from '../../lib/json'
import { getValidationProblemFieldErrors } from '../../lib/validationProblem'

export const clientsPageSize = 20
export const clientNameMaxLength = 200

export interface ClientSummary {
  id: string
  organisationId: string
  name: string
}

export interface ClientPage {
  items: ClientSummary[]
  page: number
  pageSize: number
  totalCount: number
}

interface CreateClientValidationErrors {
  name?: string[]
}

export type CreateClientResult =
  | { outcome: 'created'; client: ClientSummary }
  | { outcome: 'invalidInput'; errors: CreateClientValidationErrors }

export async function getClients(
  organisationId: string,
  page: number,
  signal: AbortSignal,
): Promise<ClientPage> {
  const query = new URLSearchParams({
    page: page.toString(),
    pageSize: clientsPageSize.toString(),
  })
  const response = await fetch(
    `/api/organisations/${encodeURIComponent(organisationId)}/clients?${query.toString()}`,
    {
      credentials: 'same-origin',
      headers: { Accept: 'application/json' },
      signal,
    },
  )

  if (!response.ok) {
    throw new HttpError(
      `Clients request failed with status ${response.status.toString()}.`,
      response.status,
    )
  }

  const clientPage: unknown = await response.json()
  if (!isClientPage(clientPage, organisationId, page)) {
    throw new Error('Clients response was invalid.')
  }

  return clientPage
}

export async function createClient(
  organisationId: string,
  name: string,
): Promise<CreateClientResult> {
  const antiforgeryToken = await getAntiforgeryToken()
  const response = await fetch(
    `/api/organisations/${encodeURIComponent(organisationId)}/clients`,
    {
      method: 'POST',
      credentials: 'same-origin',
      headers: {
        Accept: 'application/json',
        'Content-Type': 'application/json',
        'X-CSRF-TOKEN': antiforgeryToken,
      },
      body: JSON.stringify({ name }),
    },
  )

  if (response.status === 201) {
    const client: unknown = await response.json()
    if (!isClientSummary(client, organisationId)) {
      throw new Error('Create client response was invalid.')
    }

    return { outcome: 'created', client }
  }

  if (response.status === 400) {
    const responseBody: unknown = await response.json()
    const nameErrors = getValidationProblemFieldErrors(responseBody, 'name')
    if (nameErrors) {
      return { outcome: 'invalidInput', errors: { name: nameErrors } }
    }
  }

  throw new HttpError(
    `Create client request failed with status ${response.status.toString()}.`,
    response.status,
  )
}

function isClientPage(
  value: unknown,
  organisationId: string,
  requestedPage: number,
): value is ClientPage {
  return (
    isRecord(value) &&
    Array.isArray(value.items) &&
    value.items.every((item) => isClientSummary(item, organisationId)) &&
    value.page === requestedPage &&
    value.pageSize === clientsPageSize &&
    isNonNegativeInteger(value.totalCount)
  )
}

function isClientSummary(
  value: unknown,
  organisationId: string,
): value is ClientSummary {
  return (
    isRecord(value) &&
    typeof value.id === 'string' &&
    typeof value.organisationId === 'string' &&
    value.organisationId.toLowerCase() === organisationId.toLowerCase() &&
    typeof value.name === 'string'
  )
}

function isNonNegativeInteger(value: unknown): value is number {
  return typeof value === 'number' && Number.isInteger(value) && value >= 0
}
