-- Stamps the user's organization membership into the access token as org_id / org_role.
-- CurrentUserMiddleware already prefers these claims and only falls back to a database
-- lookup when they are absent, so enabling this hook removes one Postgres round trip from
-- every authenticated API request.
--
-- Creating the function does nothing by itself: it must be selected in the Supabase
-- dashboard under Authentication -> Hooks -> "Customize Access Token (JWT) Claims".
--
-- Trade-off, accepted deliberately: claims live for the token's lifetime (1 hour by
-- default), so a role change or membership removal takes effect on the next token refresh
-- rather than instantly. Rows themselves stay protected regardless — the repository's
-- organization filter and row-level security both key off the id, which cannot be forged.

create or replace function public.custom_access_token_hook(event jsonb)
returns jsonb
language plpgsql
stable
security definer
set search_path = public
as $$
declare
    claims jsonb;
    membership record;
begin
    -- Same selection rule as the API's OrganizationMembershipReader, so the claim and the
    -- fallback lookup can never disagree about which membership wins.
    select m.organization_id, m.role
        into membership
        from public.organization_members m
        where m.user_id = (event->>'user_id')::uuid
        order by m.is_default desc, m.created_at asc
        limit 1;

    claims := event->'claims';

    if membership is not null then
        claims := jsonb_set(claims, '{org_id}', to_jsonb(membership.organization_id::text));
        claims := jsonb_set(claims, '{org_role}', to_jsonb(membership.role));
    end if;

    return jsonb_set(event, '{claims}', claims);
end;
$$;

-- Only the auth server may call the hook; it must never be reachable through the Data API.
grant execute on function public.custom_access_token_hook to supabase_auth_admin;
revoke execute on function public.custom_access_token_hook from authenticated, anon, public;
grant select on table public.organization_members to supabase_auth_admin;
