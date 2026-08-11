-- Phase 9 — billing state carried by subscription rows, plus webhook idempotency.

alter table public.organization_subscriptions
    add column if not exists cancel_at_period_end boolean not null default false;

-- The provider timestamp behind the row, so an out-of-order webhook can be recognised as stale.
alter table public.organization_subscriptions
    add column if not exists last_event_at timestamptz;

create index if not exists organization_subscriptions_stripe_subscription_idx
    on public.organization_subscriptions (stripe_subscription_id);

create index if not exists organization_subscriptions_stripe_customer_idx
    on public.organization_subscriptions (stripe_customer_id);

-- One row per provider event already applied. The unique index is the idempotency guarantee:
-- a repeated delivery cannot be applied twice even if two deliveries race.
create table if not exists public.billing_events (
    id uuid primary key default gen_random_uuid(),
    event_id text not null unique,
    event_type text not null,
    received_at timestamptz not null default now()
);

-- No policies: provider events are written by the API's service role and belong to no
-- organization, so a signed-in client can never read them.
alter table public.billing_events enable row level security;

grant all privileges on public.billing_events to service_role;
