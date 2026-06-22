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

## Build / run / migrate

```bash
dotnet build
dotnet run --project ZenBotCS
dotnet ef database update --project ZenBotCS --context BotDataContext
dotnet ef database update --project ZenBotCS --context CacheDbContext
dotnet ef migrations add <Name> --project ZenBotCS --context BotDataContext --output-dir Migrations/BotDb
```

There is **no test project** and no CI. Verify changes by building; there is no
automated suite to run.

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

- **Per-interaction `DbContext` scoping is broken.** Interactions run against the
  **root** provider, so the scoped `BotDataContext` is effectively a singleton
  shared by all commands (not thread-safe). If you touch the interaction pipeline,
  prefer opening a scope per interaction. `Attributes/RequireOwnPlayerTag` shows
  the bug: it calls `services.CreateScope()` but then resolves from `services`, not
  `scope.ServiceProvider`.
- **Background services can crash the host.** An unhandled exception in a
  `BackgroundService` stops the whole app (.NET 8 default). `WarHistoryUpdateService`
  and `PlayerStatsUpdateService` currently lack try/catch; `DiscordLinkUpdateService`
  has the correct pattern.
- **`ClashKingApiClient` is transient and creates its own `RestClient`** — avoid
  resolving it in hot paths; prefer reuse.
- **Command registration is both guild-scoped (hardcoded guild id in
  `InteractionHandler`) and global** → duplicate commands in the home guild.
- `CwlService` is ~1,400 lines; when adding to it, prefer extracting a focused
  helper over growing it further.
