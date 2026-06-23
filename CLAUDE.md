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
- `CwlService` is ~1,400 lines; when adding to it, prefer extracting a focused
  helper over growing it further.
