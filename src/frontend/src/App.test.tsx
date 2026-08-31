import { fireEvent, render, screen } from '@testing-library/react'
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

    expect(
      screen.getByRole('heading', {
        name: 'Contractor operations, kept connected.',
      }),
    ).toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent(
      'Checking your session…',
    )
  })

  it('shows sign-in for an unauthenticated session', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValue(new Response(null, { status: 401 }))
    vi.stubGlobal('fetch', fetchMock)

    renderApp()

    expect(
      await screen.findByRole('heading', { name: 'Sign in to EngageOps' }),
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
      vi.fn<typeof fetch>().mockResolvedValue(authenticatedSessionResponse()),
    )

    renderApp()

    expect(
      await screen.findByText(/owner@northstar\.example/),
    ).toBeInTheDocument()
  })

  it('allows a failed session request to be retried', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(new Response(null, { status: 503 }))
      .mockResolvedValueOnce(authenticatedSessionResponse())
    vi.stubGlobal('fetch', fetchMock)

    renderApp()

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'We couldn’t load your session',
    )

    fireEvent.click(screen.getByRole('button', { name: 'Try again' }))

    expect(
      await screen.findByText(/owner@northstar\.example/),
    ).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })

  it('transitions to the authenticated state after sign-in', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
      .mockResolvedValueOnce(Response.json({ token: 'antiforgery-token' }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(authenticatedSessionResponse())
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
})

function renderApp() {
  return render(
    <TestQueryClientProvider>
      <App />
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
