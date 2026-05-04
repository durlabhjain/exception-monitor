# ExceptionMonitor — Quick Setup Guide

A .NET 10 backend API for secure multi-tenant exception intake, grouping, search, retention, and notification delivery.

---

## What It Does

- Receives exceptions from your apps via HTTP (JSON or form POST)
- Groups duplicate errors using deterministic fingerprints — same error type + message + stack frame = one group
- Tracks first seen, last seen, and occurrence count per error group
- Full-text search over exceptions using PostgreSQL `tsvector`
- Email and webhook notifications when new errors are first seen
- Per-API-key data retention with automatic cleanup
- Multi-tenant: supports multiple clients and applications under one deployment

---

## Prerequisites

| Requirement | Version | Notes |
|---|---|---|
| .NET SDK | 10.x | Must be SDK, not just Runtime |
| PostgreSQL | 14+ | See platform-specific install below |

---

## Installation

### macOS

#### 1. Install .NET 10 SDK

Download from https://dotnet.microsoft.com/download/dotnet/10.0 — choose the **macOS SDK** installer (arm64 for Apple Silicon, x64 for Intel).

Verify:
```bash
dotnet --version
# must print 10.x.x
```

#### 2. Install PostgreSQL

```bash
brew install postgresql@16
brew services start postgresql@16
```

#### 3. Database Setup

On macOS, Homebrew installs PostgreSQL with your system username as the superuser (not `postgres`). Run these once:

```bash
# Create the postgres role
psql postgres -c "CREATE ROLE postgres WITH SUPERUSER LOGIN PASSWORD 'postgres';"

# Create the database
psql postgres -c "CREATE DATABASE exception_monitor OWNER postgres;"

# Enable pgcrypto extension (required for UUID generation)
psql -U postgres -d exception_monitor -c "CREATE EXTENSION IF NOT EXISTS pgcrypto;"
```

Verify extension:
```bash
psql -U postgres -d exception_monitor -c "SELECT * FROM pg_extension WHERE extname = 'pgcrypto';"
```

#### 4. Run the Server

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run --project ExceptionMonitor.Api/ExceptionMonitor.Api.csproj
```

---

### Windows

#### 1. Install .NET 10 SDK

Download from https://dotnet.microsoft.com/download/dotnet/10.0 — choose **Windows x64 SDK installer**.

Verify in Command Prompt or PowerShell:
```cmd
dotnet --version
```
Must print `10.x.x`.

#### 2. Install PostgreSQL

**Option A — Installer (recommended):**

Download the Windows installer from https://www.postgresql.org/download/windows/ and run it.
- Set a password for the `postgres` superuser during installation (e.g. `postgres`)
- Default port: `5432`
- After install, PostgreSQL service starts automatically

**Option B — winget:**
```cmd
winget install PostgreSQL.PostgreSQL.16
```

**Option C — Chocolatey:**
```cmd
choco install postgresql16
```

#### 3. Database Setup

Open **Command Prompt** or **PowerShell** and run:

```cmd
psql -U postgres -c "CREATE DATABASE exception_monitor OWNER postgres;"
```

Enable the `pgcrypto` extension:

```cmd
psql -U postgres -d exception_monitor -c "CREATE EXTENSION IF NOT EXISTS pgcrypto;"
```

Verify:
```cmd
psql -U postgres -d exception_monitor -c "SELECT * FROM pg_extension WHERE extname = 'pgcrypto';"
```

> If `psql` is not found, add PostgreSQL's `bin` folder to your PATH:
> `C:\Program Files\PostgreSQL\16\bin`

#### 4. Run the Server

**Command Prompt:**
```cmd
set ASPNETCORE_ENVIRONMENT=Development
dotnet run --project ExceptionMonitor.Api/ExceptionMonitor.Api.csproj
```

**PowerShell:**
```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project ExceptionMonitor.Api/ExceptionMonitor.Api.csproj
```

---

## Configuration

### Development (default — no changes needed)

The file `ExceptionMonitor.Api/appsettings.Development.json` has working defaults:

```json
{
  "Security": {
    "ApiKeyHashSecret": "development-only-change-me",
    "AdminApiKey": "dev-admin-key"
  }
}
```

- **`ApiKeyHashSecret`** — HMAC secret used to hash API keys before storing. Never change this after creating keys — all existing keys will stop working.
- **`AdminApiKey`** — Static key required in the `X-Admin-Api-Key` header for all admin and query endpoints.

### Production

Generate strong secrets:

**macOS / Linux:**
```bash
openssl rand -hex 32   # for ApiKeyHashSecret
openssl rand -hex 24   # for AdminApiKey
```

**Windows PowerShell:**
```powershell
[System.Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32)).ToLower()
[System.Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(24)).ToLower()
```

Set via environment variables (never commit secrets):

**macOS / Linux:**
```bash
export ConnectionStrings__Postgres='Host=localhost;Port=5432;Database=exception_monitor;Username=postgres;Password=postgres'
export Security__ApiKeyHashSecret='<generated-secret>'
export Security__AdminApiKey='<generated-admin-key>'
```

**Windows Command Prompt:**
```cmd
set ConnectionStrings__Postgres=Host=localhost;Port=5432;Database=exception_monitor;Username=postgres;Password=postgres
set Security__ApiKeyHashSecret=<generated-secret>
set Security__AdminApiKey=<generated-admin-key>
```

**Windows PowerShell:**
```powershell
$env:ConnectionStrings__Postgres = "Host=localhost;Port=5432;Database=exception_monitor;Username=postgres;Password=postgres"
$env:Security__ApiKeyHashSecret = "<generated-secret>"
$env:Security__AdminApiKey = "<generated-admin-key>"
```

### SMTP (optional — only needed for email notifications)

**macOS / Linux:**
```bash
export Smtp__Host='smtp.example.com'
export Smtp__Port='587'
export Smtp__UseSsl='true'
export Smtp__Username='user@example.com'
export Smtp__Password='yourpassword'
```

**Windows PowerShell:**
```powershell
$env:Smtp__Host = "smtp.example.com"
$env:Smtp__Port = "587"
$env:Smtp__UseSsl = "true"
$env:Smtp__Username = "user@example.com"
$env:Smtp__Password = "yourpassword"
```

---

## Verify the Server is Running

The server starts on **http://localhost:5180**.

On first run, migrations execute automatically and all tables are created.

```bash
curl http://localhost:5180/health
curl http://localhost:5180/
```

Expected response from `/`:
```json
{ "service": "ExceptionMonitor.Api", "status": "Running" }
```

---

## API Overview

| Group | Auth Header | Base Path |
|---|---|---|
| Admin | `X-Admin-Api-Key` | `/api/admin` |
| Ingestion | `X-Exception-Api-Key` | `/api/ingest` |
| Query | `X-Admin-Api-Key` | `/api/error-groups`, `/api/events` |

> All curl examples below work on macOS, Linux, and Windows (curl is built into Windows 10+).  
> On Windows, replace single quotes `'` with double quotes `"` in Command Prompt, or use PowerShell examples provided.

---

## Step-by-Step: First-Time Setup via curl

### 1. Create a Client (tenant)

**macOS / Linux / Windows PowerShell:**
```bash
curl -s -X POST http://localhost:5180/api/admin/clients \
  -H "X-Admin-Api-Key: dev-admin-key" \
  -H "Content-Type: application/json" \
  -d "{\"name\": \"My Company\"}"
```

**Windows Command Prompt:**
```cmd
curl -s -X POST http://localhost:5180/api/admin/clients -H "X-Admin-Api-Key: dev-admin-key" -H "Content-Type: application/json" -d "{\"name\": \"My Company\"}"
```

Response:
```json
{
  "id": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "name": "My Company",
  "slug": "my-company",
  "isActive": true,
  "createdAt": "..."
}
```

Save the `id` as `CLIENT_ID`.

---

### 2. Create an Application

```bash
curl -s -X POST http://localhost:5180/api/admin/applications \
  -H "X-Admin-Api-Key: dev-admin-key" \
  -H "Content-Type: application/json" \
  -d "{\"clientId\": \"<CLIENT_ID>\", \"name\": \"My App\", \"defaultEnvironment\": \"production\", \"defaultRetentionDays\": 90}"
```

Save the `id` as `APPLICATION_ID`.

---

### 3. Create an API Key

```bash
curl -s -X POST http://localhost:5180/api/admin/api-keys \
  -H "X-Admin-Api-Key: dev-admin-key" \
  -H "Content-Type: application/json" \
  -d "{\"applicationId\": \"<APPLICATION_ID>\", \"name\": \"my-key\", \"retentionDays\": 90, \"allowAllIps\": true}"
```

Response:
```json
{
  "id": "...",
  "name": "my-key",
  "keyPrefix": "abc123...",
  "plaintextKey": "exm_abc123_xxxxxxxxxxxxxxxxxxxx"
}
```

> Save the `plaintextKey` — it is shown only once and cannot be recovered.

---

### 4. Ingest an Exception

```bash
curl -s -X POST http://localhost:5180/api/ingest \
  -H "X-Exception-Api-Key: exm_abc123_xxxxxxxxxxxxxxxxxxxx" \
  -H "Content-Type: application/json" \
  -d "{\"message\": \"Object reference not set to an instance of an object\", \"exceptionType\": \"NullReferenceException\", \"stackTrace\": \"at MyApp.Services.UserService.GetUser(Int32 id) in UserService.cs:line 42\", \"severity\": \"Error\", \"environment\": \"production\", \"source\": \"MyApp.Api\", \"release\": \"1.0.0\"}"
```

Response (`202 Accepted`):
```json
{
  "eventId": "...",
  "groupId": "...",
  "fingerprint": "...",
  "status": "Accepted"
}
```

Save `groupId` for the next steps.

---

### 5. Ingest with Full Request Context

```bash
curl -s -X POST http://localhost:5180/api/ingest \
  -H "X-Exception-Api-Key: exm_abc123_xxxxxxxxxxxxxxxxxxxx" \
  -H "Content-Type: application/json" \
  -d "{\"message\": \"Database connection timeout\", \"exceptionType\": \"SqlException\", \"stackTrace\": \"at MyApp.Data.DbContext.SaveChanges() in DbContext.cs:line 88\", \"severity\": \"Critical\", \"environment\": \"production\", \"correlationId\": \"req-abc-123\", \"request\": {\"method\": \"POST\", \"url\": \"https://api.myapp.com/orders\", \"route\": \"/orders\", \"statusCode\": 500}, \"tags\": {\"team\": \"backend\"}}"
```

---

## Querying Errors

### List all error groups

```bash
curl -s http://localhost:5180/api/error-groups -H "X-Admin-Api-Key: dev-admin-key"
```

### Filter by application, environment, or status

```bash
curl -s "http://localhost:5180/api/error-groups?applicationId=<APPLICATION_ID>&environment=production&status=Open" -H "X-Admin-Api-Key: dev-admin-key"
```

### Full-text search

```bash
curl -s "http://localhost:5180/api/error-groups?q=NullReference" -H "X-Admin-Api-Key: dev-admin-key"
```

### Get a specific group with its recent events

```bash
curl -s http://localhost:5180/api/error-groups/<GROUP_ID> -H "X-Admin-Api-Key: dev-admin-key"
```

### Update group status

```bash
curl -s -X POST "http://localhost:5180/api/error-groups/<GROUP_ID>/status?status=Resolved" -H "X-Admin-Api-Key: dev-admin-key"
```

Valid statuses: `Open` | `Acknowledged` | `Resolved` | `Ignored`

### List raw events

```bash
curl -s "http://localhost:5180/api/events?applicationId=<APPLICATION_ID>&severity=Error" -H "X-Admin-Api-Key: dev-admin-key"
```

### Get a single event

```bash
curl -s http://localhost:5180/api/events/<EVENT_ID> -H "X-Admin-Api-Key: dev-admin-key"
```

---

## Admin APIs

### List all clients

```bash
curl -s http://localhost:5180/api/admin/clients -H "X-Admin-Api-Key: dev-admin-key"
```

### List all applications

```bash
curl -s http://localhost:5180/api/admin/applications -H "X-Admin-Api-Key: dev-admin-key"
```

### Add an environment

```bash
curl -s -X POST http://localhost:5180/api/admin/environments \
  -H "X-Admin-Api-Key: dev-admin-key" \
  -H "Content-Type: application/json" \
  -d "{\"applicationId\": \"<APPLICATION_ID>\", \"name\": \"staging\", \"notificationsEnabled\": false}"
```

### Revoke an API key

```bash
curl -s -X POST http://localhost:5180/api/admin/api-keys/<KEY_ID>/revoke -H "X-Admin-Api-Key: dev-admin-key"
```

### Add a notification recipient (email)

```bash
curl -s -X POST http://localhost:5180/api/admin/recipients \
  -H "X-Admin-Api-Key: dev-admin-key" \
  -H "Content-Type: application/json" \
  -d "{\"applicationId\": \"<APPLICATION_ID>\", \"type\": \"Email\", \"name\": \"Dev Team\", \"email\": \"dev@mycompany.com\"}"
```

### Add a notification recipient (webhook)

```bash
curl -s -X POST http://localhost:5180/api/admin/recipients \
  -H "X-Admin-Api-Key: dev-admin-key" \
  -H "Content-Type: application/json" \
  -d "{\"applicationId\": \"<APPLICATION_ID>\", \"type\": \"Webhook\", \"name\": \"Slack Hook\", \"webhookUrl\": \"https://hooks.slack.com/services/xxx/yyy/zzz\"}"
```

### Create a notification rule

```bash
curl -s -X POST http://localhost:5180/api/admin/notification-rules \
  -H "X-Admin-Api-Key: dev-admin-key" \
  -H "Content-Type: application/json" \
  -d "{\"applicationId\": \"<APPLICATION_ID>\", \"name\": \"New errors in production\", \"eventType\": \"FirstSeen\", \"environment\": \"production\", \"severityMinimum\": \"Error\", \"cooldownMinutes\": 60}"
```

Supported `eventType` values: `FirstSeen`, `Regression`, `Threshold`, `Digest`

---

## Inspecting the Database Directly

**macOS / Linux:**
```bash
psql -U postgres -d exception_monitor
```

**Windows:**
```cmd
psql -U postgres -d exception_monitor
```

Useful queries:

```sql
-- All tables
\dt

-- All error groups
SELECT id, exception_type, normalized_message, status, total_count, first_seen_at, last_seen_at
FROM exception_groups
ORDER BY last_seen_at DESC;

-- All events
SELECT id, exception_type, message, severity, environment, received_at
FROM exception_events
ORDER BY received_at DESC;

-- Events for a specific group
SELECT id, message, occurred_at, request_url
FROM exception_events
WHERE group_id = '<GROUP_ID>'
ORDER BY received_at DESC;

-- Full-text search
SELECT id, message, exception_type
FROM exception_events
WHERE search_vector @@ websearch_to_tsquery('english', 'NullReference');

-- Pending notifications
SELECT id, delivery_type, subject, status, attempt_count, next_attempt_at
FROM notification_deliveries
WHERE status = 'Pending';

-- API keys
SELECT id, name, key_prefix, is_active, allow_all_ips, retention_days, last_used_at
FROM api_keys;
```

---

## Ingestion Payload Reference

| Field | Type | Required | Description |
|---|---|---|---|
| `message` | string | yes | Error message |
| `stackTrace` | string | yes | Full stack trace |
| `exceptionType` | string | no | e.g. `NullReferenceException` |
| `severity` | string | no | `Debug`, `Info`, `Warning`, `Error`, `Critical` (default: `Error`) |
| `environment` | string | no | Defaults to application's `defaultEnvironment` |
| `occurredAt` | ISO datetime | no | Defaults to now |
| `source` | string | no | Service or host name |
| `release` | string | no | App version/release tag |
| `correlationId` | string | no | Request or trace correlation ID |
| `traceId` | string | no | OpenTelemetry trace ID |
| `spanId` | string | no | OpenTelemetry span ID |
| `fingerprint` | string | no | Custom fingerprint to control grouping |
| `tags` | object | no | Key-value string pairs |
| `metadata` | object | no | Arbitrary JSON |
| `request.method` | string | no | HTTP method |
| `request.url` | string | no | Full request URL |
| `request.route` | string | no | Route template |
| `request.statusCode` | int | no | HTTP status code |

---