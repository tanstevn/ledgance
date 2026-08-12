-- Phase 9.5.1 — AI consumption accounting.
--
-- Two pieces: `ai_usage` stays the per-period counter (it is what a limit check reads and the
-- only row that has to be locked), and `ai_usage_events` becomes the attribution ledger — who
-- spent what, on which capability, against which engagement. Both are written by one function
-- so the counter and the ledger cannot drift apart.

create table if not exists public.ai_usage_events (
    id uuid primary key default gen_random_uuid(),
    organization_id uuid not null references public.organizations (id) on delete cascade,
    user_id uuid not null,
    module text not null check (module in ('Audit', 'Accounting')),
    period text not null,
    capability text not null,
    units bigint not null check (units >= 0),
    client_id uuid,
    engagement_id uuid,
    occurred_at timestamptz not null default now()
);

create index if not exists ai_usage_events_org_period_idx
    on public.ai_usage_events (organization_id, module, period, occurred_at desc);

create index if not exists ai_usage_events_engagement_idx
    on public.ai_usage_events (engagement_id)
    where engagement_id is not null;

alter table public.ai_usage_events enable row level security;

create policy ai_usage_events_read_own
    on public.ai_usage_events for select
    to authenticated
    using (public.is_organization_member(organization_id));

-- Takes units from an organization's allowance and records what they were spent on, or takes
-- nothing at all. `for update` serialises concurrent callers on the counter row, which is what
-- stops two simultaneous requests from both spending the same remaining credits. Returning no
-- row means the allowance would have been exceeded; the caller refuses the operation.
--
-- p_limit is the resolved entitlement, passed in because entitlement resolution (catalogue →
-- configuration → per-organization override) lives in the application. -1 is unlimited: the
-- spend is still recorded, it just cannot be refused.
create or replace function public.consume_ai_units(
    p_organization_id uuid,
    p_module text,
    p_period text,
    p_units bigint,
    p_limit bigint,
    p_user_id uuid,
    p_capability text,
    p_client_id uuid default null,
    p_engagement_id uuid default null
)
-- The OUT columns are deliberately not named after ai_usage columns: plpgsql substitutes
-- output names into SQL and an 'units_used' output would make every column reference
-- ambiguous.
returns table (event_id uuid, total_units bigint)
language plpgsql
as $$
declare
    v_used bigint;
    v_event uuid;
begin
    insert into public.ai_usage (organization_id, module, period, units_used, updated_at)
    values (p_organization_id, p_module, p_period, 0, now())
    on conflict (organization_id, module, period) do nothing;

    select ai_usage.units_used into v_used
      from public.ai_usage
     where ai_usage.organization_id = p_organization_id
       and ai_usage.module = p_module
       and ai_usage.period = p_period
       for update;

    if p_limit <> -1 and v_used + p_units > p_limit then
        return;
    end if;

    update public.ai_usage
       set units_used = ai_usage.units_used + p_units,
           updated_at = now()
     where ai_usage.organization_id = p_organization_id
       and ai_usage.module = p_module
       and ai_usage.period = p_period
    returning ai_usage.units_used into v_used;

    insert into public.ai_usage_events (
        organization_id, user_id, module, period, capability, units, client_id, engagement_id)
    values (
        p_organization_id, p_user_id, p_module, p_period, p_capability, p_units,
        p_client_id, p_engagement_id)
    returning id into v_event;

    return query select v_event, v_used;
end;
$$;

-- Gives units back when the work never reached the provider. The event row is removed rather
-- than flagged, so the ledger means "usage actually consumed" and always sums to the counter.
-- Scoped by organization as well as event id: a released reservation can only ever be one this
-- organization made.
create or replace function public.release_ai_units(
    p_organization_id uuid,
    p_event_id uuid
)
returns void
language plpgsql
as $$
declare
    v_units bigint;
    v_module text;
    v_period text;
begin
    delete from public.ai_usage_events
     where id = p_event_id
       and organization_id = p_organization_id
    returning units, module, period into v_units, v_module, v_period;

    if v_units is null then
        return;
    end if;

    update public.ai_usage
       set units_used = greatest(0, ai_usage.units_used - v_units),
           updated_at = now()
     where ai_usage.organization_id = p_organization_id
       and ai_usage.module = v_module
       and ai_usage.period = v_period;
end;
$$;

-- Only the API's service-role client spends or releases usage. A signed-in user reads their
-- organization's totals through RLS and can never move them.
revoke all on function public.consume_ai_units(
    uuid, text, text, bigint, bigint, uuid, text, uuid, uuid) from public, anon, authenticated;
revoke all on function public.release_ai_units(uuid, uuid) from public, anon, authenticated;

grant execute on function public.consume_ai_units(
    uuid, text, text, bigint, bigint, uuid, text, uuid, uuid) to service_role;
grant execute on function public.release_ai_units(uuid, uuid) to service_role;
