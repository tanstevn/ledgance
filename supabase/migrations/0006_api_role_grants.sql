-- Newer Supabase projects no longer grant the Data API roles default privileges on the
-- public schema, so the grants are explicit. The API's service-role client performs all
-- writes and enforces tenancy in code; signed-in users get read-only direct access as the
-- RLS-guarded backstop. The anon role gets nothing — unauthenticated clients have no
-- business reading these tables.

grant usage on schema public to authenticated, service_role;

grant all privileges on all tables in schema public to service_role;
grant select on all tables in schema public to authenticated;

-- Future migrations create tables under the same policy without repeating the grants.
alter default privileges in schema public grant all on tables to service_role;
alter default privileges in schema public grant select on tables to authenticated;
