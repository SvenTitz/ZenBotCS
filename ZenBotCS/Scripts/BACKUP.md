# Database backups

How ZenBotCS's MySQL databases are backed up on the production (Ubuntu) VPS.
[`backup-zenbot.sh`](backup-zenbot.sh) dumps the databases nightly, keeps a local
rolling window, and mirrors them offsite to Google Drive with **rclone**.

## What gets backed up

- **Bot DB** (`BotDbConnectionString`) — critical and irreplaceable: account links,
  CWL signups, pinned rosters, clan settings, leadership notes, reminders, history.
- **CoC cache DB** (`CocApiCacheConnectionString`) — regenerable from the API, but
  included so a restore is instant instead of waiting for the cache to refill.

> Not covered by this script: `appsettings.json`, `gspread*.json`, and the Google
> OAuth token directory (`PathToGspreadToken`). Keep those safe separately — losing
> the OAuth token means re-authorising Google.

## One-time setup

### 1. MySQL credentials (`~/.my.cnf`)
The script reads the DB login from `~/.my.cnf` so no password sits in the script or
cron. Both databases are assumed to use the same MySQL login (taken from the
connection strings in `appsettings.json`).

```bash
nano ~/.my.cnf
```
```ini
[client]
user=YOUR_DB_USER
password=YOUR_DB_PASSWORD
```
```bash
chmod 600 ~/.my.cnf
```

### 2. The script
Copy `backup-zenbot.sh` to the VPS (e.g. `~/backup-zenbot.sh`), edit the config
block at the top (database names, retention), and make it executable:
```bash
chmod +x ~/backup-zenbot.sh
```

### 3. rclone → Google Drive
```bash
curl https://rclone.org/install.sh | sudo bash
rclone config
```
Create a remote named **`gdrive`**: storage `drive`, leave client id/secret blank,
scope **`drive.file`** (rclone only ever sees files it created). When asked
"Use web browser to automatically authenticate?", answer **No** (the VPS is
headless), then run the printed `rclone authorize "drive" "..."` command on a
machine that has a browser *and* rclone (e.g. your PC), and paste the token back.

Test:
```bash
rclone copy ~/backup-zenbot.sh gdrive:zenbot-backups
rclone ls gdrive:zenbot-backups
```

### 4. Schedule it (cron)
```bash
crontab -e
```
Add (daily at 03:30 — use your real home path; check with `whoami`):
```cron
30 3 * * * /home/YOURUSER/backup-zenbot.sh >> /home/YOURUSER/backup.log 2>&1
```

## Retention

- **Local** — `LOCAL_RETENTION_DAYS` (default 14), pruned with `find -mtime`.
- **Offsite** — `REMOTE_RETENTION_DAYS` (default 60), pruned with
  `rclone delete --min-age`, which removes only files past that age.

The offsite step uses `rclone copy` + an age-based delete **on purpose** — not
`rclone sync`. `sync` would delete the Drive copy of anything missing locally, so a
lost or wiped local folder would propagate to the offsite copy and defeat the
backup. `copy` only uploads, and the age-based delete can never wipe recent files.

## Restore

Fetch a dump (from `~/backups`, or `rclone copy gdrive:zenbot-backups/FILE .`) and
load it:
```bash
gunzip < zenbot_2026-06-26_0330.sql.gz | mysql YOUR_BOT_DB
```
Test a restore into a scratch database now and then — an untested backup isn't a
backup.
