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
| Extra stats     | [ClashKing API](https://api.clashk.ing) v2 via RestSharp |
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
  - `PlayerWarHitsUpdateService` – caches per-player war hits (every 15 min)
  - `LeadershipLogBackfillService` – backfills leadership-notes history
- **Two databases**: the bot's own data (`BotDbConnectionString`) and the CoC API
  cache (`CocApiCacheConnectionString`). Both are MySQL and both have their EF
  migrations stored in this repo (`Migrations/BotDb`, `Migrations/CocApiCache`).

---

## Commands (overview)

| Group | Commands |
|-------|----------|
| `/clan` | `add`, `delete`, `list`, `warlog`, `stats attacks`, `settings edit`, `settings reset` |
| `/cwl` | `data`, `signup post/roster/pin-roster/missing/summary/check/delete/reset/dump/move/add`, `roles assign/remove` (+ button/menu component flows for the signup wizard) |
| `/player` | `to-do`, `stats misses`, `stats attacks` |
| `/links` | `list-unlinked`, `update`, `lookup` |
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
- _Optional:_ a **Google OAuth2 client** (`gspreadOAuth2.json`) with access to the
  Sheets & Drive APIs — only for the Google Sheets features (roster sheets, CWL data
  export, signup dump, `RosterSource = Spreadsheet`). Without it everything else works;
  those commands report an error.

### Configuration

Copy [`ZenBotCS/appsettings.Example.json`](ZenBotCS/appsettings.Example.json) to
`ZenBotCS/appsettings.json` (it is **git-ignored** and not committed) and fill it in.

> **Secrets on a dev machine:** keep tokens, passwords and connection strings in
> [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets)
> rather than in `appsettings.json`. They live in your user profile, and
> `Program.cs` loads them after `appsettings.json`, so they override it:
>
> ```bash
> dotnet user-secrets set "CocApiPassword" "..." --project ZenBotCS
> ```
>
> On a server there is no user-secrets store; use the `chmod 600` `appsettings.json` there.

Keys the bot reads:

| Key | Purpose |
|-----|---------|
| `DiscordToken` | Discord bot token |
| `CocApiToken` | Clash of Clans API token |
| `CocApiAutoProvisionKey` | _Optional, dev machines._ When `true`, logs into developer.clashofclans.com at startup and (re)creates the key `CocApiKeyName` for the current public IP, so a changing home IP doesn't break the IP-locked token. `CocApiToken` is the fallback. |
| `CocApiEmail` / `CocApiPassword` | Login for developer.clashofclans.com, only needed with `CocApiAutoProvisionKey`. Put these in user secrets. |
| `CocApiKeyName` | _Optional._ Name of the auto-provisioned key (default `ZenBotCS-<machine name>`). |
| `CkApiToken` | ClashKing developer token (`ck_dev_…`). Required by `/v2/links/shared`, the Discord-link lookup; the war endpoints are public. |
| `BotDbConnectionString` | MySQL connection string for the bot database |
| `CocApiCacheConnectionString` | MySQL connection string for the CoC cache database |
| `PathToGspreadCredentialsOAuth2` | _Optional (Sheets)._ Path to the Google OAuth2 client JSON |
| `PathToGspreadToken` | _Optional (Sheets)._ Directory where the OAuth2 token is cached. Release builds never prompt for a login, so authorize once with a Debug build and copy the token here. |
| `CwlRosterTemplateSpreadsheetId` | _Optional (Sheets)._ Google Sheet template for CWL rosters |
| `CwlRosterChampStyleTemplateSpreadsheetId` | _Optional (Sheets)._ Template for "champ style" rosters |
| `RosterSource` | _Optional._ Where CWL roster reads come from: `Database` (default — the web roster site) or `Spreadsheet` (fall back to the pinned Google Sheet). |
| `FamilyLeadershipRoleId` / `WarGeneralRoleId` | Discord role IDs used for permission checks |
| `LeadershipNotesChannelId` | Channel for gatekeep/leadership notes |
| `HelpBotsLinking` / `HelpBotsGatekeeper` / `HelpCwlSignup` / `CwlSignupHelpButton` | Help text / button targets |
| `Serilog` | Logging configuration (console + rolling file) |

> The Google OAuth2 client file (`gspreadOAuth2.json`) is copied to the output
> directory on build and is git-ignored. Keep it out of source control.

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

## Running with Docker

A `Dockerfile` and `docker-compose.yml` are provided for containerized deployment.

### Prerequisites

- Docker and Docker Compose
- The same config files as above (`appsettings.json`, optionally `gspreadOAuth2.json`).

### Quick start

```bash
# Set the MySQL root password (used by init.sql and the compose file)
export MYSQL_ROOT_PASSWORD=your-secure-password

docker compose up -d
```

### What runs

| Service      | Purpose |
|-------------|---------|
| `mysql`      | MySQL 8.0 — `init.sql` auto-creates the `BotDb` and `CocApiCache` databases on first start. Persists data in a named volume. |
| `zenbot`     | The Discord bot (`.NET 8` runtime). EF migrations run at startup — no manual `dotnet ef` step needed. Logs written to a named volume. |
| `zenbot-web` | ASP.NET 8 web companion (roster rendering, etc.). Requires `libfontconfig1` for SkiaSharp font support. |
| `caddy`      | Caddy 2 reverse proxy in front of `zenbot-web` with automatic TLS via Let's Encrypt. Configured via `Caddyfile`. |

### Important notes

- **Connection strings** in your container-bound `appsettings.json` files must use
  `Server=mysql` (the compose service name), not `localhost`.
- Config files are **volume-mounted read-only** into the containers — they're
  excluded from the build context by `.dockerignore` so they are never baked into
  the image. Edit them on the host and restart affected containers to pick up
  changes.
- The `mysql` service has a health check; both app containers wait for it to be
  healthy before starting.

---

## Known issues / improvement backlog

**Recently addressed:** per-interaction `DbContext` scoping (each interaction now
opens its own DI scope; `RunMode.Sync`), background-service crash safety (the update
loops catch and log per cycle so a transient failure can't stop the host),
`ClashKingApiClient` lifetime (now a singleton reusing one `RestClient`), duplicate
command registration (now global-only), and the interaction error handler (cleanup
is now guarded and can't throw out of the handler).

**Still open:**

1. **`CwlService` is a ~1,400-line god class** mixing the signup state machine,
   spreadsheet formatting, roster generation, and role assignment — the main
   maintenance liability.

---

## License

See [LICENSE.txt](LICENSE.txt).
