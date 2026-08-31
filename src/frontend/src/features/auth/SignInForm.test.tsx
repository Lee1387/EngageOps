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

  it('allows the password visibility to be toggled', () => {
    renderForm()
    const password = screen.getByLabelText('Password')

    expect(password).toHaveAttribute('type', 'password')

    fireEvent.click(screen.getByRole('button', { name: 'Show password' }))

    expect(password).toHaveAttribute('type', 'text')
    expect(
      screen.getByRole('button', { name: 'Hide password' }),
    ).toHaveAttribute('aria-pressed', 'true')
  })

  it('identifies password recovery as unavailable', () => {
    renderForm()

    expect(
      screen.getByRole('button', { name: 'Forgot password?' }),
    ).toBeDisabled()
  })

  it('uses accessible custom validation for required fields', () => {
    const fetchMock = vi.fn<typeof fetch>()
    vi.stubGlobal('fetch', fetchMock)
    renderForm()

    const form = screen.getByRole('form', { name: 'Sign in' })
    fireEvent.submit(form)

    expect(form).toHaveAttribute('novalidate')
    expect(screen.getByText('Enter your email address')).toBeInTheDocument()
    expect(screen.getByText('Enter your password')).toBeInTheDocument()
    expect(screen.getByLabelText('Email address')).toHaveAttribute(
      'aria-describedby',
      'email-error',
    )
    expect(screen.getByLabelText('Password')).toHaveAttribute(
      'aria-describedby',
      'password-error',
    )
    expect(screen.getByLabelText('Email address')).toHaveFocus()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('shows a custom error for an invalid email address', () => {
    renderForm()

    fireEvent.change(screen.getByLabelText('Email address'), {
      target: { value: 'not-an-email' },
    })
    fireEvent.change(screen.getByLabelText('Password'), {
      target: { value: 'ValidPassword1!' },
    })
    fireEvent.submit(screen.getByRole('form', { name: 'Sign in' }))

    expect(screen.getByText('Enter a valid email address')).toBeInTheDocument()
    expect(screen.getByLabelText('Email address')).toHaveAttribute(
      'aria-invalid',
      'true',
    )
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
                email: ['Email must be a valid email address.'],
                password: ['Password must not exceed 256 characters.'],
              },
            },
            { status: 400 },
          ),
        ),
    )

    renderForm()
    submitValidCredentials()

    expect(
      await screen.findByText('Email must be a valid email address.'),
    ).toBeInTheDocument()
    expect(
      screen.getByText('Password must not exceed 256 characters.'),
    ).toBeInTheDocument()
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
      "We couldn't sign you in. Check your email and password and try again.",
    )
  })

  it('clears a previous authentication failure when the form changes', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn<typeof fetch>()
        .mockResolvedValueOnce(Response.json({ token: 'antiforgery-token' }))
        .mockResolvedValueOnce(new Response(null, { status: 401 })),
    )

    renderForm()
    submitValidCredentials()

    expect(await screen.findByRole('alert')).toBeInTheDocument()

    fireEvent.change(screen.getByLabelText('Email address'), {
      target: { value: 'updated@northstar.example' },
    })

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
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
      "We couldn't sign you in. Check your email and password and try again.",
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
