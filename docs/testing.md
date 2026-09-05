# Testing and validation

Backend tests live in [`tests/backend/EngageOps.Api.Tests`](../tests/backend/EngageOps.Api.Tests).
Frontend tests are colocated as `*.test.tsx` files in
[`src/frontend/src`](../src/frontend/src).

## Prerequisites

Use the host tool versions listed in [Local development](local-development.md#prerequisites).
Restore dependencies from the repository root:

```powershell
dotnet restore EngageOps.slnx
```

From `src/frontend`:

```powershell
pnpm install --frozen-lockfile
```

Backend integration tests require a running Docker engine with Linux container
support and access to the PostgreSQL image selected by
[`PostgreSqlTestDatabase.cs`](../tests/backend/EngageOps.Api.Tests/Persistence/PostgreSqlTestDatabase.cs).
Testcontainers starts isolated databases and the tests apply EF migrations. The
Compose application and its demo account do not need to be running or seeded.
Frontend tests run in jsdom and mock API responses, so they do not require the API
or PostgreSQL.

## Backend

Run from the repository root:

```powershell
dotnet format EngageOps.slnx --verify-no-changes
dotnet build EngageOps.slnx
dotnet test --solution EngageOps.slnx
```

The solution uses xUnit v3 and Microsoft.Testing.Platform, selected in
[`global.json`](../global.json). The shared build configuration enables nullable
reference types, code-style analysis and warnings-as-errors.

The suite covers:

- Entity validation and assignment cancellation rules.
- EF mappings, migrations, relational constraints and persistence.
- Membership checks and concealment of inaccessible tenants/resources.
- Cookie sessions, antiforgery, lockout, malformed inputs and safe HTTP errors.
- Account provisioning, rollback and concurrent duplicate registration.
- Client/worker creation, pagination and assignment creation/list/detail/cancellation.
- Demo seeding, repeatability, reset scope and ambiguous/shared organisation safeguards.

[`EngageOpsApiFactory`](../tests/backend/EngageOps.Api.Tests/EngageOpsApiFactory.cs)
hosts the application through `WebApplicationFactory` and injects test database
configuration. Shared HTTP helpers handle cookies, antiforgery and response
assertions. Database tests use real PostgreSQL constraints and migrations.

## Frontend

Run from `src/frontend`:

```powershell
pnpm format:check
pnpm lint
pnpm typecheck
pnpm test
pnpm build
```

`pnpm build` also runs TypeScript checking before the Vite build. For interactive
test feedback, use `pnpm test:watch`.

Vitest and React Testing Library cover session/sign-in/sign-out behaviour,
organisation access, client pagination and creation, field validation, retries,
expired sessions, cached-data visibility and focus restoration. Shared test setup
provides DOM matchers and cleanup; test query clients disable automatic retries.

These tests use jsdom and mocked `fetch`. They do not validate real browser layout
or the full browser-to-database path. For UI changes, also exercise the affected
workflow in the running application at desktop and smaller widths, with keyboard
navigation and the relevant loading, empty, success and failure states. The
[demo organisations](development-data.md) provide empty, small and paginated lists
for those checks.

## Local infrastructure

Run from the repository root with `.env` configured:

```powershell
docker compose config --quiet
docker compose ps
```

The first command validates configuration without printing interpolated values.
The second shows service state; it does not build or start services. PostgreSQL has
a container health check. The API's `/health` endpoint checks application liveness,
so verify an affected API workflow as well when checking database-backed behaviour.

## Dependency checks

When changing dependencies, run from the repository root:

```powershell
dotnet package list --outdated
dotnet package list --vulnerable
```

From `src/frontend`:

```powershell
pnpm outdated
pnpm audit
```

## Automated checks

[`ci.yml`](../.github/workflows/ci.yml) runs on pushes and pull requests to `main`:

- Backend restore, formatting verification, Release build and tests.
- Compose configuration validation and backend/frontend Docker image builds.
- Frontend frozen-lockfile installation, formatting, lint, tests and build.

[`codeql.yml`](../.github/workflows/codeql.yml) scans C# and JavaScript/TypeScript on
pushes/pull requests to `main` and weekly. The scheduled/manual
[`dependency-audit.yml`](../.github/workflows/dependency-audit.yml) runs `pnpm audit`.
[`dependabot.yml`](../.github/dependabot.yml) configures weekly updates for NuGet,
the .NET SDK, Docker Compose and GitHub Actions.

Test fixtures remain independent of demo datasets; see
[Test fixtures](development-data.md#test-fixtures) for that distinction.
