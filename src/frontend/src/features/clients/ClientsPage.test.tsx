import { act, fireEvent, render, screen, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { createTestQueryClient } from '../../test/createTestQueryClient'
import { TestQueryClientProvider } from '../../test/TestQueryClientProvider'
import { ClientsPage } from './ClientsPage'

const userId = '01990db2-4a3f-7d35-a2bd-6b69ac9c75bd'
const organisationId = '01990db2-4a3f-7d35-a2bd-6b69ac9c75be'

describe('ClientsPage', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('shows a loading state while clients are requested', () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn<typeof fetch>()
        .mockReturnValue(new Promise<Response>(() => undefined)),
    )

    renderPage()

    expect(screen.getByRole('status')).toHaveTextContent('Loading clients…')
  })

  it('lists the selected organisations clients', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(organisationsResponse())
      .mockResolvedValueOnce(
        clientPageResponse(['Alpha Logistics', 'Zeta Care']),
      )
    vi.stubGlobal('fetch', fetchMock)

    renderPage()

    expect(await screen.findByText('Alpha Logistics')).toBeInTheDocument()
    expect(screen.getByText('Zeta Care')).toBeInTheDocument()
    expect(
      screen.getByText('View the clients managed by Northstar Workforce.'),
    ).toBeInTheDocument()
    const breadcrumb = screen.getByRole('navigation', { name: 'Breadcrumb' })
    expect(
      within(breadcrumb).getByRole('link', { name: 'Organisations' }),
    ).toHaveAttribute('href', '/organisations')
    expect(
      within(breadcrumb).getByText('Northstar Workforce'),
    ).toBeInTheDocument()
    expect(within(breadcrumb).getByText('Clients')).toHaveAttribute(
      'aria-current',
      'page',
    )
    expect(
      screen.getByText('Northstar Workforce', { selector: 'p' }),
    ).toBeInTheDocument()
    expect(screen.getByRole('list', { name: 'Clients' })).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledWith(
      `/api/organisations/${organisationId}/clients?page=1&pageSize=20`,
      expect.objectContaining({
        credentials: 'same-origin',
        headers: { Accept: 'application/json' },
      }),
    )
  })

  it('accepts canonical organisation identifiers for an uppercase route', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn<typeof fetch>()
        .mockResolvedValueOnce(organisationsResponse())
        .mockResolvedValueOnce(clientPageResponse(['Alpha Logistics'])),
    )

    renderPage(createTestQueryClient(), organisationId.toUpperCase())

    expect(await screen.findByText('Alpha Logistics')).toBeInTheDocument()
    expect(
      screen.queryByText('We couldn’t load this organisation’s clients'),
    ).not.toBeInTheDocument()
  })

  it('shows an empty state when the organisation has no clients', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn<typeof fetch>()
        .mockResolvedValueOnce(organisationsResponse())
        .mockResolvedValueOnce(clientPageResponse([])),
    )

    renderPage()

    expect(
      await screen.findByRole('heading', { name: 'No clients yet' }),
    ).toBeInTheDocument()
  })

  it('does not expose whether an unavailable organisation exists', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn<typeof fetch>()
        .mockResolvedValueOnce(organisationsResponse())
        .mockResolvedValueOnce(new Response(null, { status: 404 })),
    )

    renderPage()

    expect(
      await screen.findByRole('heading', {
        name: 'Organisation unavailable',
      }),
    ).toBeInTheDocument()
    expect(
      screen.getByText(
        /may no longer exist, or your account may no longer have access/,
      ),
    ).toBeInTheDocument()
  })

  it('allows a failed request to be retried', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(organisationsResponse())
      .mockResolvedValueOnce(new Response(null, { status: 503 }))
      .mockResolvedValueOnce(clientPageResponse([]))
    vi.stubGlobal('fetch', fetchMock)

    renderPage()

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'We couldn’t load this organisation’s clients',
    )
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }))

    expect(
      await screen.findByRole('heading', { name: 'No clients yet' }),
    ).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledTimes(3)
  })

  it('requests the next server page without carrying over previous results', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(organisationsResponse())
      .mockResolvedValueOnce(clientPageResponse(['Alpha Logistics'], 1, 21))
      .mockResolvedValueOnce(clientPageResponse(['Zeta Care'], 2, 21))
    vi.stubGlobal('fetch', fetchMock)

    renderPage()

    expect(await screen.findByText('Alpha Logistics')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))

    expect(await screen.findByText('Zeta Care')).toBeInTheDocument()
    expect(screen.queryByText('Alpha Logistics')).not.toBeInTheDocument()
    expect(screen.getByText('Page 2 of 2')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenNthCalledWith(
      3,
      `/api/organisations/${organisationId}/clients?page=2&pageSize=20`,
      expect.any(Object),
    )
  })

  it('does not show cached clients when access is lost during a refetch', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(organisationsResponse())
      .mockResolvedValueOnce(clientPageResponse(['Alpha Logistics']))
      .mockResolvedValueOnce(new Response(null, { status: 404 }))
    vi.stubGlobal('fetch', fetchMock)
    const queryClient = createTestQueryClient()

    renderPage(queryClient)
    expect(await screen.findByText('Alpha Logistics')).toBeInTheDocument()

    await act(async () => {
      await queryClient.invalidateQueries({
        queryKey: ['clients', userId, organisationId, 1],
      })
    })

    expect(
      await screen.findByRole('heading', {
        name: 'Organisation unavailable',
      }),
    ).toBeInTheDocument()
    expect(screen.queryByText('Alpha Logistics')).not.toBeInTheDocument()
  })
})

function renderPage(
  client = createTestQueryClient(),
  routeOrganisationId = organisationId,
) {
  return render(
    <TestQueryClientProvider client={client}>
      <MemoryRouter
        initialEntries={[`/organisations/${routeOrganisationId}/clients`]}
      >
        <Routes>
          <Route
            path="organisations/:organisationId/clients"
            element={<ClientsPage userId={userId} />}
          />
        </Routes>
      </MemoryRouter>
    </TestQueryClientProvider>,
  )
}

function clientPageResponse(
  names: string[],
  page = 1,
  totalCount = names.length,
) {
  return Response.json({
    items: names.map((name, index) => ({
      id: `01990db2-4a3f-7d35-a2bd-6b69ac9c7${(600 + index).toString()}`,
      organisationId,
      name,
    })),
    page,
    pageSize: 20,
    totalCount,
  })
}

function organisationsResponse() {
  return Response.json([
    {
      id: organisationId,
      name: 'Northstar Workforce',
    },
  ])
}
