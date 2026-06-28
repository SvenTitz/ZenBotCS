# Web roster site (`ZenBotCS.Web`)

A small self-hosted **Blazor Server** site for viewing/editing CWL rosters, meant to
replace the Google Sheet. It runs on the **same VPS** as the bot, as a separate
process, and talks to the **same bot MySQL database** directly (no API layer).

## Architecture

```
ZenBotCS.Entities  ← shared BotDataContext + models
      ▲                       ▲
      │                       │
  ZenBotCS (bot)        ZenBotCS.Web (this site)
```

- Both the bot and the site reference `ZenBotCS.Entities`; they do **not** reference
  each other. Each is its own process with its own DB connections.
- The site reads/writes the bot DB via `IDbContextFactory<BotDataContext>` (a
  short-lived context per operation — a scoped context is unsafe in Blazor Server,
  where a scope lives for the whole circuit).
- **Migrations are owned by the bot** (`ZenBotCS`). The site never runs migrations.
- Interactivity is **Interactive Server** only (no WebAssembly), so components reach
  the DB directly and no separate API is needed. This uses a persistent
  SignalR/WebSocket connection — fine for the handful of people editing rosters.

## Configuration

`appsettings.json` is **git-ignored** (same as the bot). Copy
[`appsettings.Example.json`](../ZenBotCS.Web/appsettings.Example.json) to
`appsettings.json` and fill in:

| Key | What |
| --- | --- |
| `BotDbConnectionString` | Same bot DB the bot uses. |
| `MySqlServerVersion` | e.g. `8.0.0-mysql` or `10.11.0-mariadb`. Set so startup never opens a probe connection. |
| `Discord:ClientId` / `Discord:ClientSecret` | From the Discord Developer Portal → your app → OAuth2. |
| `Discord:BotToken` | Used to look up a member's roles for authorization (see below). |
| `Discord:GuildId` | Your community server id. |
| `Discord:RequiredRoleId` | Discord role id that grants roster access — typically the same as the bot's `FamilyLeadershipRoleId`. |

### Discord application

In the [Developer Portal](https://discord.com/developers/applications) → your app →
**OAuth2** → add a redirect URL: `https://YOUR_DOMAIN/signin-discord`
(for local dev, `http://localhost:5075/signin-discord`). That callback path is the
default for the Discord OAuth handler.

### Authorization

Access is gated on a **single Discord role**, mirroring the bot's
`RequireLeadershipRoleAttribute` (which checks `FamilyLeadershipRoleId`). Because the
site has no gateway connection, on login it looks the member up via the Discord REST
API (`GET /guilds/{GuildId}/members/{userId}`) using `Discord:BotToken`, and grants
the `RosterAccess` role claim only if the member holds `Discord:RequiredRoleId`. It is
**fail-closed**: anyone without the role (or not in the guild) can sign in but sees no
roster content. The bot must be a member of the guild for the lookup to work.

## Build & run

```bash
dotnet build                                   # whole solution
dotnet run --project ZenBotCS.Web              # local dev (http://localhost:5075)
dotnet publish ZenBotCS.Web -c Release -o /opt/zenbot-web   # on the VPS
```

## Deploy on the VPS (systemd + reverse proxy)

The site is a second long-running process alongside the bot.

### 1. systemd service

`/etc/systemd/system/zenbot-web.service`:

```ini
[Unit]
Description=ZenBot roster web site
After=network.target mysql.service

[Service]
WorkingDirectory=/opt/zenbot-web
ExecStart=/usr/bin/dotnet /opt/zenbot-web/ZenBotCS.Web.dll
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5080
Restart=always
RestartSec=5
User=YOURUSER

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now zenbot-web
```

The app listens only on `127.0.0.1:5080`; the reverse proxy terminates TLS in front
of it.

### 2. Reverse proxy (Caddy — automatic HTTPS)

`/etc/caddy/Caddyfile`:

```caddy
rosters.YOUR_DOMAIN {
    reverse_proxy 127.0.0.1:5080
}
```

Caddy proxies WebSockets out of the box, which Blazor Server's circuit needs — no
extra config. `sudo systemctl reload caddy` and you're done.

> If you use nginx instead, you must add the `Upgrade`/`Connection` headers for the
> WebSocket to work; Caddy needs none.

### 3. Updates

```bash
git pull
dotnet publish ZenBotCS.Web -c Release -o /opt/zenbot-web
sudo systemctl restart zenbot-web
```

## Backups

No new backup work: all roster data lives in the bot DB, already covered by
[`Scripts/BACKUP.md`](../ZenBotCS/Scripts/BACKUP.md). Add `appsettings.json` from
`/opt/zenbot-web` to the encrypted secrets list if you want its Discord secrets
backed up too.

## Troubleshooting

- **Login does nothing / no redirect to Discord (silent).** A privacy/content-blocker
  browser extension (e.g. **Privacy Badger**, uBlock Origin) is cancelling the
  navigation to `discord.com`. Whitelist `discord.com` for the site. This can affect
  end users too, not just local dev.
- **Signed in, but "no roster access".** The account doesn't hold `Discord:RequiredRoleId`,
  **or** the configured `BotToken` belongs to a bot that isn't in `Discord:GuildId`, **or**
  `GuildId`/`RequiredRoleId` are wrong. The role lookup is `GET /guilds/{GuildId}/members/{userId}`
  with the bot token — it must succeed and return that role id.
- **Role lookup needs a `User-Agent`.** Discord's API (behind Cloudflare) returns an
  empty-body `403` to requests without a recognized `User-Agent`. The code sets one
  (`DiscordBot (...)`); keep it if you touch that call.
- **Local dev must be HTTPS for the OAuth cookie.** The OAuth correlation cookie is
  `Secure`. Chrome treats `http://localhost` as secure and works; **Firefox refuses
  `Secure` cookies over HTTP**. Run the `https` launch profile and register
  `https://localhost:7114/signin-discord` as a redirect in the Discord app. In
  production this is moot (Caddy serves real HTTPS).
- **Firefox shows a cert warning on `https://localhost`.** Firefox uses its own trust
  store and ignores the Windows-trusted .NET dev cert. Set
  `security.enterprise_roots.enabled = true` in `about:config`, or accept the exception.
