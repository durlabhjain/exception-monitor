# ExceptionMonitor

A .NET 10 backend/API for secure multi-tenant exception intake, grouping, search, retention, and notification delivery.

## Current scope

- PostgreSQL-backed schema using DbUp migrations.
- Dapper/Npgsql data access.
- Public JSON and form exception ingestion.
- API key authentication with hashed keys and optional IP/CIDR allowlist.
- Clients, applications, environments, users, app access, notification recipients, and rules.
- Error grouping with deterministic fingerprints.
- PostgreSQL full-text search over exception events.
- Email/webhook notification delivery worker.
- Per-key retention cleanup worker.

## Local configuration

Set the PostgreSQL connection string and secrets before running:

```bash
export ConnectionStrings__Postgres='Host=localhost;Port=5432;Database=exception_monitor;Username=postgres;Password=postgres'
export Security__ApiKeyHashSecret='replace-with-long-random-secret'
export Security__AdminApiKey='replace-with-admin-bootstrap-key'
```

Run the API:

```bash
dotnet run --project ExceptionMonitor.Api/ExceptionMonitor.Api.csproj
```

In development, admin APIs can be called with:

```text
X-Admin-Api-Key: dev-admin-key
```

Application ingestion uses either:

```text
Authorization: Bearer exm_<prefix>_<secret>
```

or:

```text
X-Exception-Api-Key: exm_<prefix>_<secret>
```

## Setup & API Usage

See [QUICKSETUP.md](QUICKSETUP.md) for full setup instructions, curl examples, and API reference.
