import { FiAlertCircle, FiBriefcase, FiRefreshCw } from 'react-icons/fi'
import { useOrganisations } from './useOrganisations'

interface OrganisationsPageProps {
  userId: string
}

export function OrganisationsPage({ userId }: OrganisationsPageProps) {
  const organisations = useOrganisations(userId)

  return (
    <section aria-labelledby="organisations-heading">
      <div>
        <p className="text-xs font-semibold tracking-[0.12em] text-brand-700 uppercase">
          Workspace
        </p>
        <h1
          className="mt-3 text-3xl font-semibold tracking-[-0.03em] text-ink sm:text-4xl"
          id="organisations-heading"
        >
          Organisations
        </h1>
        <p className="mt-4 max-w-2xl text-base leading-7 text-muted">
          View the organisations your account can access.
        </p>
      </div>

      <div className="mt-8 sm:mt-10">
        {organisations.isPending && <OrganisationsLoading />}

        {organisations.isError && (
          <div
            className="rounded-panel border border-red-200 bg-red-50 p-6 sm:p-8"
            role="alert"
          >
            <FiAlertCircle aria-hidden="true" className="size-6 text-red-700" />
            <h2 className="mt-4 text-lg font-semibold text-ink">
              We couldn’t load your organisations
            </h2>
            <p className="mt-2 max-w-xl text-sm leading-6 text-muted">
              Check your connection and try again.
            </p>
            <button
              className="mt-5 inline-flex min-h-11 cursor-pointer items-center justify-center gap-2 rounded-control border border-red-300 bg-white px-4 text-sm font-semibold text-red-800 transition-colors duration-200 hover:bg-red-100 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-700 disabled:cursor-wait disabled:text-red-400"
              type="button"
              disabled={organisations.isFetching}
              onClick={() => void organisations.refetch()}
            >
              <FiRefreshCw
                aria-hidden="true"
                className={`size-4 ${organisations.isFetching ? 'animate-spin motion-reduce:animate-none' : ''}`}
              />
              {organisations.isFetching ? 'Trying again…' : 'Try again'}
            </button>
          </div>
        )}

        {organisations.data?.length === 0 && <OrganisationsEmpty />}

        {organisations.data && organisations.data.length > 0 && (
          <div className="overflow-hidden rounded-panel border border-line bg-surface shadow-panel">
            <div className="flex items-center justify-between gap-4 border-b border-line px-5 py-4 sm:px-6">
              <h2 className="text-sm font-semibold text-ink">
                Your organisations
              </h2>
              <span className="rounded-full bg-blue-50 px-2.5 py-1 text-xs font-semibold text-brand-700">
                {organisations.data.length}
              </span>
            </div>
            <ul aria-label="Organisations" className="divide-y divide-line">
              {organisations.data.map((organisation) => (
                <li
                  className="flex items-center gap-4 px-5 py-5 sm:px-6"
                  key={organisation.id}
                >
                  <span className="grid size-11 shrink-0 place-items-center rounded-control bg-blue-50 text-brand-700">
                    <FiBriefcase aria-hidden="true" className="size-5" />
                  </span>
                  <div className="min-w-0">
                    <p className="truncate text-base font-semibold text-ink">
                      {organisation.name}
                    </p>
                    <p className="mt-1 text-sm text-muted">Organisation</p>
                  </div>
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>
    </section>
  )
}

function OrganisationsLoading() {
  return (
    <div
      className="overflow-hidden rounded-panel border border-line bg-surface shadow-panel"
      role="status"
    >
      <span className="sr-only">Loading organisations…</span>
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

function OrganisationsEmpty() {
  return (
    <div className="rounded-panel border border-line bg-surface px-6 py-12 text-center shadow-panel sm:px-8 sm:py-16">
      <span className="mx-auto grid size-12 place-items-center rounded-control bg-blue-50 text-brand-700">
        <FiBriefcase aria-hidden="true" className="size-6" />
      </span>
      <h2 className="mt-5 text-lg font-semibold text-ink">
        No organisations yet
      </h2>
      <p className="mx-auto mt-2 max-w-md text-sm leading-6 text-muted">
        Your account is not currently linked to an organisation.
      </p>
    </div>
  )
}
