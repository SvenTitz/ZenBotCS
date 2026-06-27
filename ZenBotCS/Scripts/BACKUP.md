# Database backups

How ZenBotCS's MySQL databases are backed up on the production (Ubuntu) VPS.
[`backup-zenbot.sh`](backup-zenbot.sh) dumps the databases nightly, keeps a local
rolling window, and mirrors them offsite to Google Drive with **rclone**.

## What gets backed up

- **Bot DB** (`BotDbConnectionString`) — critical and irreplaceable: account links,
  CWL signups, pinned rosters, clan settings, leadership notes, reminders, history.
- **CoC cache DB** (`CocApiCacheConnectionString`) — regenerable from the API, but
  included so a restore is instant instead of waiting for the cache to refill.
- **Secrets** (optional, off by default) — `appsettings.json`, `gspread*.json`, and
  the Google OAuth token directory. These contain the Discord/CoC tokens, DB
  passwords, and Google secrets, so they are uploaded **encrypted** (rclone `crypt`)
  and **never** to the plain Drive folder. Enable by filling in `SECRET_PATHS` in the
  script (see step 5).

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

### 5. (Optional) Encrypted secrets backup
The secret files (`appsettings.json`, `gspread*.json`, the Google token dir) are
backed up **encrypted** via an rclone `crypt` remote — never in plain text. Set up
the remote:
```bash
rclone config
```
New remote named **`gcrypt`**, storage **`crypt`**, `remote>` = `gdrive:zenbot-secrets`
(a *separate* Drive folder from the DB backups), then set a password (let rclone
generate one, or type your own).

> ⚠️ **Save that crypt password in your password manager.** It lives (obscured) in
> the VPS's rclone config so the job can run unattended, but if the VPS is lost and
> the password only lived there, the encrypted Drive backup is **unrecoverable**.

Then fill in the `SECRET_PATHS=( … )` array in the script with the real paths (the
Google ones are the `PathToGspread*` values from `appsettings.json`). That's it —
the next run tars them up, encrypts, and uploads to `gcrypt:`.

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

To restore the **secrets** after a full rebuild: re-create the `gcrypt` remote with
the *same password* (from your password manager), then pull and unpack:
```bash
rclone copy gcrypt:secrets_2026-06-26_0330.tar.gz .
tar xzf secrets_2026-06-26_0330.tar.gz   # extracts the files under their original paths
```
