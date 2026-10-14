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
| Website         | ASP.NET Core 8 Blazor Server + MudBlazor, Discord OAuth login, SkiaSharp images |

The solution has four projects:

- **`ZenBotCS`** – the bot host: composition root (`Program.cs`), Discord
  modules, services, background workers, helpers, and EF migrations.
- **`ZenBotCS.Entities`** – the EF `BotDataContext`, entity models, enums, and
  the ClashKing API DTOs (shared, dependency-light layer).
- **`ZenBotCS.Web`** – an optional Blazor Server website for leadership (CWL
  rosters, clan stats and CWL history, clan/bot settings). It shares the bot's
  database; see [The website](#the-website-zenbotcsweb).
- **`ZenBotCS.Tests`** – xUnit tests for the pure helpers (schedules, windows,
  parsing, diffing). Run with `dotnet test`.

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
- **Background services** (`Services/Background/`) run periodic loops:
  - `DiscordLinkUpdateService` – refreshes player↔Discord links from ClashKing (every 10 min)
  - `WarHistoryUpdateService` – pulls clan war history (every 15 min)
  - `PlayerWarHitsUpdateService` – caches per-player war hits (every 15 min)
  - `CwlHistoryUpdateService` – daily snapshot of each clan's CWL performance
  - `CwlRosterReminderService` – reminds leadership when the in-game CWL lineup
    doesn't match the roster before a war starts (every 15 min)
  - `WarSpinReminderService` – heads-up before mandatory wars and a ping after a
    missed war spin (every 5 min)
  - `LeadershipLogBackfillService` – backfills leadership-notes history (every 30 min)
- **CoC game data** comes from the CocApi cache, which runs its own background
  download workers. See [`docs/cocapi-integration.md`](docs/cocapi-integration.md).
- **Two databases**: the bot's own data (`BotDbConnectionString`) and the CoC API
  cache (`CocApiCacheConnectionString`). Both are MySQL and both have their EF
  migrations stored in this repo (`Migrations/BotDb`, `Migrations/CocApiCache`).

---

## Commands (overview)

| Group | Commands |
|-------|----------|
| `/clan` | `add`, `delete`, `list`, `warlog`, `stats attacks`, `roles audit`, `settings edit`, `settings reset` |
| `/cwl` | `data`, `signup post/summary/check/delete/reset/dump/move/add`, `roster get/pin/missing-spin/missing-day`, `roles assign/remove` (+ button/menu component flows for the signup wizard) |
| `/player` | `to-do`, `stats misses`, `stats attacks` |
| `/links` | `list-unlinked`, `update`, `lookup` (`add`/`remove` exist but are disabled) |
| `/reminder` | `misses add/remove/list` |
| `/gatekeep` | `notes lookup`, `notes post` |
| `/help` | `bots linking`, `bots gatekeeper`, `cwl signup` |
| `/util` | `ping`, `timestamp`, `spintimes` |

Admin-only commands use `[RequireUserPermission(GuildPermission.Administrator)]`;
leadership commands check the `FamilyLeadershipRoleId` role.

Commands are registered **globally**, so after the first start (or a new command)
it can take a while until Discord shows them.

---

## Getting started

### Prerequisites

- .NET 8 SDK (plus `dotnet tool install -g dotnet-ef` if you want to add migrations)
- A MySQL 8 server (MariaDB works too) with **two empty databases**, one for the
  bot and one for the CoC cache. [`init.sql`](init.sql) creates them as `BotDb` and
  `CocApiCache`. The bot creates all tables itself on startup.
- A **Discord application with a bot user** (see [Discord setup](#discord-setup))
- A **Clash of Clans developer account** at https://developer.clashofclans.com.
  API keys are IP-locked: either create a key for the machine's public IP and set
  `CocApiToken`, or let the bot create one at startup with `CocApiAutoProvisionKey`
  (handy on a home connection whose IP changes).
- A **ClashKing developer token** (`ck_dev_…`) for the Discord-link lookup. Without
  it the war-history features still work, but links (and so CWL signups for
  unlinked players) only come from the bot's own table.
- _Optional:_ a **Google OAuth2 client** (`gspreadOAuth2.json`) with access to the
  Sheets & Drive APIs — only for the Google Sheets features (roster sheets, CWL data
  export, signup dump, `RosterSource = Spreadsheet`). Without it everything else works;
  those commands report an error.

### Discord setup

In the [Discord Developer Portal](https://discord.com/developers/applications):

1. Create an application, add a **Bot**, and copy its token (`DiscordToken`).
2. Under *Bot → Privileged Gateway Intents*, enable **Server Members** and
   **Message Content**. The bot won't connect without them.
3. Invite it with the `bot` and `applications.commands` scopes. It needs at least
   *View Channels*, *Send Messages*, *Embed Links*, *Read Message History* and
   **Manage Roles** (for `/cwl roles`). Its own role must sit **above** the roles it
   assigns.
4. Copy the role/channel IDs the config asks for (enable *Developer Mode* in
   Discord, then right-click → *Copy ID*).

The website uses the **same application** for its login (OAuth2 client ID/secret)
and the same bot token; see [The website](#the-website-zenbotcsweb).

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
dotnet test
```

On startup the bot applies all pending EF migrations to **both** databases before it
connects to Discord, so a fresh, empty database is fine and no manual
`dotnet ef database update` is needed. If a migration fails, the bot logs it and
exits.

Once it's running, add your clans with `/clan add` and configure them with
`/clan settings edit` (or on the website).

### Database migrations

Migrations live in this repo for both contexts (`MigrationsAssembly("ZenBotCS")`,
`Migrations/BotDb` and `Migrations/CocApiCache`). The bot owns them; the website
never migrates. To apply them by hand anyway:

```bash
# Bot database
dotnet ef database update --project ZenBotCS --context BotDataContext

# CoC cache database
dotnet ef database update --project ZenBotCS --context CacheDbContext
```

`dotnet ef` starts the bot's host, so it needs `ZenBotCS/appsettings.json` (or user
secrets) with the connection strings.

To add a migration:

```bash
dotnet ef migrations add <Name> --project ZenBotCS \
  --context BotDataContext --output-dir Migrations/BotDb
```

---

## The website (`ZenBotCS.Web`)

Optional. A Blazor Server site for leadership that replaces the old Google Sheet
rosters: editable CWL rosters (including sub-rosters played in other clans), clan
and player stats, CWL history with a downloadable summary image, and clan/bot
settings. People sign in with Discord; access is granted by a Discord role.

It reads and writes the **bot's database** directly and reads the CoC cache
database for clan names and the live CWL, so **the bot must run first** (it creates
the tables). There is no API between the two.

### Configuration

Copy [`ZenBotCS.Web/appsettings.Example.json`](ZenBotCS.Web/appsettings.Example.json)
to `ZenBotCS.Web/appsettings.json` and fill it in. The comments in the example
describe every key; in short:

| Key | Purpose |
|-----|---------|
| `BotDbConnectionString` / `CocApiCacheConnectionString` | Same databases as the bot. |
| `MySqlServerVersion` | e.g. `8.0.0-mysql` or `10.11.0-mariadb`. |
| `CocApiToken` | CoC API key for the server's IP, used when adding a player. |
| `CkApiToken` | ClashKing developer token, for the Discord-link lookup when adding a player. |
| `Discord:ClientId` / `Discord:ClientSecret` | The Discord application's OAuth2 credentials. |
| `Discord:BotToken` | The bot's token, used to check a user's roles and to list roles/channels. |
| `Discord:GuildId` | Your server's ID. |
| `Discord:RequiredRoleId` | Role that grants access (usually the bot's `FamilyLeadershipRoleId`). |
| `Discord:GatekeeperRoleId` | _Optional._ Role that can also edit clan settings. |
| `Discord:AdminUserIds` | _Optional._ Comma-separated user IDs that can also edit bot settings. |

User secrets work here too, but ASP.NET Core only loads them when
`ASPNETCORE_ENVIRONMENT=Development` (the default for `dotnet run`):

```bash
dotnet user-secrets set "Discord:ClientSecret" "..." --project ZenBotCS.Web
```

In the Developer Portal, under *OAuth2 → Redirects*, add
`https://YOUR_DOMAIN/signin-discord`, and `https://localhost:7114/signin-discord`
for local development.

### Run

```bash
dotnet run --project ZenBotCS.Web --launch-profile https
```

This serves `https://localhost:7114`. Use the `https` profile: the login cookie is
`Secure`, and Firefox refuses it over plain HTTP.

More detail: [`docs/web-roster-site.md`](docs/web-roster-site.md) (features,
authorization, troubleshooting) and
[`docs/web-roster-deployment.md`](docs/web-roster-deployment.md) (the systemd +
Caddy setup on a VPS).

---

## Running with Docker

A `Dockerfile` and `docker-compose.yml` are provided for containerized deployment.

### Prerequisites

- Docker and Docker Compose
- `ZenBotCS/appsettings.json` and `ZenBotCS.Web/appsettings.json`, filled in as
  described above. User secrets don't exist inside the containers, so everything
  goes into these files.
- Optionally `ZenBotCS/gspreadOAuth2.json` for the Google Sheets features.
- A domain pointing at the host, set in the [`Caddyfile`](Caddyfile) (it ships with
  the production domain; replace it with yours).

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
  `Server=mysql` (the compose service name), not `localhost`. The compose MySQL only
  has the `root` user, so use `user=root;password=<MYSQL_ROOT_PASSWORD>` with the
  `BotDb` / `CocApiCache` databases, or create a dedicated user yourself.
- Startup order is handled for you: the bot waits for MySQL, applies the migrations,
  and only then does the website start.
- The CoC API key must be valid for the **host's public IP**.
- **Google Sheets in Docker:** compose doesn't mount a folder for the OAuth2 token
  (`PathToGspreadToken`). To use the Sheets features, mount one (e.g.
  `./ZenBotCS/gspread-token:/app/gspread-token`) and put the token there. Without it,
  only those commands fail.
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
