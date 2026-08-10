-- Accounting Core MVP schema: entities (books), chart of accounts, fiscal periods, journal
-- entries with append-only ledger lines, reconciliations and source documents. Also
-- generalizes the activity log's engagement scope column to a product-neutral context id.

alter table public.activity_log
    rename column engagement_id to context_id;

drop index if exists public.activity_log_engagement_idx;
create index if not exists activity_log_context_idx
    on public.activity_log (context_id) where context_id is not null;

create table if not exists public.accounting_entities (
    id uuid primary key default gen_random_uuid(),
    organization_id uuid not null references public.organizations (id) on delete cascade,
    name text not null,
    legal_name text not null default '',
    base_currency text not null check (char_length(base_currency) = 3),
    is_archived boolean not null default false,
    created_at timestamptz not null default now()
);

create index if not exists accounting_entities_org_idx
    on public.accounting_entities (organization_id);

create table if not exists public.accounting_accounts (
    id uuid primary key default gen_random_uuid(),
    organization_id uuid not null references public.organizations (id) on delete cascade,
    entity_id uuid not null references public.accounting_entities (id) on delete cascade,
    code text not null,
    name text not null,
    type text not null check (type in ('Asset', 'Liability', 'Equity', 'Revenue', 'Expense')),
    classification text not null default '',
    parent_account_id uuid references public.accounting_accounts (id),
    is_active boolean not null default true,
    created_at timestamptz not null default now(),
    unique (entity_id, code)
);

create index if not exists accounting_accounts_entity_idx
    on public.accounting_accounts (entity_id);
create index if not exists accounting_accounts_parent_idx
    on public.accounting_accounts (parent_account_id) where parent_account_id is not null;

create table if not exists public.accounting_fiscal_periods (
    id uuid primary key default gen_random_uuid(),
    organization_id uuid not null references public.organizations (id) on delete cascade,
    entity_id uuid not null references public.accounting_entities (id) on delete cascade,
    name text not null,
    start_date date not null,
    end_date date not null check (end_date >= start_date),
    status text not null default 'Open' check (status in ('Open', 'Closed')),
    closed_by uuid,
    closed_at timestamptz,
    created_at timestamptz not null default now()
);

create index if not exists accounting_fiscal_periods_entity_idx
    on public.accounting_fiscal_periods (entity_id, start_date);

create table if not exists public.accounting_journal_entries (
    id uuid primary key default gen_random_uuid(),
    organization_id uuid not null references public.organizations (id) on delete cascade,
    entity_id uuid not null references public.accounting_entities (id) on delete cascade,
    entry_number bigint not null,
    entry_date date not null,
    memo text not null,
    reference text not null default '',
    status text not null default 'Draft' check (status in ('Draft', 'Posted', 'Reversed')),
    lines jsonb not null default '[]'::jsonb,
    reversal_of_entry_id uuid references public.accounting_journal_entries (id),
    reversed_by_entry_id uuid references public.accounting_journal_entries (id),
    created_by uuid not null,
    created_at timestamptz not null default now(),
    posted_by uuid,
    posted_at timestamptz,
    unique (entity_id, entry_number)
);

create index if not exists accounting_journal_entries_entity_date_idx
    on public.accounting_journal_entries (entity_id, entry_date);
create index if not exists accounting_journal_entries_entity_status_idx
    on public.accounting_journal_entries (entity_id, status);

-- Ledger lines are materialized at posting time and are append-only: amounts only ever change
-- through a reversing entry that adds its own lines.
create table if not exists public.accounting_ledger_lines (
    id uuid primary key default gen_random_uuid(),
    organization_id uuid not null references public.organizations (id) on delete cascade,
    entity_id uuid not null references public.accounting_entities (id) on delete cascade,
    entry_id uuid not null references public.accounting_journal_entries (id),
    entry_number bigint not null,
    entry_date date not null,
    account_id uuid not null references public.accounting_accounts (id),
    description text not null default '',
    debit numeric(18,2) not null default 0,
    credit numeric(18,2) not null default 0,
    check (debit >= 0 and credit >= 0),
    check ((debit > 0) <> (credit > 0))
);

create index if not exists accounting_ledger_lines_account_date_idx
    on public.accounting_ledger_lines (account_id, entry_date);
create index if not exists accounting_ledger_lines_entity_date_idx
    on public.accounting_ledger_lines (entity_id, entry_date);

create table if not exists public.accounting_reconciliations (
    id uuid primary key default gen_random_uuid(),
    organization_id uuid not null references public.organizations (id) on delete cascade,
    entity_id uuid not null references public.accounting_entities (id) on delete cascade,
    account_id uuid not null references public.accounting_accounts (id),
    statement_date date not null,
    statement_balance numeric(18,2) not null,
    status text not null default 'InProgress'
        check (status in ('InProgress', 'Completed', 'Cancelled')),
    cleared_line_ids jsonb not null default '[]'::jsonb,
    cleared_balance numeric(18,2),
    difference numeric(18,2),
    explanation text,
    started_by uuid not null,
    started_at timestamptz not null default now(),
    completed_by uuid,
    completed_at timestamptz
);

create index if not exists accounting_reconciliations_entity_idx
    on public.accounting_reconciliations (entity_id);
create index if not exists accounting_reconciliations_account_idx
    on public.accounting_reconciliations (account_id);

create table if not exists public.accounting_documents (
    id uuid primary key default gen_random_uuid(),
    organization_id uuid not null references public.organizations (id) on delete cascade,
    entity_id uuid not null references public.accounting_entities (id) on delete cascade,
    journal_entry_id uuid references public.accounting_journal_entries (id),
    reconciliation_id uuid references public.accounting_reconciliations (id),
    file_name text not null,
    content_type text not null default '',
    size_bytes bigint not null default 0,
    storage_path text not null,
    description text not null default '',
    uploaded_by uuid not null,
    uploaded_at timestamptz not null default now()
);

create index if not exists accounting_documents_entity_idx
    on public.accounting_documents (entity_id);

-- Private bucket for accounting source documents; the API accesses it with the service-role
-- key and issues short-lived signed URLs for downloads.
insert into storage.buckets (id, name, public)
values ('accounting-documents', 'accounting-documents', false)
on conflict (id) do nothing;

-- Row-level security: end users get organization-scoped read access as the backstop; all
-- writes go through the API's service-role client, which enforces tenancy in code.
do $$
declare
    accounting_table text;
begin
    foreach accounting_table in array array[
        'accounting_entities', 'accounting_accounts', 'accounting_fiscal_periods',
        'accounting_journal_entries', 'accounting_ledger_lines',
        'accounting_reconciliations', 'accounting_documents'
    ] loop
        execute format('alter table public.%I enable row level security', accounting_table);
        execute format(
            'create policy %I on public.%I for select to authenticated
             using (public.is_organization_member(organization_id))',
            accounting_table || '_read_own', accounting_table);
    end loop;
end $$;
