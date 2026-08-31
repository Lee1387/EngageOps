import { fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { TestQueryClientProvider } from '../../test/TestQueryClientProvider'
import { SignInForm } from './SignInForm'

describe('SignInForm', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('shows a submitting state while authentication is pending', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(Response.json({ token: 'antiforgery-token' }))
      .mockReturnValueOnce(new Promise<Response>(() => undefined))
    vi.stubGlobal('fetch', fetchMock)

    renderForm()
    submitValidCredentials()

    expect(
      await screen.findByRole('button', { name: 'Signing in…' }),
    ).toBeDisabled()
  })

  it('associates server validation errors with their fields', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn<typeof fetch>()
        .mockResolvedValueOnce(Response.json({ token: 'antiforgery-token' }))
        .mockResolvedValueOnce(
          Response.json(
            {
              status: 400,
              title: 'One or more validation errors occurred.',
              errors: {
                email: ['Email is required.'],
                password: ['Password is required.'],
              },
            },
            { status: 400 },
          ),
        ),
    )

    renderForm()
    fireEvent.submit(screen.getByRole('form', { name: 'Sign in' }))

    expect(await screen.findByText('Email is required.')).toBeInTheDocument()
    expect(screen.getByText('Password is required.')).toBeInTheDocument()
    expect(screen.getByLabelText('Email address')).toHaveAttribute(
      'aria-invalid',
      'true',
    )
    expect(screen.getByLabelText('Password')).toHaveAttribute(
      'aria-invalid',
      'true',
    )
  })

  it('shows the safe invalid-credentials message', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn<typeof fetch>()
        .mockResolvedValueOnce(Response.json({ token: 'antiforgery-token' }))
        .mockResolvedValueOnce(new Response(null, { status: 401 })),
    )

    renderForm()
    submitValidCredentials()

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Invalid email or password.',
    )
  })

  it('shows a retryable message for unexpected failures', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn<typeof fetch>()
        .mockResolvedValueOnce(Response.json({ token: 'antiforgery-token' }))
        .mockResolvedValueOnce(new Response(null, { status: 503 })),
    )

    renderForm()
    submitValidCredentials()

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'We couldn’t sign you in. Please try again.',
    )
  })
})

function renderForm() {
  return render(
    <TestQueryClientProvider>
      <SignInForm />
    </TestQueryClientProvider>,
  )
}

function submitValidCredentials() {
  fireEvent.change(screen.getByLabelText('Email address'), {
    target: { value: 'owner@northstar.example' },
  })
  fireEvent.change(screen.getByLabelText('Password'), {
    target: { value: 'ValidPassword1!' },
  })
  fireEvent.submit(screen.getByRole('form', { name: 'Sign in' }))
}
