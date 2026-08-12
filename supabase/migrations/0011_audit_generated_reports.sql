-- Phase 9.5 — AI-generated audit reports.
--
-- A generated report is a draft, not an audit report: it lives in its own table, is never
-- written into audit_reports, and carries the review state that decides whether a professional
-- has accepted it as a working basis. Provider and model are recorded so a reviewer can see
-- what produced what; nothing here stores a credential.

create table if not exists public.audit_generated_reports (
    id uuid primary key default gen_random_uuid(),
    organization_id uuid not null references public.organizations (id) on delete cascade,
    engagement_id uuid not null references public.audit_engagements (id) on delete cascade,
    capability text not null,
    report_scope text not null,
    title text not null default '',
    status text not null default 'Draft'
        check (status in ('Draft', 'Accepted', 'Rejected')),
    provider text not null default '',
    model text not null default '',
    sections jsonb not null default '[]'::jsonb,
    generated_by uuid not null,
    generated_at timestamptz not null default now(),
    reviewed_by uuid,
    reviewed_at timestamptz,
    review_note text
);

create index if not exists audit_generated_reports_engagement_idx
    on public.audit_generated_reports (engagement_id, generated_at desc);

-- Same posture as every other Audit table: organization-scoped read for signed-in users as the
-- backstop, all writes through the API's service-role client, which enforces tenancy in code.
alter table public.audit_generated_reports enable row level security;

create policy audit_generated_reports_read_own on public.audit_generated_reports
    for select to authenticated
    using (public.is_organization_member(organization_id));

-- Audit plan codes were restructured in Phase 9.5 (Professional/Organization/Firm became
-- Micro/Micro-Growth/Small/Medium/Medium-Growth). A row still naming a retired plan resolves
-- to Free in code; this maps the closest equivalent so paying organizations keep their
-- capacity until the next provider event confirms the row.
update public.organization_subscriptions
   set plan = case plan
       when 'AuditProfessional' then 'AuditMicroGrowth'
       when 'AuditOrganization' then 'AuditSmall'
       when 'AuditFirm' then 'AuditMedium'
       else plan
   end
 where module = 'Audit'
   and plan in ('AuditProfessional', 'AuditOrganization', 'AuditFirm');
