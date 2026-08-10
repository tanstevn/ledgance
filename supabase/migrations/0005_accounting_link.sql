-- Per-organization opt-in that authorizes Audit to read the organization's own Ledgance
-- Accounting books (module-boundaries §4). Off by default; the API also requires the
-- accounting_context_sharing entitlement on both products before honoring it.

create table if not exists public.integration_accounting_links (
    id uuid primary key default gen_random_uuid(),
    organization_id uuid not null references public.organizations (id) on delete cascade,
    is_enabled boolean not null default false,
    updated_by uuid not null,
    updated_at timestamptz not null default now(),
    unique (organization_id)
);

alter table public.integration_accounting_links enable row level security;

create policy integration_accounting_links_read_own
    on public.integration_accounting_links for select
    to authenticated
    using (public.is_organization_member(organization_id));
