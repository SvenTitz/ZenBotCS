# ZenBotCS

A Discord bot for managing a Clash of Clans clan family. It wires together the
official **Clash of Clans API**, the community **ClashKing API**, and **Google
Sheets** to provide war/CWL statistics, Clan War League (CWL) signup and roster
management, Discord↔player account linking, reminders, and leadership notes
("gatekeeping").

The bot is built for a **single Discord server**.

---

## Tech stack

| Area            | Technology |
|-----------------|------------|
| Runtime         | .NET 8 (C#, nullable + implicit usings enabled) |
| Discord         | [Discord.Net](https://github.com/discord-net/Discord.Net) 3.17 + `Discord.Addons.Hosting` (Interaction framework) |
| Game data       | [CocApi](https://www.nuget.org/packages/CocApi) (`.Rest` + `.Cache`) |
| Extra stats     | [ClashKing API](https://api.clashk.ing) via RestSharp |
| Spreadsheets    | Google Sheets / Drive API |
| Database        | MySQL via EF Core 9 + Pomelo provider |
| Logging         | Serilog (console + rolling file in `ZenBotCS/logs/`) |

There are two projects:

- **`ZenBotCS`** – the bot host: composition root (`Program.cs`), Discord
  modules, services, background workers, helpers, and EF migrations.
- **`ZenBotCS.Entities`** – the EF `BotDataContext`, entity models, enums, and
  the ClashKing API DTOs (shared, dependency-light layer).

---

## Architecture

```
Discord interaction
        │
        ▼
InteractionHandler (Handler/)            ← receives gateway events, logs, dispatches
        │
        ▼
Modules/ (e.g. CwlModule, ClanModule)    ← thin command definitions; Defer + Followup
        │  inject
        ▼
Services/SlashCommands/ (CwlService, …)  ← business logic, returns Discord Embeds
        │  use
        ▼
Clients/ + Helper/ + Entities (EF)       ← CoC cache, ClashKing client, Google Sheets, MySQL
```

- **Modules** are intentionally thin: they `DeferAsync()`, call a service method,
  and `FollowupAsync(embed: …)`. Almost all real logic lives in the services.
- **Background services** (`Services/Background/`) run periodic sync loops:
  - `DiscordLinkUpdateService` – refreshes player↔Discord links (every 10 min)
  - `WarHistoryUpdateService` – pulls clan war history (every 15 min)
  - `PlayerStatsUpdateService` – pulls per-player stats & war hits
  - `LeadershipLogBackfillService` – backfills leadership-notes history
- **Two databases**: the bot's own data (`BotDbConnectionString`) and the CoC API
  cache (`CocApiCacheConnectionString`). Both are MySQL and both have their EF
  migrations stored in this repo (`Migrations/BotDb`, `Migrations/CocApiCache`).

---

## Commands (overview)

| Group | Commands |
|-------|----------|
| `/clan` | `add`, `delete`, `list`, `warlog`, `stats attacks`, `stats activity`, `settings edit`, `settings reset` |
| `/cwl` | `data`, `signup post/roster/pin-roster/missing/summary/check/delete/reset/dump/move/add`, `roles assign/remove` (+ button/menu component flows for the signup wizard) |
| `/player` | `to-do`, `stats misses`, `stats attacks`, `stats data` |
| `/links` | `list-unlinked`, `update` |
| `/reminder` | `misses add/remove/list` |
| `/gatekeep` | `notes lookup`, `notes post` |
| `/help` | `bots linking`, `bots gatekeeper`, `cwl signup` |
| `/util` | `ping`, `timestamp`, `spintimes` |

Admin-only commands use `[RequireUserPermission(GuildPermission.Administrator)]`.

---

## Getting started

### Prerequisites

- .NET 8 SDK
- A MySQL server (two schemas/databases — one for the bot, one for the CoC cache)
- A **Discord bot token** (with the *Message Content* and *Server Members*
  privileged intents enabled in the Discord developer portal)
- A **Clash of Clans API token** (https://developer.clashofclans.com) — note the
  token is IP-locked
- A **Google service account** (`gspread.json`) and **OAuth2 client**
  (`gspreadOAuth2.json`) with access to the Sheets & Drive APIs

### Configuration

Create `ZenBotCS/appsettings.json` (it is **git-ignored** and not committed).
Keys the bot reads:

| Key | Purpose |
|-----|---------|
| `DiscordToken` | Discord bot token |
| `CocApiToken` | Clash of Clans API token |
| `BotDbConnectionString` | MySQL connection string for the bot database |
| `CocApiCacheConnectionString` | MySQL connection string for the CoC cache database |
| `PathToGspreadCredentials` | Path to the Google service-account JSON |
| `PathToGspreadCredentialsOAuth2` | Path to the Google OAuth2 client JSON |
| `PathToGspreadToken` | Path where the OAuth2 token is cached |
| `CwlRosterTemplateSpreadsheetId` | Google Sheet template for CWL rosters |
| `CwlRosterChampStyleTemplateSpreadsheetId` | Template for "champ style" rosters |
| `FamilyLeadershipRoleId` / `WarGeneralRoleId` | Discord role IDs used for permission checks |
| `LeadershipNotesChannelId` | Channel for gatekeep/leadership notes |
| `HelpBotsLinking` / `HelpBotsGatekeeper` / `HelpCwlSignup` / `CwlSignupHelpButton` | Help text / button targets |
| `Serilog` | Logging configuration (console + rolling file) |

> The Google credential files (`gspread.json`, `gspreadOAuth2.json`) are copied to
> the output directory on build and are also git-ignored. Keep them out of source
> control.

### Build & run

```bash
dotnet restore
dotnet build
dotnet run --project ZenBotCS
```

### Database migrations

Migrations live in this repo for both contexts (`MigrationsAssembly("ZenBotCS")`).
Apply them with the EF Core tools (`dotnet tool install -g dotnet-ef`):

```bash
# Bot database
dotnet ef database update --project ZenBotCS --context BotDataContext

# CoC cache database
dotnet ef database update --project ZenBotCS --context CacheDbContext
```

To add a migration:

```bash
dotnet ef migrations add <Name> --project ZenBotCS \
  --context BotDataContext --output-dir Migrations/BotDb
```

---

## Known issues / improvement backlog

These are latent today (low concurrency hides them) but worth addressing:

1. **Interaction commands share a single `DbContext`.** `BotDataContext` is
   scoped, but interactions execute against the **root** service provider
   (`InteractionHandler.ExecuteCommandAsync`), so every command shares one
   instance. `DbContext` is not thread-safe — this risks concurrency exceptions
   and an ever-growing change tracker under load. Fix: open a DI scope per
   interaction. (`Attributes/RequireOwnPlayerTag` creates a scope but then
   resolves from the root provider, so the scope is a no-op.)
2. **Some background services have no exception guard.** In .NET 8 an unhandled
   exception in a `BackgroundService` stops the whole host. `WarHistoryUpdateService`
   and `PlayerStatsUpdateService` lack a try/catch around their loops
   (`DiscordLinkUpdateService` has one — use it as the model).
3. **`ClashKingApiClient` is transient and news up its own `RestClient`** →
   a new `HttpClient` per resolution. Prefer a singleton or `IHttpClientFactory`.
4. **Commands are registered both per-guild and globally**, producing duplicate
   entries in the home guild.
5. **`CwlService` is a ~1,400-line god class** mixing the signup state machine,
   spreadsheet formatting, roster generation, and role assignment — the main
   maintenance liability.

---

## License

See [LICENSE.txt](LICENSE.txt).
