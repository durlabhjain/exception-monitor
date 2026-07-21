create extension if not exists pgcrypto;

create table if not exists clients (
    id uuid primary key default gen_random_uuid(),
    name text not null,
    slug text not null unique,
    is_active boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table if not exists applications (
    id uuid primary key default gen_random_uuid(),
    client_id uuid not null references clients(id) on delete cascade,
    name text not null,
    slug text not null,
    default_environment text not null default 'production',
    default_retention_days integer not null default 90 check (default_retention_days between 1 and 3650),
    is_active boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    unique (client_id, slug)
);

create table if not exists application_environments (
    id uuid primary key default gen_random_uuid(),
    application_id uuid not null references applications(id) on delete cascade,
    name text not null,
    notifications_enabled boolean not null default false,
    created_at timestamptz not null default now()
);

create table if not exists api_keys (
    id uuid primary key default gen_random_uuid(),
    application_id uuid not null references applications(id) on delete cascade,
    name text not null,
    key_prefix text not null unique,
    key_hash text not null,
    retention_days integer not null default 90 check (retention_days between 1 and 3650),
    rate_limit_per_minute integer not null default 1500 check (rate_limit_per_minute > 0),
    allow_all_ips boolean not null default true,
    is_active boolean not null default true,
    expires_at timestamptz null,
    last_used_at timestamptz null,
    created_at timestamptz not null default now(),
    revoked_at timestamptz null
);

create table if not exists api_key_ip_allowlists (
    id uuid primary key default gen_random_uuid(),
    api_key_id uuid not null references api_keys(id) on delete cascade,
    cidr cidr not null,
    note text null,
    created_at timestamptz not null default now(),
    unique (api_key_id, cidr)
);

create table if not exists users (
    id uuid primary key default gen_random_uuid(),
    email text not null unique,
    display_name text null,
    is_active boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table if not exists user_identities (
    id uuid primary key default gen_random_uuid(),
    user_id uuid not null references users(id) on delete cascade,
    provider text not null,
    provider_subject text not null,
    email text not null,
    email_verified boolean not null default false,
    last_login_at timestamptz null,
    unique (provider, provider_subject)
);

create table if not exists user_client_access (
    id uuid primary key default gen_random_uuid(),
    user_id uuid not null references users(id) on delete cascade,
    client_id uuid not null references clients(id) on delete cascade,
    role text not null check (role in ('SystemAdmin','ClientAdmin','AppAdmin','Developer','Viewer')),
    all_applications boolean not null default false,
    created_at timestamptz not null default now(),
    unique (user_id, client_id)
);

create table if not exists user_application_access (
    id uuid primary key default gen_random_uuid(),
    user_id uuid not null references users(id) on delete cascade,
    application_id uuid not null references applications(id) on delete cascade,
    role text not null check (role in ('AppAdmin','Developer','Viewer')),
    created_at timestamptz not null default now(),
    unique (user_id, application_id)
);

create table if not exists exception_groups (
    id uuid primary key default gen_random_uuid(),
    client_id uuid not null references clients(id) on delete cascade,
    application_id uuid not null references applications(id) on delete cascade,
    environment text not null,
    fingerprint text not null,
    exception_type text null,
    normalized_message text not null,
    severity text not null default 'Error',
    status text not null default 'Open' check (status in ('Open','Acknowledged','Resolved','Ignored')),
    first_seen_at timestamptz not null,
    last_seen_at timestamptz not null,
    total_count bigint not null default 0,
    last_event_id uuid null,
    notification_suppressed_until timestamptz null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    unique (application_id, environment, fingerprint)
);

create table if not exists exception_events (
    id uuid primary key default gen_random_uuid(),
    client_id uuid not null references clients(id) on delete cascade,
    application_id uuid not null references applications(id) on delete cascade,
    api_key_id uuid not null references api_keys(id) on delete restrict,
    group_id uuid null references exception_groups(id) on delete set null,
    environment text not null,
    severity text not null default 'Error',
    exception_type text null,
    message text not null,
    stack_trace text not null,
    fingerprint text not null,
    occurred_at timestamptz not null,
    received_at timestamptz not null default now(),
    source text null,
    release text null,
    correlation_id text null,
    trace_id text null,
    span_id text null,
    user_hash text null,
    request_method text null,
    request_url text null,
    request_route text null,
    request_referrer text null,
    request_status_code integer null,
    remote_ip inet null,
    user_agent text null,
    request_headers jsonb null,
    request_params jsonb null,
    request_body jsonb null,
    query_string jsonb null,
    payload_format text not null,
    payload_size integer not null,
    tags jsonb not null default '{}'::jsonb,
    metadata jsonb not null default '{}'::jsonb,
    raw_payload jsonb null,
    search_vector tsvector generated always as (
        setweight(to_tsvector('english', coalesce(exception_type, '')), 'A') ||
        setweight(to_tsvector('english', coalesce(message, '')), 'A') ||
        setweight(to_tsvector('english', coalesce(stack_trace, '')), 'B') ||
        setweight(to_tsvector('english', coalesce(request_url, '')), 'C') ||
        setweight(to_tsvector('english', coalesce(request_route, '')), 'C')
    ) stored
);

create table if not exists notification_recipients (
    id uuid primary key default gen_random_uuid(),
    application_id uuid not null references applications(id) on delete cascade,
    type text not null check (type in ('Email','Webhook')),
    name text not null,
    email text null,
    webhook_url text null,
    webhook_secret text null,
    is_active boolean not null default true,
    created_at timestamptz not null default now()
);

create table if not exists notification_rules (
    id uuid primary key default gen_random_uuid(),
    application_id uuid not null references applications(id) on delete cascade,
    environment text null,
    name text not null,
    event_type text not null check (event_type in ('FirstSeen','Regression','Threshold','Digest')),
    severity_minimum text not null default 'Error',
    threshold_count integer null,
    threshold_window_minutes integer null,
    cooldown_minutes integer not null default 60,
    digest_interval_minutes integer null,
    is_active boolean not null default true,
    created_at timestamptz not null default now()
);

create table if not exists notification_deliveries (
    id uuid primary key default gen_random_uuid(),
    application_id uuid not null references applications(id) on delete cascade,
    recipient_id uuid not null references notification_recipients(id) on delete cascade,
    rule_id uuid null references notification_rules(id) on delete set null,
    group_id uuid null references exception_groups(id) on delete set null,
    delivery_type text not null check (delivery_type in ('Email','Webhook')),
    subject text not null,
    body text not null,
    payload jsonb not null default '{}'::jsonb,
    status text not null default 'Pending' check (status in ('Pending','Sent','Failed','Abandoned')),
    attempt_count integer not null default 0,
    next_attempt_at timestamptz not null default now(),
    last_attempt_at timestamptz null,
    last_error text null,
    created_at timestamptz not null default now(),
    sent_at timestamptz null
);

create table if not exists audit_log (
    id uuid primary key default gen_random_uuid(),
    actor_user_id uuid null references users(id) on delete set null,
    actor_email text null,
    action text not null,
    entity_type text not null,
    entity_id uuid null,
    details jsonb not null default '{}'::jsonb,
    created_at timestamptz not null default now()
);

create unique index if not exists ux_application_environments_application_lower_name on application_environments(application_id, lower(name));
create index if not exists ix_applications_client on applications(client_id);
create index if not exists ix_api_keys_application on api_keys(application_id);
create index if not exists ix_exception_events_app_received on exception_events(application_id, received_at desc);
create index if not exists ix_exception_events_group_received on exception_events(group_id, received_at desc);
create index if not exists ix_exception_events_search on exception_events using gin(search_vector);
create index if not exists ix_exception_events_tags on exception_events using gin(tags);
create index if not exists ix_exception_events_metadata on exception_events using gin(metadata jsonb_path_ops);
create index if not exists ix_exception_groups_app_status_last on exception_groups(application_id, status, last_seen_at desc);
create index if not exists ix_notification_deliveries_pending on notification_deliveries(status, next_attempt_at) where status = 'Pending';
