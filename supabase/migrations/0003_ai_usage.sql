-- Monthly AI usage accounting per organization and product module.

create table if not exists public.ai_usage (
    id uuid primary key default gen_random_uuid(),
    organization_id uuid not null references public.organizations (id) on delete cascade,
    module text not null check (module in ('Audit', 'Accounting')),
    period text not null,
    units_used bigint not null default 0,
    updated_at timestamptz not null default now(),
    unique (organization_id, module, period)
);

alter table public.ai_usage enable row level security;

create policy ai_usage_read_own
    on public.ai_usage for select
    to authenticated
    using (public.is_organization_member(organization_id));
