# Local demo data

EngageOps uses one development account with three organisations:

| Organisation             | Clients | Purpose                                                     |
| ------------------------ | ------: | ----------------------------------------------------------- |
| Northstar Demo Workforce |      45 | Larger client list: three pages at the UI's page size of 20 |
| Cedar Demo Workforce     |       3 | Small populated list                                        |
| Newhaven Demo Workforce  |       0 | Empty state and adding a first client                       |

The account defaults to `demo@engageops.local` with password `LocalDevelopment1!`.
Set `DEVELOPMENT_DATA_EMAIL` and `DEVELOPMENT_DATA_PASSWORD` in the local `.env`
file to change these defaults. They are local development credentials only.

Organisation names and client datasets have one owner:
[`DevelopmentDataCatalog.cs`](../src/backend/EngageOps.Api/DevelopmentData/DevelopmentDataCatalog.cs).

## Seed

From the repository root, with the Compose services running from the current source:

```powershell
./scripts/development-data.ps1 seed
```

For initial setup, copy `.env.example` to `.env`, set the database password, then run
`docker compose up --build --watch`. Open `http://localhost:5173` after seeding.

Seeding creates the account and its organisation memberships as needed. Existing
accounts keep their password. Repeating the command adds missing clients without
duplicating names, including case-only differences. Existing records and manually
added clients are preserved, so counts can grow beyond the baseline above.

Run seed/reset commands one at a time. They run only in the Development environment
and apply outstanding migrations before operating. Starting the API alone does not
seed data.

## Reset

```powershell
./scripts/development-data.ps1 reset
./scripts/development-data.ps1 seed
```

Reset removes all clients, workers and assignments in the configured account's
three named demo organisations, including records added manually. It removes those
organisations and their memberships in the same transaction. The account is deleted
only if it has no remaining organisation memberships. Seeding then recreates the
baseline; sign in again if the account was recreated.

Other accounts and unrelated organisations are preserved, even if another account
has an organisation with the same name. Reset stops without deleting data when a
matching organisation has other members, or when the configured account has multiple
organisations with the same demo name. Seeding also rejects ambiguous names.

The catalogue names identify the dataset. Keep them stable when extending it;
renaming an organisation or changing the configured account email does not clean up
its old data automatically. Inspect and explicitly scope any legacy-data cleanup.

## Test fixtures

Sample names and accounts inside `tests/backend` and frontend `*.test.tsx` files
are independent test fixtures. Backend integration tests use disposable PostgreSQL
Testcontainers; frontend tests mock API responses. These fixtures do not populate
the local Compose database and should stay independent of the demo catalogue so
changing a demo cannot silently change a test's inputs or expectations.
