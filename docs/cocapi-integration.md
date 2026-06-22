# CocApi integration

How ZenBotCS uses **[devhl-labs/CocApi](https://github.com/devhl-labs/CocApi)** —
a .NET wrapper for the Clash of Clans API with an optional EF Core caching layer.
Docs live in the [project wiki](https://github.com/devhl-labs/CocApi/wiki).

NuGet packages (all `2.14.2-net8`): `CocApi`, `CocApi.Rest`, `CocApi.Cache`.

## Mental model

CocApi runs its own **background services** that periodically poll Supercell's API
on a time-to-live (TTL) schedule, persist the responses to a cache database, and
raise **events** when data changes (new attacks, war ended, members joined/left).
Your code reads the local cache instead of calling the rate-limited API directly.

```
Supercell CoC API
      │  (CocApi background services poll on a TTL)
      ▼
CacheDbContext  ──────────►  events: ClanWarUpdated, ClanWarEnded, …
 (separate MySQL DB)
      ▲
      │  query cache / GetOrFetch
ZenBotCS services & autocomplete
```

## Two databases, two contexts

The CoC cache uses its own `CacheDbContext` on a **separate** MySQL database
(`CocApiCacheConnectionString`), completely distinct from the bot's own
`BotDataContext` (`BotDbConnectionString`). Don't mix them.

- Cache migrations: `Migrations/CocApiCache`
- Bot migrations: `Migrations/BotDb`

Registered in `Program.cs`:

```csharp
builder.Services.AddCocApiCache<CustomClansClient, PlayersClient, TimeToLiveProvider>(
    dbContextOptions => { /* CocApiCacheConnectionString */ },
    options => { options.Clans.Enabled = true; /* ActiveWars, ClanMembers,
        CwlWars, NewCwlWars, NewWars, Players, Wars … */ });
```

`TimeToLiveProvider` here is **CocApi's built-in default** — the TTL is not
customized. The custom `CustomClansClient` (subclass of `ClansClient`) only adds
event handlers and a cached-clans query helper.

## Tracking clans — the `Download` flag

A clan is only kept fresh if it is *tracked*. Tracking is toggled by the
`Download` flag on the cache row:

```csharp
// Start tracking (ClanService.Add)
await _clansClient.AddOrUpdateAsync(clanTag, downloadMembers: true);

// Stop tracking (ClanService.Delete)
await _clansClient.DeleteAsync(clanTag);
```

> **"The family clans" = every cache row with `Download == true`.** That is the
> definition used throughout the bot.

Note: `ClanMemberService` stores members with `Download = false` by default.

## Reading cached data

The canonical "all clans / all players" lists (used by autocomplete handlers and
the background workers) come from querying `CacheDbContext` directly — **not** the
live API:

```csharp
// CustomClansClient.GetCachedClansAsync()
Clans.AsNoTracking().Where(i => i.Download).Select(i => i.Content)

// PlayersClientExtension.GetCachedPlayersAsync()
Players.AsNoTracking().Where(p => p.RawContent != null).Select(p => p.Content)
```

The clients expose `ScopeFactory` and `Logger` publicly; open a scope from
`ScopeFactory` to resolve a `CacheDbContext`.

## Cache-first fetch (`GetOrFetch`)

When you need a single entity and don't care whether it's already cached, use the
cache-first helpers — they read the cache and fall back to the live API on a miss:

- `GetOrFetchClanAsync(tag)`
- `GetOrFetchPlayerAsync(tag)`
- `PlayersClientExtension.GetOrFetchPlayersAsync(tags)` — batches many tags with
  per-tag error handling.

## Events

`CustomClansClient` subscribes to clan-war events in its constructor:

| Event | Handler | Effect |
|-------|---------|--------|
| `ClanWarEnded` | `OnClanWarEnded` | Calls `ReminderService.PostMissedAttacksReminderForWar(e)` — **this is how missed-attack reminders fire** (not a polling loop in this repo). |
| `ClanWarUpdated` | `OnClanWarUpdated` | Currently a no-op (logging commented out). |

Useful diff helpers from the library when handling events:
`ClanWar.NewAttacks(stored, fetched)`, `Clan.ClanMembersJoined()`,
`Clan.ClanMembersLeft()`.

## Gotchas

- The cache database **needs its EF migrations applied before first run**
  (`dotnet ef database update --context CacheDbContext`).
- The CoC API token (`CocApiToken`) is **IP-locked** and rate-limited per token;
  tokens are registered in `Program.cs` via `AddTokens`.
- Reading from the cache returns whatever the last successful poll stored — it can
  lag the live game by up to the TTL. Use `GetOrFetch…` when you need freshness.
