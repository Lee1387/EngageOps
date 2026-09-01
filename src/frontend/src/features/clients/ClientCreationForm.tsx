import { useState, type SyntheticEvent } from 'react'
import { FiAlertCircle } from 'react-icons/fi'
import { HttpError } from '../../lib/http'
import {
  clientNameMaxLength,
  type ClientSummary,
  type CreateClientResult,
} from './api'
import { useCreateClient } from './useCreateClient'

interface ClientCreationFormProps {
  userId: string
  organisationId: string
  organisationName: string | undefined
  onCancel: () => void
  onCreated: (client: ClientSummary) => void
}

export function ClientCreationForm({
  userId,
  organisationId,
  organisationName,
  onCancel,
  onCreated,
}: ClientCreationFormProps) {
  const [clientError, setClientError] = useState<string>()
  const createClient = useCreateClient(userId, organisationId)
  const serverError = getServerNameError(createClient.data)
  const nameError = clientError ?? serverError
  const requestError = getRequestError(createClient.error)

  function handleSubmit(event: SyntheticEvent<HTMLFormElement, SubmitEvent>) {
    event.preventDefault()
    createClient.reset()

    const form = event.currentTarget
    const formData = new FormData(form)
    const name = formData.get('name')
    const nameValue = typeof name === 'string' ? name.trim() : ''

    if (!nameValue) {
      setClientError('Enter a client name')
      const nameInput = form.elements.namedItem('name')
      if (nameInput instanceof HTMLInputElement) {
        nameInput.focus()
      }
      return
    }

    setClientError(undefined)
    createClient.mutate(nameValue, {
      onSuccess: (result) => {
        if (result.outcome === 'created') {
          form.reset()
          onCreated(result.client)
        }
      },
    })
  }

  function clearError() {
    setClientError(undefined)
    createClient.reset()
  }

  return (
    <div className="inline-form-enter rounded-panel border border-line bg-surface p-5 shadow-panel sm:p-6">
      <div>
        <h2 className="text-lg font-semibold text-ink" id="add-client-heading">
          Add client
        </h2>
        <p className="mt-1 text-sm leading-6 text-muted">
          {organisationName
            ? `Add a client managed by ${organisationName}.`
            : 'Add a client managed by this organisation.'}
        </p>
      </div>

      <form
        aria-labelledby="add-client-heading"
        className="mt-5"
        noValidate
        onSubmit={handleSubmit}
      >
        <label className="block text-sm font-semibold text-ink" htmlFor="name">
          Client name
        </label>
        <input
          className="form-control mt-2 px-4"
          id="name"
          name="name"
          type="text"
          autoComplete="organization"
          autoFocus
          disabled={createClient.isPending}
          maxLength={clientNameMaxLength}
          aria-invalid={Boolean(nameError)}
          aria-describedby={nameError ? 'name-error' : undefined}
          onChange={clearError}
        />
        <div className="min-h-6 pt-1" aria-live="polite">
          {nameError && (
            <p
              className="field-error flex items-center gap-1.5 text-sm text-red-700"
              id="name-error"
            >
              <FiAlertCircle aria-hidden="true" className="size-4 shrink-0" />
              {nameError}
            </p>
          )}
        </div>

        {requestError && (
          <p
            className="mt-2 flex items-start gap-2 rounded-control border border-red-200 bg-red-50 px-4 py-3 text-sm leading-5 text-red-800"
            role="alert"
          >
            <FiAlertCircle
              aria-hidden="true"
              className="mt-0.5 size-4 shrink-0"
            />
            {requestError}
          </p>
        )}

        <div className="mt-5 flex flex-col gap-3 sm:flex-row sm:justify-end">
          <button
            className="inline-flex min-h-11 cursor-pointer items-center justify-center rounded-control border border-line bg-surface px-4 text-sm font-semibold text-ink transition-colors duration-200 hover:border-slate-400 hover:bg-slate-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-700 disabled:cursor-not-allowed disabled:text-muted"
            type="button"
            disabled={createClient.isPending}
            onClick={onCancel}
          >
            Cancel
          </button>
          <button
            className="button-primary min-h-11 px-5"
            type="submit"
            disabled={createClient.isPending}
          >
            {createClient.isPending ? (
              <>
                <span
                  aria-hidden="true"
                  className="size-4 animate-spin rounded-full border-2 border-white/40 border-t-white motion-reduce:animate-none"
                />
                Adding client…
              </>
            ) : (
              'Add client'
            )}
          </button>
        </div>
      </form>
    </div>
  )
}

function getServerNameError(
  result: CreateClientResult | undefined,
): string | undefined {
  return result?.outcome === 'invalidInput'
    ? result.errors.name?.join(' ')
    : undefined
}

function getRequestError(error: Error | null): string | null {
  if (!error) {
    return null
  }

  return error instanceof HttpError && error.status === 404
    ? 'We couldn’t add this client because the organisation is no longer available.'
    : 'We couldn’t add this client right now. Check your connection and try again.'
}
