-- Evidence grows classification and a retained version history. Superseding used to
-- overwrite the row's file pointer in place, so nothing could show what version 1 said;
-- prior versions now live in version_history and stay downloadable.

alter table public.audit_evidence
    add column if not exists category text not null default 'Evidence'
        check (category in ('Evidence', 'Financial', 'Correspondence', 'Supporting')),
    add column if not exists tags jsonb not null default '[]'::jsonb,
    add column if not exists version_history jsonb not null default '[]'::jsonb;

create index if not exists audit_evidence_engagement_file_idx
    on public.audit_evidence (engagement_id, file_name);
