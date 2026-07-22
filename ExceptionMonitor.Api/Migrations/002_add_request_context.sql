alter table exception_events add column if not exists request_headers jsonb null;
alter table exception_events add column if not exists request_params jsonb null;
alter table exception_events add column if not exists request_body jsonb null;
alter table exception_events add column if not exists query_string jsonb null;
