import { fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { TestQueryClientProvider } from '../../test/TestQueryClientProvider'
import { ClientCreationForm } from './ClientCreationForm'

const userId = '01990db2-4a3f-7d35-a2bd-6b69ac9c75bd'
const organisationId = '01990db2-4a3f-7d35-a2bd-6b69ac9c75be'

describe('ClientCreationForm', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('uses accessible custom validation for a required client name', () => {
    const fetchMock = vi.fn<typeof fetch>()
    vi.stubGlobal('fetch', fetchMock)

    renderForm()
    const form = screen.getByRole('form', { name: 'Add client' })
    fireEvent.submit(form)

    expect(form).toHaveAttribute('novalidate')
    expect(screen.getByText('Enter a client name')).toBeInTheDocument()
    expect(screen.getByLabelText('Client name')).toHaveAttribute(
      'aria-invalid',
      'true',
    )
    expect(screen.getByLabelText('Client name')).toHaveAttribute(
      'aria-describedby',
      'name-error',
    )
    expect(screen.getByLabelText('Client name')).toHaveFocus()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('associates server validation errors with the client name', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn<typeof fetch>()
        .mockResolvedValueOnce(Response.json({ token: 'antiforgery-token' }))
        .mockResolvedValueOnce(
          Response.json(
            {
              errors: {
                name: ['Client name must not contain control characters.'],
              },
            },
            { status: 400 },
          ),
        ),
    )

    renderForm()
    enterClientName('Northstar Logistics')
    submitForm()

    expect(
      await screen.findByText(
        'Client name must not contain control characters.',
      ),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Client name')).toHaveAttribute(
      'aria-invalid',
      'true',
    )

    fireEvent.change(screen.getByLabelText('Client name'), {
      target: { value: 'Updated Logistics' },
    })
    expect(
      screen.queryByText('Client name must not contain control characters.'),
    ).not.toBeInTheDocument()
  })

  it.each([
    [
      404,
      'We couldn’t add this client because the organisation is no longer available.',
    ],
    [
      503,
      'We couldn’t add this client right now. Check your connection and try again.',
    ],
  ])('shows a safe failure message for status %s', async (status, message) => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn<typeof fetch>()
        .mockResolvedValueOnce(Response.json({ token: 'antiforgery-token' }))
        .mockResolvedValueOnce(new Response(null, { status })),
    )

    renderForm()
    enterClientName('Northstar Logistics')
    submitForm()

    expect(await screen.findByRole('alert')).toHaveTextContent(message)
  })
})

function renderForm() {
  return render(
    <TestQueryClientProvider>
      <ClientCreationForm
        organisationId={organisationId}
        organisationName="Northstar Workforce"
        userId={userId}
        onCancel={vi.fn()}
        onCreated={vi.fn()}
      />
    </TestQueryClientProvider>,
  )
}

function enterClientName(name: string) {
  fireEvent.change(screen.getByLabelText('Client name'), {
    target: { value: name },
  })
}

function submitForm() {
  fireEvent.submit(screen.getByRole('form', { name: 'Add client' }))
}
