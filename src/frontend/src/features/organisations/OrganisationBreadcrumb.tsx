import { Link } from 'react-router'

interface OrganisationBreadcrumbProps {
  currentPage: string
  organisationName: string | undefined
}

export function OrganisationBreadcrumb({
  currentPage,
  organisationName,
}: OrganisationBreadcrumbProps) {
  return (
    <nav aria-label="Breadcrumb">
      <ol className="flex max-w-full flex-wrap items-center gap-x-1.5 gap-y-1 text-xs text-muted sm:gap-x-2 sm:text-sm">
        <li>
          <Link
            className="inline-flex min-h-11 items-center rounded-control font-semibold text-brand-700 transition-colors duration-200 hover:text-brand-950 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-700"
            to="/organisations"
          >
            Organisations
          </Link>
        </li>
        <li className="flex max-w-full min-w-0 items-center gap-2">
          <span aria-hidden="true" className="text-slate-300">
            /
          </span>
          <span className="min-w-0 font-medium break-words text-ink">
            {organisationName ?? 'Organisation'}
          </span>
        </li>
        <li className="flex max-w-full min-w-0 items-center gap-2">
          <span aria-hidden="true" className="text-slate-300">
            /
          </span>
          <span aria-current="page" className="min-w-0 break-words">
            {currentPage}
          </span>
        </li>
      </ol>
    </nav>
  )
}
