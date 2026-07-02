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
  **not** overwritten by re-publishing, because it doesn't exist in the repo.
- **DB**: bound to `127.0.0.1` only; firewall (`ufw`) allows just `22/80/443`. Remote DB
  access is via SSH tunnel (`ssh -L 3307:127.0.0.1:3306 YOURUSER@vps`), not an open port.
- Currently deployed from the **`feature/web`** branch (switch the commands to `main` once
  it's merged).

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
If the pulled changes add an EF migration (new files under `ZenBotCS/Migrations/BotDb/`),
apply it **before** restarting — and **back up the DB first** (`Scripts/BACKUP.md`):
```bash
cd ~/ZenBotCS
git pull
dotnet ef database update --project ZenBotCS --context BotDataContext   # see gotcha below
dotnet publish ZenBotCS.Web -c Release -o /opt/zenbot-web
sudo systemctl restart zenbot-web
```
> **Gotcha:** `dotnet ef` spins up the bot's host, which needs the bot's `appsettings.json`
> in the `ZenBotCS` project folder. It's git-ignored, so the VPS clone usually doesn't have
> it and you'll get *"Unable to create a 'DbContext'…"*. Fix: copy your bot's config into the
> project folder first — `cp /opt/zenbot-bot/appsettings.json ~/ZenBotCS/ZenBotCS/` (adjust
> the source path). (A design-time `IDesignTimeDbContextFactory` would remove this step — not
> yet added.)

### Handy commands
```bash
journalctl -u zenbot-web -f          # live app logs
journalctl -u caddy -f               # Caddy / TLS logs
sudo systemctl restart zenbot-web    # restart app
sudo systemctl reload caddy          # reload Caddy after editing the Caddyfile
```

---

## First-time setup (reference)

Already done, but here's the full path in case you rebuild the box.

1. **DNS** — point `rosters.YOURDOMAIN` (A / AAAA record) at the VPS IP. Verify with
   `dig +short rosters.YOURDOMAIN`.
2. **Code** — `cd ~/ZenBotCS && git checkout feature/web && git pull`.
3. **Runtime** — confirm ASP.NET Core 8 is present: `dotnet --list-runtimes | grep AspNetCore`
   (install `aspnetcore-runtime-8.0` if missing).
4. **Migrate** — back up, then `dotnet ef database update --project ZenBotCS --context BotDataContext`
   (see gotcha above).
5. **Publish** — `sudo mkdir -p /opt/zenbot-web && sudo chown $USER:$USER /opt/zenbot-web`
   then `dotnet publish ZenBotCS.Web -c Release -o /opt/zenbot-web`.
6. **Config** — `cd /opt/zenbot-web && cp appsettings.Example.json appsettings.json`, fill in
   the DB connection strings (same as the bot), `MySqlServerVersion`, and the Discord
   `ClientId`/`ClientSecret`/`BotToken`/`GuildId`/`RequiredRoleId`; then `chmod 600 appsettings.json`.
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

---

## Troubleshooting (things we actually hit)

- **"Server Not Found" / site won't load, but `dig` resolves it** — local DNS cached the old
  "no such domain" answer. `ipconfig /flushdns` on your PC (and check the browser isn't using a
  lagging DNS-over-HTTPS resolver).
- **Site loads but Discord login fails / redirect mismatch** — the app is building `http://`
  callback URLs. Make sure the deployed build includes `UseForwardedHeaders` (the proxy fix) and
  that `https://rosters.YOURDOMAIN/signin-discord` is registered in Discord.
- **`dotnet ef` → "Unable to create a 'DbContext'…"** — missing `appsettings.json` in the
  `ZenBotCS` project folder; see the migration gotcha above.
- **502 Bad Gateway** — Caddy is up but the app isn't: `systemctl status zenbot-web`,
  `journalctl -u zenbot-web -e`.
- **Cert not issued** — Caddy needs port 80 reachable and DNS live: `journalctl -u caddy -e`.
- **DB shows `0.0.0.0:3306`** — it's listening publicly; set `bind-address = 127.0.0.1` in the
  MariaDB/MySQL config and restart. The bot, the web app, and the SSH tunnel all use localhost,
  so this doesn't break them.
