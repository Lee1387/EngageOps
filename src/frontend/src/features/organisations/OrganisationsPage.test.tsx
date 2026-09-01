import { act, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { createTestQueryClient } from '../../test/createTestQueryClient'
import { TestQueryClientProvider } from '../../test/TestQueryClientProvider'
import { OrganisationsPage } from './OrganisationsPage'

const userId = '01990db2-4a3f-7d35-a2bd-6b69ac9c75bd'

describe('OrganisationsPage', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('shows a loading state while organisations are requested', () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn<typeof fetch>()
        .mockReturnValue(new Promise<Response>(() => undefined)),
    )

    renderPage()

    expect(screen.getByRole('status')).toHaveTextContent(
      'Loading organisations…',
    )
  })

  it('lists organisations returned for the authenticated user', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      Response.json([
        {
          id: '01990db2-4a3f-7d35-a2bd-6b69ac9c75be',
          name: 'Alpha Staffing',
        },
        {
          id: '01990db2-4a3f-7d35-a2bd-6b69ac9c75bf',
          name: 'Zeta Workforce',
        },
      ]),
    )
    vi.stubGlobal('fetch', fetchMock)

    renderPage()

    expect(await screen.findByText('Alpha Staffing')).toBeInTheDocument()
    expect(screen.getByText('Zeta Workforce')).toBeInTheDocument()
    expect(
      screen.getByRole('list', { name: 'Organisations' }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('link', { name: 'View clients for Alpha Staffing' }),
    ).toHaveAttribute(
      'href',
      '/organisations/01990db2-4a3f-7d35-a2bd-6b69ac9c75be/clients',
    )
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/organisations',
      expect.objectContaining({
        credentials: 'same-origin',
        headers: { Accept: 'application/json' },
      }),
    )
  })

  it('shows an empty state when the user has no organisation memberships', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn<typeof fetch>().mockResolvedValue(Response.json([])),
    )

    renderPage()

    expect(
      await screen.findByRole('heading', { name: 'No organisations yet' }),
    ).toBeInTheDocument()
    expect(
      screen.getByText(
        'Your account is not currently linked to an organisation.',
      ),
    ).toBeInTheDocument()
  })

  it('does not reuse organisation data for a different authenticated user', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(
        Response.json([
          {
            id: '01990db2-4a3f-7d35-a2bd-6b69ac9c75be',
            name: 'Alpha Staffing',
          },
        ]),
      )
      .mockReturnValueOnce(new Promise<Response>(() => undefined))
    vi.stubGlobal('fetch', fetchMock)

    const page = renderPage()
    expect(await screen.findByText('Alpha Staffing')).toBeInTheDocument()

    page.rerender(
      <TestQueryClientProvider>
        <MemoryRouter>
          <OrganisationsPage userId="01990db2-4a3f-7d35-a2bd-6b69ac9c75c0" />
        </MemoryRouter>
      </TestQueryClientProvider>,
    )

    expect(screen.queryByText('Alpha Staffing')).not.toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent(
      'Loading organisations…',
    )
  })

  it('allows a failed request to be retried', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(new Response(null, { status: 503 }))
      .mockResolvedValueOnce(Response.json([]))
    vi.stubGlobal('fetch', fetchMock)

    renderPage()

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'We couldn’t load your organisations',
    )

    fireEvent.click(screen.getByRole('button', { name: 'Try again' }))

    expect(
      await screen.findByRole('heading', { name: 'No organisations yet' }),
    ).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })

  it('does not show cached organisations when a refetch fails', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(
        Response.json([
          {
            id: '01990db2-4a3f-7d35-a2bd-6b69ac9c75be',
            name: 'Alpha Staffing',
          },
        ]),
      )
      .mockResolvedValueOnce(new Response(null, { status: 503 }))
    vi.stubGlobal('fetch', fetchMock)
    const queryClient = createTestQueryClient()

    renderPage(queryClient)
    expect(await screen.findByText('Alpha Staffing')).toBeInTheDocument()

    await act(async () => {
      await queryClient.invalidateQueries({
        queryKey: ['organisations', userId],
      })
    })

    expect(await screen.findByRole('alert')).toBeInTheDocument()
    expect(screen.queryByText('Alpha Staffing')).not.toBeInTheDocument()
  })
})

function renderPage(client = createTestQueryClient()) {
  return render(
    <TestQueryClientProvider client={client}>
      <MemoryRouter>
        <OrganisationsPage userId={userId} />
      </MemoryRouter>
    </TestQueryClientProvider>,
  )
}
