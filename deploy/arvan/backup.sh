#!/usr/bin/env bash
# Database backup into deploy/arvan/backups/amlak-<date>.bak.   Usage: sudo bash deploy/arvan/backup.sh
set -euo pipefail
cd "$(dirname "$0")"
name="amlak-$(date +%Y%m%d-%H%M).bak"
docker compose exec -T db sh -c "/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P \"\$MSSQL_SA_PASSWORD\" -Q \"BACKUP DATABASE Amlak TO DISK=N'/var/opt/mssql/$name' WITH INIT\""
mkdir -p backups
docker compose cp "db:/var/opt/mssql/$name" "backups/$name"
docker compose exec -T db rm -f "/var/opt/mssql/$name"
echo "Saved deploy/arvan/backups/$name"
