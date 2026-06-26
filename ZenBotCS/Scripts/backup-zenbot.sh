#!/usr/bin/env bash
#
# Nightly backup for ZenBotCS: dumps the MySQL databases, keeps a local rolling
# window, and mirrors them offsite to Google Drive via rclone.
#
# Setup and scheduling instructions: see BACKUP.md (same folder).
#
set -euo pipefail

# --- configuration -----------------------------------------------------------
DBS=(zenbot zenbot_cache)               # database names: bot DB first, CoC cache DB second
DEST="$HOME/backups"                    # local backup folder
RCLONE_REMOTE="gdrive:zenbot-backups"   # rclone remote + folder for the offsite copy
LOCAL_RETENTION_DAYS=14                 # days of backups to keep locally
REMOTE_RETENTION_DAYS=60                # days of backups to keep offsite (>= local is fine)
# -----------------------------------------------------------------------------

mkdir -p "$DEST"
STAMP=$(date +%F_%H%M)

# Dump each database. --single-transaction gives a consistent InnoDB snapshot
# without locking the bot out; credentials come from ~/.my.cnf (see BACKUP.md)
# so no password is exposed here or in cron.
for DB in "${DBS[@]}"; do
    mysqldump --single-transaction --quick --no-tablespaces "$DB" \
        | gzip > "$DEST/${DB}_${STAMP}.sql.gz"
done

# Prune local copies older than the retention window.
find "$DEST" -name '*.sql.gz' -mtime +"$LOCAL_RETENTION_DAYS" -delete

# Upload to Google Drive. 'copy' only ever uploads, so a lost/empty local folder
# can never wipe the offsite copy (which 'rclone sync' would).
rclone copy "$DEST" "$RCLONE_REMOTE"

# Prune offsite copies older than the remote window (by age only, never mirroring local loss).
rclone delete --min-age "${REMOTE_RETENTION_DAYS}d" "$RCLONE_REMOTE"

echo "$(date '+%F %T') backup complete: ${DBS[*]}"
