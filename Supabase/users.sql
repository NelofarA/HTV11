create table if not exists public.users (
    id uuid primary key references auth.users (id) on delete cascade,
    name text not null,
    email text not null,
    onboarding_completed boolean not null default false
);

create table if not exists public.user_preferences (
    user_id uuid primary key references public.users (id) on delete cascade,
    interests text[] not null default '{}',
    age_group text,
    accessibility_needs text[] not null default '{}',
    travel_mode text,
    budget_preference text,
    visit_duration text,
    visit_date_start date,
    visit_date_end date,
    setting_preference text,
    activity_pace text
);

alter table public.user_preferences enable row level security;

drop policy if exists "Users can read their own preferences" on public.user_preferences;
create policy "Users can read their own preferences"
    on public.user_preferences for select
    to authenticated
    using ((select auth.uid()) = user_id);

drop policy if exists "Users can insert their own preferences" on public.user_preferences;
create policy "Users can insert their own preferences"
    on public.user_preferences for insert
    to authenticated
    with check ((select auth.uid()) = user_id);

drop policy if exists "Users can update their own preferences" on public.user_preferences;
create policy "Users can update their own preferences"
    on public.user_preferences for update
    to authenticated
    using ((select auth.uid()) = user_id)
    with check ((select auth.uid()) = user_id);

alter table public.users enable row level security;

drop policy if exists "Users can read their own row" on public.users;
create policy "Users can read their own row"
    on public.users for select
    to authenticated
    using ((select auth.uid()) = id);

drop policy if exists "Users can update their own row" on public.users;
create policy "Users can update their own row"
    on public.users for update
    to authenticated
    using ((select auth.uid()) = id)
    with check ((select auth.uid()) = id);

create or replace function public.handle_new_user()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
begin
    insert into public.users (id, name, email, onboarding_completed)
    values (
        new.id,
        coalesce(nullif(trim(new.raw_user_meta_data ->> 'name'), ''), new.email),
        new.email,
        false
    );
    return new;
end;
$$;

drop trigger if exists on_auth_user_created on auth.users;
create trigger on_auth_user_created
    after insert on auth.users
    for each row execute procedure public.handle_new_user();

insert into public.users (id, name, email, onboarding_completed)
select
    id,
    coalesce(nullif(trim(raw_user_meta_data ->> 'name'), ''), email),
    email,
    false
from auth.users
on conflict (id) do nothing;
