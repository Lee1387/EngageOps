import { useEffect, useRef, useState } from 'react'
import {
  FiAlertCircle,
  FiCheckCircle,
  FiChevronLeft,
  FiChevronRight,
  FiPlus,
  FiRefreshCw,
  FiUsers,
} from 'react-icons/fi'
import { Link, Navigate, useParams } from 'react-router'
import { HttpError } from '../../lib/http'
import { OrganisationBreadcrumb } from '../organisations/OrganisationBreadcrumb'
import { useOrganisations } from '../organisations/useOrganisations'
import { clientsPageSize, type ClientPage, type ClientSummary } from './api'
import { ClientCreationForm } from './ClientCreationForm'
import { useClients } from './useClients'

interface ClientsPageProps {
  userId: string
}

export function ClientsPage({ userId }: ClientsPageProps) {
  const { organisationId } = useParams<{ organisationId: string }>()

  return organisationId ? (
    <ClientsContent
      key={organisationId}
      organisationId={organisationId}
      userId={userId}
    />
  ) : (
    <Navigate replace to="/organisations" />
  )
}

interface ClientsContentProps {
  userId: string
  organisationId: string
}

function ClientsContent({ userId, organisationId }: ClientsContentProps) {
  const [page, setPage] = useState(1)
  const [isCreating, setIsCreating] = useState(false)
  const [successMessage, setSuccessMessage] = useState<string>()
  const addClientButton = useRef<HTMLButtonElement>(null)
  const shouldRestoreAddClientFocus = useRef(false)
  const organisations = useOrganisations(userId)
  const clients = useClients(userId, organisationId, page)
  const organisation = organisations.isSuccess
    ? organisations.data.find(
        (candidate) =>
          candidate.id.toLowerCase() === organisationId.toLowerCase(),
      )
    : undefined
  const organisationUnavailable =
    clients.error instanceof HttpError && clients.error.status === 404

  useEffect(() => {
    if (!isCreating && shouldRestoreAddClientFocus.current) {
      addClientButton.current?.focus()
      shouldRestoreAddClientFocus.current = false
    }
  }, [isCreating])

  function closeCreationForm() {
    shouldRestoreAddClientFocus.current = true
    setIsCreating(false)
  }

  function handleClientCreated(client: ClientSummary) {
    setSuccessMessage(`${client.name} was added.`)
    closeCreationForm()
  }

  return (
    <section aria-labelledby="clients-heading">
      <OrganisationBreadcrumb
        currentPage="Clients"
        organisationName={organisation?.name}
      />

      <div className="mt-5 flex flex-col gap-6 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <p className="text-xs font-semibold tracking-[0.12em] text-brand-700 uppercase">
            {organisation?.name ?? 'Organisation'}
          </p>
          <h1
            className="mt-3 text-3xl font-semibold tracking-[-0.03em] text-ink sm:text-4xl"
            id="clients-heading"
          >
            Clients
          </h1>
          <p className="mt-4 max-w-2xl text-base leading-7 text-muted">
            {organisation
              ? `View the clients managed by ${organisation.name}.`
              : 'View the clients managed by this organisation.'}
          </p>
        </div>

        {clients.isSuccess && !isCreating && (
          <button
            className="button-primary min-h-11 w-full shrink-0 px-5 sm:w-auto"
            type="button"
            ref={addClientButton}
            onClick={() => {
              setSuccessMessage(undefined)
              setIsCreating(true)
            }}
          >
            <FiPlus aria-hidden="true" className="size-4" />
            Add client
          </button>
        )}
      </div>

      {isCreating && clients.isSuccess && (
        <div className="mt-8">
          <ClientCreationForm
            organisationId={organisationId}
            organisationName={organisation?.name}
            userId={userId}
            onCancel={closeCreationForm}
            onCreated={handleClientCreated}
          />
        </div>
      )}

      {successMessage && (
        <p
          className="mt-8 flex items-center gap-2 rounded-control border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-800"
          role="status"
        >
          <FiCheckCircle aria-hidden="true" className="size-4 shrink-0" />
          {successMessage}
        </p>
      )}

      <div className="mt-8 sm:mt-10">
        {clients.isPending && <ClientsLoading />}

        {clients.isError && organisationUnavailable && (
          <OrganisationUnavailable />
        )}

        {clients.isError && !organisationUnavailable && (
          <ClientsError
            isRetrying={clients.isFetching}
            onRetry={() => void clients.refetch()}
          />
        )}

        {clients.isSuccess && clients.data.totalCount === 0 && (
          <ClientsEmpty organisationName={organisation?.name} />
        )}

        {clients.isSuccess && clients.data.totalCount > 0 && (
          <ClientList
            clientPage={clients.data}
            onNextPage={() => {
              setPage((current) => current + 1)
            }}
            onPreviousPage={() => {
              setPage((current) => current - 1)
            }}
          />
        )}
      </div>
    </section>
  )
}

interface ClientListProps {
  clientPage: ClientPage
  onNextPage: () => void
  onPreviousPage: () => void
}

function ClientList({
  clientPage,
  onNextPage,
  onPreviousPage,
}: ClientListProps) {
  const totalPages = Math.ceil(clientPage.totalCount / clientsPageSize)

  return (
    <div className="overflow-hidden rounded-panel border border-line bg-surface shadow-panel">
      <div className="flex items-center justify-between gap-4 border-b border-line px-5 py-4 sm:px-6">
        <h2 className="text-sm font-semibold text-ink">Organisation clients</h2>
        <span className="rounded-full bg-blue-50 px-2.5 py-1 text-xs font-semibold text-brand-700">
          {clientPage.totalCount}
        </span>
      </div>

      <ul aria-label="Clients" className="divide-y divide-line">
        {clientPage.items.map((client) => (
          <li
            className="flex items-center gap-4 px-5 py-5 sm:px-6"
            key={client.id}
          >
            <span className="grid size-11 shrink-0 place-items-center rounded-control bg-blue-50 text-brand-700">
              <FiUsers aria-hidden="true" className="size-5" />
            </span>
            <div className="min-w-0">
              <p className="truncate text-base font-semibold text-ink">
                {client.name}
              </p>
              <p className="mt-1 text-sm text-muted">Client</p>
            </div>
          </li>
        ))}
      </ul>

      {totalPages > 1 && (
        <nav
          aria-label="Client pages"
          className="grid grid-cols-2 items-center gap-3 border-t border-line px-5 py-4 sm:flex sm:justify-between sm:gap-4 sm:px-6"
        >
          <button
            className="row-start-2 inline-flex min-h-11 w-full cursor-pointer items-center justify-center gap-2 rounded-control border border-line bg-surface px-3.5 text-sm font-semibold text-ink transition-colors duration-200 hover:border-slate-400 hover:bg-slate-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-700 disabled:cursor-not-allowed disabled:text-muted sm:w-auto"
            type="button"
            disabled={clientPage.page === 1}
            onClick={onPreviousPage}
          >
            <FiChevronLeft aria-hidden="true" className="size-4" />
            Previous
          </button>
          <p className="col-span-2 row-start-1 text-center text-sm whitespace-nowrap text-muted sm:col-auto sm:row-auto">
            Page {clientPage.page} of {totalPages}
          </p>
          <button
            className="row-start-2 inline-flex min-h-11 w-full cursor-pointer items-center justify-center gap-2 rounded-control border border-line bg-surface px-3.5 text-sm font-semibold text-ink transition-colors duration-200 hover:border-slate-400 hover:bg-slate-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-700 disabled:cursor-not-allowed disabled:text-muted sm:w-auto"
            type="button"
            disabled={clientPage.page >= totalPages}
            onClick={onNextPage}
          >
            Next
            <FiChevronRight aria-hidden="true" className="size-4" />
          </button>
        </nav>
      )}
    </div>
  )
}

function ClientsLoading() {
  return (
    <div
      className="overflow-hidden rounded-panel border border-line bg-surface shadow-panel"
      role="status"
    >
      <span className="sr-only">Loading clients…</span>
      <div
        className="border-b border-line px-5 py-5 sm:px-6"
        aria-hidden="true"
      >
        <div className="h-4 w-36 animate-pulse rounded bg-slate-200 motion-reduce:animate-none" />
      </div>
      <div
        className="flex items-center gap-4 px-5 py-5 sm:px-6"
        aria-hidden="true"
      >
        <div className="size-11 animate-pulse rounded-control bg-slate-200 motion-reduce:animate-none" />
        <div className="flex-1">
          <div className="h-4 max-w-56 animate-pulse rounded bg-slate-200 motion-reduce:animate-none" />
          <div className="mt-2 h-3 max-w-24 animate-pulse rounded bg-slate-100 motion-reduce:animate-none" />
        </div>
      </div>
    </div>
  )
}

interface ClientsErrorProps {
  isRetrying: boolean
  onRetry: () => void
}

function ClientsError({ isRetrying, onRetry }: ClientsErrorProps) {
  return (
    <div
      className="rounded-panel border border-red-200 bg-red-50 p-6 sm:p-8"
      role="alert"
    >
      <FiAlertCircle aria-hidden="true" className="size-6 text-red-700" />
      <h2 className="mt-4 text-lg font-semibold text-ink">
        We couldn’t load this organisation’s clients
      </h2>
      <p className="mt-2 max-w-xl text-sm leading-6 text-muted">
        Check your connection and try again.
      </p>
      <button
        className="mt-5 inline-flex min-h-11 cursor-pointer items-center justify-center gap-2 rounded-control border border-red-300 bg-white px-4 text-sm font-semibold text-red-800 transition-colors duration-200 hover:bg-red-100 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-700 disabled:cursor-wait disabled:text-red-400"
        type="button"
        disabled={isRetrying}
        onClick={onRetry}
      >
        <FiRefreshCw
          aria-hidden="true"
          className={`size-4 ${isRetrying ? 'animate-spin motion-reduce:animate-none' : ''}`}
        />
        {isRetrying ? 'Trying again…' : 'Try again'}
      </button>
    </div>
  )
}

function OrganisationUnavailable() {
  return (
    <div className="rounded-panel border border-line bg-surface p-6 shadow-panel sm:p-8">
      <FiAlertCircle aria-hidden="true" className="size-6 text-muted" />
      <h2 className="mt-4 text-lg font-semibold text-ink">
        Organisation unavailable
      </h2>
      <p className="mt-2 max-w-xl text-sm leading-6 text-muted">
        This organisation may no longer exist, or your account may no longer
        have access to it.
      </p>
      <Link
        className="mt-5 inline-flex min-h-11 items-center justify-center rounded-control bg-brand-700 px-4 text-sm font-semibold text-white transition-colors duration-200 hover:bg-brand-950 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-700"
        to="/organisations"
      >
        Return to organisations
      </Link>
    </div>
  )
}

interface ClientsEmptyProps {
  organisationName: string | undefined
}

function ClientsEmpty({ organisationName }: ClientsEmptyProps) {
  return (
    <div className="rounded-panel border border-line bg-surface px-6 py-12 text-center shadow-panel sm:px-8 sm:py-16">
      <span className="mx-auto grid size-12 place-items-center rounded-control bg-blue-50 text-brand-700">
        <FiUsers aria-hidden="true" className="size-6" />
      </span>
      <h2 className="mt-5 text-lg font-semibold text-ink">No clients yet</h2>
      <p className="mx-auto mt-2 max-w-md text-sm leading-6 text-muted">
        {organisationName
          ? `${organisationName} does not have any clients yet.`
          : 'This organisation does not have any clients yet.'}
      </p>
    </div>
  )
}
