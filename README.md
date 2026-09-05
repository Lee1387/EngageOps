# EngageOps

EngageOps is an independently designed, multi-tenant workforce operations application.
It manages organisations, clients, workers and assignments through an ASP.NET Core
API, with a React workspace for organisation access and client management.

## Implemented functionality

| Area           | API                                                                           | Browser workspace                                                 |
| -------------- | ----------------------------------------------------------------------------- | ----------------------------------------------------------------- |
| Authentication | Registration, cookie sign-in, session lookup, sign-out and antiforgery tokens | Sign-in, session handling and sign-out                            |
| Organisations  | Account/organisation provisioning and membership-scoped listing               | List accessible organisations and enter a client workspace        |
| Clients        | Organisation-scoped creation and paginated listing                            | Inline creation, paginated listing and loading/empty/error states |
| Workers        | Organisation-scoped creation and paginated listing                            | API only                                                          |
| Assignments    | Creation, paginated listing, detail and cancellation                          | API only                                                          |

Assignments link a worker and client within the same organisation, with a start date,
optional end date and a `Confirmed` or `Cancelled` status. Access is controlled by
organisation membership, with tenant relationships also enforced in PostgreSQL.

## Stack

- **Backend:** C# / .NET 10, ASP.NET Core Minimal APIs, EF Core, Npgsql and ASP.NET Core Identity.
- **Frontend:** React 19, strict TypeScript, Vite, React Router, TanStack Query, Tailwind CSS and React Icons.
- **Data:** PostgreSQL, with EF Core migrations.
- **Testing:** xUnit v3 with Microsoft.Testing.Platform, PostgreSQL Testcontainers, Vitest and React Testing Library.
- **Tooling:** Docker Compose, GitHub Actions, CodeQL and dependency monitoring.

The backend is a single feature-oriented API project. The frontend has matching
feature folders for the implemented browser workflows.

## Run locally

Follow [Local development](docs/local-development.md) to configure and start the
Compose services, then use [Local demo data](docs/development-data.md) to seed an
account with empty, small and paginated client lists. The browser workspace runs at
<http://localhost:5173>.

## Documentation

- [Local development](docs/local-development.md): prerequisites, configuration, containers and migrations.
- [Architecture](docs/architecture.md): code organisation, request flow and security/data boundaries.
- [Testing](docs/testing.md): validation commands, test coverage and CI checks.
- [Local demo data](docs/development-data.md): demo account, datasets and seed/reset behaviour.

## License

[MIT](LICENSE).
