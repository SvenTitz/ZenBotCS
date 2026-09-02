# CLAUDE.md

Guidance for AI assistants working in this repository. See `README.md` for the
human-facing overview.

## What this is

A single-server Discord bot (.NET 8, C#) for Clash of Clans clan management:
war/CWL stats, CWL signup & roster wizard, account linking, reminders, and
leadership notes. Two projects:

- `ZenBotCS` — host/composition root, Discord modules, services, background
  workers, EF migrations.
- `ZenBotCS.Entities` — `BotDataContext`, entity models, enums, ClashKing DTOs.

The CoC game data comes from the **CocApi** library (`devhl-labs/CocApi`) and its
EF-Core cache — a non-obvious dependency with its own database and event model.
Before touching anything clan/player/war-related, read
[`docs/cocapi-integration.md`](docs/cocapi-integration.md).

## Build / run / migrate

```bash
dotnet build
dotnet run --project ZenBotCS
dotnet ef database update --project ZenBotCS --context BotDataContext
dotnet ef database update --project ZenBotCS --context CacheDbContext
dotnet ef migrations add <Name> --project ZenBotCS --context BotDataContext --output-dir Migrations/BotDb
dotnet test
```

There is **no CI**. `ZenBotCS.Tests` (xUnit) covers the pure, decision-making
helpers — schedules, windows, parsing, diffing — not the Discord or CocApi
surface. Verify changes by building and running `dotnet test`; anything that
talks to Discord or the CoC API still has to be checked by hand.

## Docker

The repo includes a `Dockerfile` (multi-stage) and `docker-compose.yml` for
production-like local runs and deployment.

```bash
docker compose up -d
```

### Services in compose

| Service      | Image / target            | Notes |
|-------------|---------------------------|-------|
| `mysql`     | `mysql:8.0`               | Root password via `MYSQL_ROOT_PASSWORD` env var. Runs `init.sql` on first start to create both databases. |
| `zenbot`    | `Dockerfile` target `bot` | The Discord bot host (`.NET 8 runtime`). Mounts `appsettings.json`, `gspread.json`, `gspreadOAuth2.json` read-only from the host. |
| `zenbot-web`| `Dockerfile` target `web` | ASP.NET 8 web app. Needs `libfontconfig1` (SkiaSharp). Mounts its own `appsettings.json`. |
| `caddy`     | `caddy:2`                 | Reverse proxy for `zenbot-web`, configured via `Caddyfile`. Auto-TLS on ports 80/443. |

### Dockerfile stages

- **`build`** — SDK image, restores and publishes both `ZenBotCS` and `ZenBotCS.Web`.
- **`bot`** — runtime image for the bot, entrypoint `dotnet ZenBotCS.dll`.
- **`web`** — ASP.NET image, installs `libfontconfig1`, entrypoint `dotnet ZenBotCS.Web.dll`.

### Config in Docker

The config files (`appsettings.json`, `gspread.json`, `gspreadOAuth2.json`) are
git-ignored, volume-mounted into the containers, and **excluded from the Docker
build context by `.dockerignore`** — they are never baked into the image.
Connection strings in `appsettings.json` should point to `Server=mysql` (the
compose service name).

### Database init

`init.sql` creates the two MySQL databases (`BotDb` + `CocApiCache`) on first
container start. EF migrations are applied at startup by `Program.cs`
(`MigrateAsync()`), so no manual `dotnet ef database update` is needed.

## Layering (respect it)

```
Handler/InteractionHandler  →  Modules/*  →  Services/SlashCommands/*  →  Clients/ + Helper/ + Entities (EF)
```

- **Modules** are thin: `await DeferAsync()` → call a service → `await FollowupAsync(embed: …)`.
  Don't put business logic in modules; put it in the matching service.
- **Services/SlashCommands** return `Discord.Embed` (or tuples with
  `MessageComponent`). Use `EmbedHelper` for tables/embeds; use `ErrorEmbed(...)`
  for user-facing errors.
- **Background services** (`Services/Background/`) own their own DI scope via
  `IServiceScopeFactory.CreateScope()` and loop with `Task.Delay`.

## Conventions

- `appsettings.json`, `gspread.json`, `gspreadOAuth2.json` are **git-ignored** —
  never commit secrets or hardcode tokens. Read config via
  `builder.Configuration["Key"]`.
- Services are registered in `Program.cs` (`AddTransient`). Background workers are
  `AddHostedService`. `BotDataContext` and the CoC `CacheDbContext` are scoped via
  `AddDbContext` / `AddCocApiCache`.
- Two MySQL databases: `BotDbConnectionString` and `CocApiCacheConnectionString`.
- Match the surrounding style (file-scoped namespaces in newer files, primary
  constructors with manually-assigned `_fields`). Note: existing identifiers
  contain typos (`chachedPlayers`, `timespampStart`, `Singup…`, `Acticity`) — match
  the existing name when editing that code; don't mass-rename.

## Gotchas (read before changing infra)

- **`DbContext` is scoped per interaction — keep it that way.** `InteractionHandler`
  opens a DI scope per interaction and the InteractionService runs in `RunMode.Sync`
  so the scoped `BotDataContext` lives for the whole command. Don't resolve
  `BotDataContext` from the root provider, and don't switch to `RunMode.Async`
  without making the scope outlive execution — either reintroduces a single shared,
  non-thread-safe context.
- **Background services must catch inside their loop.** An unhandled exception in a
  `BackgroundService` stops the whole app (.NET 8 default = `StopHost`). The three
  update services wrap each cycle in try/catch and log-and-continue; keep that
  pattern when adding workers (`DiscordLinkUpdateService` is the simplest model).
- **`ClashKingApiClient` is a singleton** reusing one `RestClient`/`HttpClient`.
  It's stateless and thread-safe — don't give it per-request mutable state or make
  it depend on scoped services.
- **Commands are registered globally only** (`RegisterCommandsGloballyAsync` in
  `InteractionHandler`). Don't re-add `RegisterCommandsToGuildAsync` — registering
  both bulk-writes the same set twice and duplicates every command in that guild.
- **A roster is not a clan.** A clan can split its CWL signups into sub-rosters that
  play in *other* clans (event/partner only, one host clan per sub-roster). A signup's
  `ClanTag` is where it belongs; `SubRosterId` (null = the clan's main roster) decides
  where it plays. Anything asking "who plays in clan X" must go through
  `CwlRosterSource.RosterFor` — a bare `Where(s => s.ClanTag == tag)` silently returns the
  wrong players for a host clan. Changing a signup's `ClanTag` must clear `SubRosterId`.

- **ClashKing is on the v2 API.** Base is still `https://api.clashk.ing`, but the endpoints the bot
  uses are `POST /v2/links/shared`, `GET /v2/clan/{tag}/wars` and `GET /v2/player/{tag}/war/stats`.
  The v1 paths (`/discord_links`, `/player/{tag}/stats`, `/list/seasons`, `/player/{tag}/legends`)
  are gone — 404, not deprecated, with no v2 replacement for the player-stats payload. The features
  that depended on it were removed rather than ported: `/player stats data`, `/clan stats activity`,
  and the Legend League block in `/player to-do`. **Don't try to bring them back** — `last_online`,
  the per-season activity score, `looted`, per-season `attack_wins` and the day-by-day legend
  attacks have no v2 equivalent at all, and `/v2/player/{tag}/history/stats` (the nominal
  replacement for donations/clan games/capital) returned empty for every player tested.
  Reviving `/clan stats activity` means deriving last-seen ourselves from the CocApi cache.
  Timestamps are unchanged (`yyyyMMddTHHmmss.fffZ`), but war filtering moved from unix
  `timestamp_start`/`timestamp_end` to ISO-8601 `time[after]`/`time[before]`, whose brackets must be
  percent-encoded, and `limit` is capped at 500.

- **Never call ClashKing's link endpoint directly.** It goes down regularly, and a failed lookup
  used to be indistinguishable from "this player is unlinked" — which silently broke CWL signups.
  `ClashKingApiClient.PostDiscordLinksAsync` returns `null` when nothing could be asked at all
  (a `null` *value* for a tag still means genuinely unlinked, and a tag missing from the result was
  in a batch that failed), and `Services/DiscordLinkSource` is the only thing that should read it:
  it falls back to the bot's own `DiscordLinks` table whenever the API has no answer, and mirrors
  successful lookups back into that table. `ZenBotCS.Web` does the same fallback inline in
  `RosterService.AddSignupAsync`. The table can be stale (an upstream unlink still resolves to the
  old user) — that is the accepted trade. `/links add|remove|lookup` edit and inspect that table by
  hand for when the backup itself is missing an account; note that `/links update` mirrors the API
  verbatim and will overwrite manual edits on its next run.

- **`/v2/links/shared` needs a developer token and takes at most 100 tags.** The token comes from
  config `CkApiToken` (`ck_dev_…`); without it the endpoint 401s while the public war endpoints keep
  working. `PostDiscordLinksAsync` chunks tags into 100s and treats a chunk's failure as "unanswered"
  rather than "unlinked". It also drops tags outside the base-14 CoC alphabet before sending, because
  one malformed tag makes the API reject the whole batch with a 400.

- `CwlService` is ~1,400 lines; when adding to it, prefer extracting a focused
  helper over growing it further.
