-- Ledgance foundation schema: organizations, membership and subscription state.
-- Product tables for Audit and Accounting are introduced by their own phases.

create extension if not exists "pgcrypto";

create table if not exists public.organizations (
    id uuid primary key default gen_random_uuid(),
    name text not null,
    slug text not null unique,
    created_at timestamptz not null default now()
);

create table if not exists public.organization_members (
    id uuid primary key default gen_random_uuid(),
    organization_id uuid not null references public.organizations (id) on delete cascade,
    user_id uuid not null references auth.users (id) on delete cascade,
    role text not null check (role in ('Owner', 'Admin', 'Manager', 'Member', 'Viewer')),
    is_default boolean not null default false,
    created_at timestamptz not null default now(),
    unique (organization_id, user_id)
);

create index if not exists organization_members_user_id_idx
    on public.organization_members (user_id);

create table if not exists public.organization_subscriptions (
    id uuid primary key default gen_random_uuid(),
    organization_id uuid not null references public.organizations (id) on delete cascade,
    module text not null check (module in ('Audit', 'Accounting')),
    plan text not null,
    status text not null check (status in ('Active', 'Trialing', 'PastDue', 'Canceled')),
    stripe_customer_id text,
    stripe_subscription_id text,
    current_period_end timestamptz,
    entitlement_overrides jsonb not null default '{}'::jsonb,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    unique (organization_id, module)
);

-- security definer so the membership lookup does not re-enter the policies that call it
create or replace function public.is_organization_member(target_organization uuid)
returns boolean
language sql
stable
security definer
set search_path = public
as $$
    select exists (
        select 1
        from public.organization_members m
        where m.organization_id = target_organization
          and m.user_id = auth.uid()
    );
$$;

alter table public.organizations enable row level security;
alter table public.organization_members enable row level security;
alter table public.organization_subscriptions enable row level security;

-- Row-level security is the backstop for direct client access. The API holds a service-role key
-- and enforces the same isolation in code through SupabaseRepository, so no write policies are
-- granted to end users here.
create policy organizations_read_own
    on public.organizations for select
    to authenticated
    using (public.is_organization_member(id));

create policy organization_members_read_own
    on public.organization_members for select
    to authenticated
    using (public.is_organization_member(organization_id));

create policy organization_subscriptions_read_own
    on public.organization_subscriptions for select
    to authenticated
    using (public.is_organization_member(organization_id));
