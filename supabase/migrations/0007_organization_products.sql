-- Which Ledgance products an organization has activated. Set from the platform chosen at
-- signup; a paid subscription for a module also enables it regardless of this list. The
-- default keeps organizations created before this migration working with both products.

alter table public.organizations
    add column if not exists products text[] not null default array['Audit', 'Accounting'];
