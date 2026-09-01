import { fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { TestQueryClientProvider } from './test/TestQueryClientProvider'

describe('App', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('shows the session loading state', () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn<typeof fetch>()
        .mockReturnValue(new Promise<Response>(() => undefined)),
    )

    renderApp()

    expect(screen.getByRole('status')).toHaveTextContent(
      'Checking your session…',
    )
    expect(
      screen.queryByRole('heading', { name: 'Welcome back' }),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('heading', { name: 'Welcome to EngageOps' }),
    ).not.toBeInTheDocument()
  })

  it('shows sign-in for an unauthenticated session', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValue(new Response(null, { status: 401 }))
    vi.stubGlobal('fetch', fetchMock)

    renderApp()

    expect(
      await screen.findByRole('heading', { name: 'Welcome back' }),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Email address')).toBeInTheDocument()
    expect(screen.getByLabelText('Password')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/auth/session',
      expect.objectContaining({ credentials: 'same-origin' }),
    )
  })

  it('shows the authenticated account email', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn<typeof fetch>()
        .mockResolvedValueOnce(authenticatedSessionResponse())
        .mockResolvedValueOnce(organisationsResponse()),
    )

    renderApp()

    expect(
      await screen.findByText(/owner@northstar\.example/),
    ).toBeInTheDocument()
    expect(
      await screen.findByRole('heading', { name: 'Organisations' }),
    ).toBeInTheDocument()
    expect(await screen.findByText('Northstar Workforce')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Organisations' })).toHaveAttribute(
      'aria-current',
      'page',
    )
    expect(screen.getByRole('button', { name: 'Sign out' })).toBeEnabled()
  })

  it('opens the selected organisations client workspace', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(authenticatedSessionResponse())
      .mockResolvedValueOnce(organisationsResponse())
      .mockResolvedValueOnce(organisationsResponse())
      .mockResolvedValueOnce(clientsResponse())
    vi.stubGlobal('fetch', fetchMock)

    renderApp()

    fireEvent.click(
      await screen.findByRole('link', {
        name: 'View clients for Northstar Workforce',
      }),
    )

    expect(
      await screen.findByRole('heading', { name: 'Clients' }),
    ).toBeInTheDocument()
    expect(await screen.findByText('Acme Operations')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Clients' })).toHaveAttribute(
      'aria-current',
      'page',
    )
  })

  it('allows a failed session request to be retried', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(new Response(null, { status: 503 }))
      .mockResolvedValueOnce(authenticatedSessionResponse())
      .mockResolvedValueOnce(organisationsResponse())
    vi.stubGlobal('fetch', fetchMock)

    renderApp()

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'We couldn’t load your session',
    )

    fireEvent.click(screen.getByRole('button', { name: 'Try again' }))

    expect(
      await screen.findByText(/owner@northstar\.example/),
    ).toBeInTheDocument()
    expect(await screen.findByText('Northstar Workforce')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledTimes(3)
  })

  it('returns to sign-in when loading organisations finds an expired session', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(authenticatedSessionResponse())
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
    vi.stubGlobal('fetch', fetchMock)

    renderApp()

    expect(
      await screen.findByRole('heading', { name: 'Welcome back' }),
    ).toBeInTheDocument()
    expect(fetchMock).toHaveBeenNthCalledWith(
      3,
      '/api/auth/session',
      expect.objectContaining({ credentials: 'same-origin' }),
    )
  })

  it('returns to sign-in when loading clients finds an expired session', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(authenticatedSessionResponse())
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
    vi.stubGlobal('fetch', fetchMock)

    renderApp(['/organisations/01990db2-4a3f-7d35-a2bd-6b69ac9c75be/clients'])

    expect(
      await screen.findByRole('heading', { name: 'Welcome back' }),
    ).toBeInTheDocument()
    expect(fetchMock).toHaveBeenNthCalledWith(
      4,
      '/api/auth/session',
      expect.objectContaining({ credentials: 'same-origin' }),
    )
  })

  it('returns to sign-in when the session expires while adding a client', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(authenticatedSessionResponse())
      .mockResolvedValueOnce(organisationsResponse())
      .mockResolvedValueOnce(clientsResponse([]))
      .mockResolvedValueOnce(Response.json({ token: 'antiforgery-token' }))
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
    vi.stubGlobal('fetch', fetchMock)

    renderApp(['/organisations/01990db2-4a3f-7d35-a2bd-6b69ac9c75be/clients'])
    expect(
      await screen.findByRole('heading', { name: 'No clients yet' }),
    ).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Add client' }))
    fireEvent.change(screen.getByLabelText('Client name'), {
      target: { value: 'Acme Operations' },
    })
    fireEvent.submit(screen.getByRole('form', { name: 'Add client' }))

    expect(
      await screen.findByRole('heading', { name: 'Welcome back' }),
    ).toBeInTheDocument()
    expect(fetchMock).toHaveBeenNthCalledWith(
      6,
      '/api/auth/session',
      expect.objectContaining({ credentials: 'same-origin' }),
    )
  })

  it('transitions to the authenticated state after sign-in', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
      .mockResolvedValueOnce(Response.json({ token: 'antiforgery-token' }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(authenticatedSessionResponse())
      .mockResolvedValueOnce(organisationsResponse())
    vi.stubGlobal('fetch', fetchMock)

    renderApp()

    await fillAndSubmitSignInForm()

    expect(
      await screen.findByText(/owner@northstar\.example/),
    ).toBeInTheDocument()
    const signInCall = fetchMock.mock.calls[2]
    expect(signInCall).toBeDefined()
    if (!signInCall) {
      throw new Error('The sign-in request was not sent.')
    }

    const [request, options] = signInCall
    expect(request).toBe('/api/auth/sign-in')
    expect(options?.method).toBe('POST')
    expect(new Headers(options?.headers).get('X-CSRF-TOKEN')).toBe(
      'antiforgery-token',
    )
    expect(options?.body).toBe(
      JSON.stringify({
        email: 'owner@northstar.example',
        password: 'ValidPassword1!',
      }),
    )
  })

  it('signs out and returns to the sign-in page', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(authenticatedSessionResponse())
      .mockResolvedValueOnce(organisationsResponse())
      .mockResolvedValueOnce(Response.json({ token: 'antiforgery-token' }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
    vi.stubGlobal('fetch', fetchMock)

    renderApp()

    await screen.findByText('Northstar Workforce')
    fireEvent.click(screen.getByRole('button', { name: 'Sign out' }))

    expect(
      await screen.findByRole('heading', { name: 'Welcome back' }),
    ).toBeInTheDocument()
    expect(fetchMock).toHaveBeenNthCalledWith(
      4,
      '/api/auth/sign-out',
      expect.objectContaining({
        credentials: 'same-origin',
        method: 'POST',
      }),
    )
    const signOutHeaders = new Headers(fetchMock.mock.calls[3]?.[1]?.headers)
    expect(signOutHeaders.get('X-CSRF-TOKEN')).toBe('antiforgery-token')
  })

  it('returns to sign-in when the session has already expired at sign-out', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(authenticatedSessionResponse())
      .mockResolvedValueOnce(organisationsResponse())
      .mockResolvedValueOnce(Response.json({ token: 'antiforgery-token' }))
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
    vi.stubGlobal('fetch', fetchMock)

    renderApp()

    await screen.findByText('Northstar Workforce')
    fireEvent.click(screen.getByRole('button', { name: 'Sign out' }))

    expect(
      await screen.findByRole('heading', { name: 'Welcome back' }),
    ).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('disables sign-out while the request is pending', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(authenticatedSessionResponse())
      .mockResolvedValueOnce(organisationsResponse())
      .mockResolvedValueOnce(Response.json({ token: 'antiforgery-token' }))
      .mockReturnValueOnce(new Promise<Response>(() => undefined))
    vi.stubGlobal('fetch', fetchMock)

    renderApp()

    await screen.findByText('Northstar Workforce')
    fireEvent.click(screen.getByRole('button', { name: 'Sign out' }))

    expect(
      await screen.findByRole('button', { name: 'Signing out…' }),
    ).toBeDisabled()
  })

  it('keeps the authenticated shell available when sign-out fails', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(authenticatedSessionResponse())
      .mockResolvedValueOnce(organisationsResponse())
      .mockResolvedValueOnce(Response.json({ token: 'antiforgery-token' }))
      .mockResolvedValueOnce(new Response(null, { status: 503 }))
    vi.stubGlobal('fetch', fetchMock)

    renderApp()

    await screen.findByText('Northstar Workforce')
    fireEvent.click(screen.getByRole('button', { name: 'Sign out' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'We couldn’t sign you out. Check your connection and try again.',
    )
    expect(
      screen.getByRole('heading', { name: 'Organisations' }),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Sign out' })).toBeEnabled()
  })
})

function renderApp(initialEntries = ['/']) {
  return render(
    <TestQueryClientProvider>
      <MemoryRouter initialEntries={initialEntries}>
        <App />
      </MemoryRouter>
    </TestQueryClientProvider>,
  )
}

async function fillAndSubmitSignInForm() {
  const form = await screen.findByRole('form', { name: 'Sign in' })
  fireEvent.change(screen.getByLabelText('Email address'), {
    target: { value: 'owner@northstar.example' },
  })
  fireEvent.change(screen.getByLabelText('Password'), {
    target: { value: 'ValidPassword1!' },
  })
  fireEvent.submit(form)
}

function authenticatedSessionResponse() {
  return Response.json({
    userId: '01990db2-4a3f-7d35-a2bd-6b69ac9c75bd',
    email: 'owner@northstar.example',
  })
}

function organisationsResponse() {
  return Response.json([
    {
      id: '01990db2-4a3f-7d35-a2bd-6b69ac9c75be',
      name: 'Northstar Workforce',
    },
  ])
}

function clientsResponse(names = ['Acme Operations']) {
  return Response.json({
    items: names.map((name, index) => ({
      id: `01990db2-4a3f-7d35-a2bd-6b69ac9c7${(600 + index).toString()}`,
      organisationId: '01990db2-4a3f-7d35-a2bd-6b69ac9c75be',
      name,
    })),
    page: 1,
    pageSize: 20,
    totalCount: names.length,
  })
}
