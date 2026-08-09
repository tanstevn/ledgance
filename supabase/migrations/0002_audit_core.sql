-- Audit Core MVP schema: clients, engagements and engagement-scoped records, plus the
-- platform activity log and member profile columns used for team display.

alter table public.organization_members
    add column if not exists display_name text not null default '',
    add column if not exists email text not null default '';

create table if not exists public.activity_log (
    id uuid primary key default gen_random_uuid(),
    organization_id uuid not null references public.organizations (id) on delete cascade,
    module text not null,
    action text not null,
    subject_type text not null,
    subject_id uuid not null,
    summary text not null,
    engagement_id uuid,
    actor_user_id uuid not null,
    actor_email text not null default '',
    occurred_at timestamptz not null default now()
);

create index if not exists activity_log_org_occurred_idx
    on public.activity_log (organization_id, occurred_at desc);
create index if not exists activity_log_engagement_idx
    on public.activity_log (engagement_id) where engagement_id is not null;

create table if not exists public.audit_clients (
    id uuid primary key default gen_random_uuid(),
    organization_id uuid not null references public.organizations (id) on delete cascade,
    name text not null,
    industry text not null default '',
    contact_name text not null default '',
    contact_email text not null default '',
    contact_phone text not null default '',
    website text,
    address text,
    is_archived boolean not null default false,
    created_at timestamptz not null default now()
);

create index if not exists audit_clients_org_idx on public.audit_clients (organization_id);

create table if not exists public.audit_engagements (
    id uuid primary key default gen_random_uuid(),
    organization_id uuid not null references public.organizations (id) on delete cascade,
    client_id uuid not null references public.audit_clients (id),
    name text not null,
    type text not null,
    status text not null default 'Planning',
    period_start date not null,
    period_end date not null,
    fiscal_year_end date,
    budget_hours numeric(10,2) not null default 0,
    created_by uuid not null,
    created_at timestamptz not null default now(),
    plan jsonb,
    materiality jsonb
);

create index if not exists audit_engagements_org_idx on public.audit_engagements (organization_id);
create index if not exists audit_engagements_client_idx on public.audit_engagements (client_id);

create table if not exists public.audit_engagement_members (
    id uuid primary key default gen_random_uuid(),
    organization_id uuid not null references public.organizations (id) on delete cascade,
    engagement_id uuid not null references public.audit_engagements (id) on delete cascade,
    user_id uuid not null,
    role text not null check (role in ('Staff', 'Senior', 'Manager', 'Partner')),
    assigned_at timestamptz not null default now(),
    unique (engagement_id, user_id)
);

create index if not exists audit_engagement_members_user_idx
    on public.audit_engagement_members (user_id);

create table if not exists public.audit_risks (
    id uuid primary key default gen_random_uuid(),
    organization_id uuid not null references public.organizations (id) on delete cascade,
    engagement_id uuid not null references public.audit_engagements (id) on delete cascade,
    title text not null,
    description text not null default '',
    assertions text not null default '',
    likelihood int not null check (likelihood between 1 and 3),
    impact int not null check (impact between 1 and 3),
    planned_response text not null default ''
);

create index if not exists audit_risks_engagement_idx on public.audit_risks (engagement_id);

create table if not exists public.audit_procedures (
    id uuid primary key default gen_random_uuid(),
    organization_id uuid not null references public.organizations (id) on delete cascade,
    engagement_id uuid not null references public.audit_engagements (id) on delete cascade,
    area text not null default '',
    title text not null,
    description text not null default '',
    risk_ids jsonb not null default '[]'::jsonb,
    assignee_user_id uuid,
    status text not null default 'Planned',
    conclusion text,
    completed_at timestamptz
);

create index if not exists audit_procedures_engagement_idx
    on public.audit_procedures (engagement_id);

create table if not exists public.audit_working_papers (
    id uuid primary key default gen_random_uuid(),
    organization_id uuid not null references public.organizations (id) on delete cascade,
    engagement_id uuid not null references public.audit_engagements (id) on delete cascade,
    reference text not null,
    title text not null,
    content text not null default '',
    status text not null default 'Draft',
    prepared_by uuid,
    prepared_at timestamptz,
    reviewed_by uuid,
    reviewed_at timestamptz,
    approved_by uuid,
    approved_at timestamptz,
    notes jsonb not null default '[]'::jsonb,
    unique (engagement_id, reference)
);

create index if not exists audit_working_papers_engagement_idx
    on public.audit_working_papers (engagement_id);

create table if not exists public.audit_evidence (
    id uuid primary key default gen_random_uuid(),
    organization_id uuid not null references public.organizations (id) on delete cascade,
    engagement_id uuid not null references public.audit_engagements (id) on delete cascade,
    working_paper_id uuid,
    procedure_id uuid,
    file_name text not null,
    content_type text not null default '',
    size_bytes bigint not null,
    storage_path text not null,
    version int not null default 1,
    description text not null default '',
    uploaded_by uuid not null,
    uploaded_at timestamptz not null default now()
);

create index if not exists audit_evidence_engagement_idx
    on public.audit_evidence (engagement_id);

create table if not exists public.audit_findings (
    id uuid primary key default gen_random_uuid(),
    organization_id uuid not null references public.organizations (id) on delete cascade,
    engagement_id uuid not null references public.audit_engagements (id) on delete cascade,
    title text not null,
    description text not null,
    severity text not null check (severity in ('Low', 'Medium', 'High', 'Critical')),
    status text not null default 'Open'
        check (status in ('Open', 'Resolved', 'RiskAccepted', 'Closed')),
    recommendation text not null default '',
    resolution text,
    evidence_ids jsonb not null default '[]'::jsonb,
    raised_by uuid not null,
    raised_at timestamptz not null default now()
);

create index if not exists audit_findings_engagement_idx
    on public.audit_findings (engagement_id);

create table if not exists public.audit_reports (
    id uuid primary key default gen_random_uuid(),
    organization_id uuid not null references public.organizations (id) on delete cascade,
    engagement_id uuid not null unique references public.audit_engagements (id) on delete cascade,
    opinion text not null default 'Unqualified',
    basis_for_opinion text not null default '',
    key_audit_matters text not null default '',
    other_information text not null default '',
    is_finalized boolean not null default false,
    finalized_by uuid,
    finalized_at timestamptz,
    updated_at timestamptz not null default now()
);

create table if not exists public.audit_trial_balances (
    id uuid primary key default gen_random_uuid(),
    organization_id uuid not null references public.organizations (id) on delete cascade,
    engagement_id uuid not null references public.audit_engagements (id) on delete cascade,
    source text not null,
    period_label text not null,
    lines jsonb not null default '[]'::jsonb,
    total_debits numeric(18,2) not null default 0,
    total_credits numeric(18,2) not null default 0,
    imported_by uuid not null,
    imported_at timestamptz not null default now()
);

create index if not exists audit_trial_balances_engagement_idx
    on public.audit_trial_balances (engagement_id);

-- Private bucket for audit evidence files; the API accesses it with the service-role key and
-- issues short-lived signed URLs for downloads.
insert into storage.buckets (id, name, public)
values ('audit-evidence', 'audit-evidence', false)
on conflict (id) do nothing;

-- Row-level security: end users get organization-scoped read access as the backstop; all writes
-- go through the API's service-role client, which enforces tenancy in code.
do $$
declare
    audit_table text;
begin
    foreach audit_table in array array[
        'activity_log', 'audit_clients', 'audit_engagements', 'audit_engagement_members',
        'audit_risks', 'audit_procedures', 'audit_working_papers', 'audit_evidence',
        'audit_findings', 'audit_reports', 'audit_trial_balances'
    ] loop
        execute format('alter table public.%I enable row level security', audit_table);
        execute format(
            'create policy %I on public.%I for select to authenticated
             using (public.is_organization_member(organization_id))',
            audit_table || '_read_own', audit_table);
    end loop;
end $$;
