# Roster site — deployment & updates

Runbook for `ZenBotCS.Web` on the Ubuntu VPS. For what the app *is*, see
[`web-roster-site.md`](web-roster-site.md); this doc is purely how it's hosted and how to
update it.

Replace `rosters.YOURDOMAIN` with the real subdomain, `YOURUSER` with your SSH user, and
`~/ZenBotCS` with your repo clone path throughout.

---

## What's running

```
Browser ──HTTPS──> Caddy (:80/:443, auto TLS) ──HTTP──> zenbot-web (127.0.0.1:5080)
                                                              │  reads/writes
                                                              ▼
                                                         bot MySQL DB (127.0.0.1:3306)
```

- **App**: published to `/opt/zenbot-web`, run by the **systemd** service `zenbot-web`,
  listening only on `127.0.0.1:5080` (`ASPNETCORE_ENVIRONMENT=Production`).
- **Reverse proxy**: **Caddy** terminates HTTPS and proxies to the app. The app uses
  `UseForwardedHeaders`, so it sees the real `https`/host (needed for correct OAuth URLs).
- **Config/secrets**: `/opt/zenbot-web/appsettings.json` (git-ignored, `chmod 600`). It is
  **not** overwritten by re-publishing, because it doesn't exist in the repo. User secrets
  are not loaded in Production, so every secret lives in this file.
- **Bot**: published to `/opt/zenbot-bot`, run by the systemd service `zenbot-bot`, with its
  own `appsettings.json` there. The bot **owns the database schema**: it applies all pending
  EF migrations on startup, before it connects to Discord. The site never migrates.
- **DB**: bound to `127.0.0.1` only; firewall (`ufw`) allows just `22/80/443`. Remote DB
  access is via SSH tunnel (`ssh -L 3307:127.0.0.1:3306 YOURUSER@vps`), not an open port.
- Deployed from **`main`**.

---

## Updating (the routine you'll use most)

### Normal update (no database change)
```bash
cd ~/ZenBotCS
git pull
dotnet publish ZenBotCS.Web -c Release -o /opt/zenbot-web
sudo systemctl restart zenbot-web
```
Verify:
```bash
systemctl status zenbot-web --no-pager | head -5     # active (running)
curl -sS http://127.0.0.1:5080 -o /dev/null -w "%{http_code}\n"   # 200
```
`appsettings.json` is preserved across publishes, so there's nothing to re-enter.

### Update that includes a database migration
If the pulled changes add an EF migration (new files under `ZenBotCS/Migrations/`), update
the **bot first**: restarting it applies the migration. **Back up the DB first**
(`Scripts/BACKUP.md`):
```bash
cd ~/ZenBotCS
git pull
dotnet publish ZenBotCS -c Release -o /opt/zenbot-bot
sudo systemctl restart zenbot-bot
journalctl -u zenbot-bot -n 50 --no-pager      # look for the migration / any "migration failed"
dotnet publish ZenBotCS.Web -c Release -o /opt/zenbot-web
sudo systemctl restart zenbot-web
```
If a migration fails, the bot logs `Database migration failed during startup.` and exits,
so the site keeps running on the old schema until it's fixed.
Restarting the site before the bot can make it query columns that don't exist yet.

> **Manual `dotnet ef` (only if you need it):** `dotnet ef database update` spins up the bot's
> host, which needs the bot's `appsettings.json` in the `ZenBotCS` project folder. It's
> git-ignored, so the VPS clone usually doesn't have it and you'll get *"Unable to create a
> 'DbContext'…"*. Copy it there first: `cp /opt/zenbot-bot/appsettings.json ~/ZenBotCS/ZenBotCS/`.

### Handy commands
```bash
journalctl -u zenbot-web -f          # live app logs
journalctl -u zenbot-bot -f          # live bot logs (migrations show up here)
journalctl -u caddy -f               # Caddy / TLS logs
sudo systemctl restart zenbot-web    # restart app
sudo systemctl reload caddy          # reload Caddy after editing the Caddyfile
```

---

## First-time setup (reference)

Already done, but here's the full path in case you rebuild the box.

1. **DNS** — point `rosters.YOURDOMAIN` (A / AAAA record) at the VPS IP. Verify with
   `dig +short rosters.YOURDOMAIN`.
2. **Code** — `cd ~/ZenBotCS && git checkout main && git pull`.
3. **Runtime** — confirm ASP.NET Core 10 is present: `dotnet --list-runtimes | grep AspNetCore`
   (install `aspnetcore-runtime-10.0` if missing).
4. **Bot running** — the bot (`zenbot-bot`) must be on the current commit and have started
   once, so its startup migration has created/updated the tables (see the main
   [README](../README.md) for setting the bot up). Back up the DB before that if it already
   holds data.
5. **Publish** — `sudo mkdir -p /opt/zenbot-web && sudo chown $USER:$USER /opt/zenbot-web`
   then `dotnet publish ZenBotCS.Web -c Release -o /opt/zenbot-web`.
6. **Config** — `cd /opt/zenbot-web && cp appsettings.Example.json appsettings.json` and fill in:
   - `BotDbConnectionString` and `CocApiCacheConnectionString` (same databases as the bot),
     `MySqlServerVersion`
   - `CocApiToken` (a CoC API key for the VPS IP) and `CkApiToken` (ClashKing developer
     token), both used by *Add player*
   - Discord `ClientId`/`ClientSecret`/`BotToken`/`GuildId`/`RequiredRoleId`, plus the optional
     `GatekeeperRoleId` and `AdminUserIds`

   Then `chmod 600 appsettings.json`.
7. **systemd** — `/etc/systemd/system/zenbot-web.service`:
   ```ini
   [Unit]
   Description=ZenBot roster web site
   After=network.target

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
   (Use your real `which dotnet` path.) Then `sudo systemctl daemon-reload && sudo systemctl enable --now zenbot-web`.
8. **Caddy** — install it, then `/etc/caddy/Caddyfile`:
   ```
   rosters.YOURDOMAIN {
       reverse_proxy 127.0.0.1:5080
   }
   ```
   `sudo systemctl reload caddy`.
9. **Firewall** — `sudo ufw allow OpenSSH && sudo ufw allow 80 && sudo ufw allow 443 && sudo ufw enable`.
10. **Discord** — add redirect URL `https://rosters.YOURDOMAIN/signin-discord` in the Developer
    Portal (OAuth2 → Redirects).
11. **Restart-on-runtime-upgrade hook** — `/etc/apt/apt.conf.d/99-restart-zenbot`:
    ```
    DPkg::Post-Invoke { "systemctl try-restart zenbot-web zenbot-bot || true"; };
    ```
    Without this, a monthly `unattended-upgrades` patch of the .NET runtime leaves both
    long-running processes pointing at a deleted framework directory (see Troubleshooting).
    It runs after dpkg finishes, and `try-restart` is a no-op for anything not running.

---

## Troubleshooting (things we actually hit)

- **Random `FileNotFoundException` on a *framework* assembly after weeks of uptime**
  (e.g. *Could not load file or assembly 'System.Formats.Asn1, Version=8.0.0.0'* breaking
  Discord login). The .NET runtime was patched underneath the running process:
  `unattended-upgrades` installs `<major>.<minor>.<new>` and **deletes**
  `/usr/share/dotnet/shared/Microsoft.NETCore.App/<major>.<minor>.<old>/`, which the process pinned at
  startup. Already-loaded assemblies stay mapped and keep working, so the app looks healthy
  for days — until it first needs one it hadn't loaded yet. `System.Formats.Asn1` is the
  usual victim: it loads only on the first outbound TLS cert-chain build in the process.
  `sudo systemctl restart zenbot-web` fixes it instantly. Confirm after the fact with
  `grep -E 'dotnet|aspnetcore' /var/log/dpkg.log* | grep -E ' (install|upgrade) '` and
  compare against `systemctl show zenbot-web -p ActiveEnterTimestamp`. Prevented by the apt
  hook in setup step 11.

- **"Server Not Found" / site won't load, but `dig` resolves it** — local DNS cached the old
  "no such domain" answer. `ipconfig /flushdns` on your PC (and check the browser isn't using a
  lagging DNS-over-HTTPS resolver).
- **Site loads but Discord login fails / redirect mismatch** — the app is building `http://`
  callback URLs. Make sure the deployed build includes `UseForwardedHeaders` (the proxy fix) and
  that `https://rosters.YOURDOMAIN/signin-discord` is registered in Discord.
- **`dotnet ef` → "Unable to create a 'DbContext'…"** — missing `appsettings.json` in the
  `ZenBotCS` project folder; see the manual `dotnet ef` note above. Usually you don't need
  `dotnet ef` at all: restarting the bot migrates.
- **Site errors about unknown columns/tables after an update** — the site was updated before
  the bot applied the new migration. Publish and restart `zenbot-bot`, check its log, then
  restart `zenbot-web`.
- **502 Bad Gateway** — Caddy is up but the app isn't: `systemctl status zenbot-web`,
  `journalctl -u zenbot-web -e`.
- **Cert not issued** — Caddy needs port 80 reachable and DNS live: `journalctl -u caddy -e`.
- **DB shows `0.0.0.0:3306`** — it's listening publicly; set `bind-address = 127.0.0.1` in the
  MariaDB/MySQL config and restart. The bot, the web app, and the SSH tunnel all use localhost,
  so this doesn't break them.
