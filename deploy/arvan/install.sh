#!/usr/bin/env bash
# Installs and starts Royal Fine Homes on a fresh Ubuntu server (ArvanCloud cloud server, 4 GB RAM or more).
#
#   git clone https://github.com/AhmadrezaHamidi/Amalki.git && cd Amalki
#   sudo bash deploy/arvan/install.sh
#
# Safe to run again: it keeps .env (passwords) and the data volumes, rebuilds the site and restarts it.
# Guide (Persian): docs/DEPLOY-ARVAN.md
set -euo pipefail

cd "$(dirname "$0")"
DIR=$(pwd)
say() { printf '\n\033[1;34m==> %s\033[0m\n' "$*"; }
die() { printf '\n\033[1;31m%s\033[0m\n' "$*" >&2; exit 1; }

[ "$(id -u)" -eq 0 ] || die "Run with sudo: sudo bash deploy/arvan/install.sh"

# 1. Docker from Ubuntu's own packages (download.docker.com does not serve Iranian IPs; Arvan's Ubuntu mirror does).
if ! command -v docker >/dev/null || ! docker compose version >/dev/null 2>&1; then
  say "Installing Docker"
  apt-get update -q
  apt-get install -y -q docker.io docker-compose-v2 curl openssl ||
    apt-get install -y -q docker.io docker-compose-plugin curl openssl
  systemctl enable --now docker
fi

# 2. SQL Server needs 2 GB of memory; add swap on small servers so it does not get killed.
mem_mb=$(awk '/MemTotal/ {print int($2/1024)}' /proc/meminfo)
if [ "$mem_mb" -lt 3500 ] && ! swapon --show | grep -q .; then
  say "Only ${mem_mb} MB RAM: adding 2 GB swap"
  fallocate -l 2G /swapfile && chmod 600 /swapfile && mkswap /swapfile >/dev/null && swapon /swapfile
  grep -q '^/swapfile' /etc/fstab || echo '/swapfile none swap sw 0 0' >> /etc/fstab
fi

# 3. Settings with random passwords (only the first time).
if [ ! -f .env ]; then
  say "Creating deploy/arvan/.env"
  ip=$(curl -4 -fsS --max-time 5 https://api.ipify.org 2>/dev/null || hostname -I | awk '{print $1}')
  cat > .env <<ENV
# Created by install.sh. Change and run install.sh again. This file holds passwords: never commit it.
SITE_URL=http://${ip}
HTTP_PORT=80
DB_PASSWORD=Db-$(openssl rand -hex 12)A1!
# Test mode: OTP shown on screen + one-click demo admin / user. Set false before real use.
DEMO_MODE=true
SEED_DEMO=true
ADMIN_EMAIL=
ADMIN_PASSWORD=
SMS_PROVIDER=Log
SMS_API_KEY=
SMS_TEMPLATE=
SMS_SENDER=
TELEGRAM_BOT_TOKEN=
TELEGRAM_CHAT_ID=
TELEGRAM_ADMIN_CHAT_ID=
# Telegram is filtered in Iran: your own relay outside Iran (docs/CHANNELS.md).
TELEGRAM_API_BASE=https://api.telegram.org
TELEGRAM_WEBHOOK_SECRET=
BALE_BOT_TOKEN=
BALE_CHAT_ID=
BALE_ADMIN_CHAT_ID=
ENV
  chmod 600 .env
fi
set -a; . ./.env; set +a

# 4. Images. Microsoft's registry (mcr.microsoft.com) is unreachable from many servers in Iran. Two ways round it:
#    - IMAGE_REGISTRY=ghcr.io/<github-user>: pull both images that GitHub Actions built and mirrored there
#      (.github/workflows/arvan-images.yml; run `docker login ghcr.io` once if the packages are private);
#    - deploy/arvan/ship-images.sh from a computer that can reach mcr.microsoft.com.
MSSQL_IMAGE=mcr.microsoft.com/mssql/server:2022-latest
need_ship() {
  die "Could not download $1 on this server (probably blocked from Iran).
Use the images GitHub builds instead (docs/DEPLOY-ARVAN.md, \"GitHub images\"):
    docker login ghcr.io -u <github-user>      # password: a GitHub token with read:packages
    IMAGE_REGISTRY=ghcr.io/<github-user> sudo -E bash deploy/arvan/install.sh
or from your own computer: bash deploy/arvan/ship-images.sh root@${SITE_URL#http://}"
}
if [ -n "${IMAGE_REGISTRY:-}" ]; then
  reg=${IMAGE_REGISTRY%/}
  say "Downloading images from $reg"
  docker pull "$reg/mssql-server:2022-latest" || die "Could not pull $reg/mssql-server:2022-latest. Run: docker login ${reg%%/*}"
  docker pull "$reg/amalki-web:latest" || die "Could not pull $reg/amalki-web:latest. Run: docker login ${reg%%/*}"
  docker tag "$reg/mssql-server:2022-latest" "$MSSQL_IMAGE"
  docker tag "$reg/amalki-web:latest" amalki-web:latest
  grep -q '^IMAGE_REGISTRY=' .env || echo "IMAGE_REGISTRY=$reg" >> .env
else
  if ! docker image inspect "$MSSQL_IMAGE" >/dev/null 2>&1; then
    say "Downloading SQL Server image (about 600 MB)"
    docker pull "$MSSQL_IMAGE" || need_ship "the SQL Server image"
  fi
  if [ "${SKIP_BUILD:-}" != "1" ] && ! docker image inspect amalki-web:shipped >/dev/null 2>&1; then
    say "Building the site image"
    docker compose build web || need_ship "the .NET images or NuGet packages for the build"
  else
    say "Using the site image sent from your computer"
    docker image inspect amalki-web:shipped >/dev/null 2>&1 && docker tag amalki-web:shipped amalki-web:latest
  fi
fi

# 5. Start.
say "Starting"
docker compose up -d --no-build
say "Waiting for the site (first start creates the database, up to 3 minutes)"
for _ in $(seq 1 36); do
  if curl -fsS --max-time 5 "http://localhost:${HTTP_PORT:-80}/healthz" >/dev/null 2>&1; then
    printf '\n\033[1;32mDone: %s\033[0m\n' "$SITE_URL"
    [ "${DEMO_MODE:-}" = "true" ] && echo "Test mode is on: use the yellow bar at the top to log in as admin or user."
    echo "Logs: cd $DIR && docker compose logs -f web"
    exit 0
  fi
  sleep 5
done
docker compose ps
docker compose logs --tail 60 web
die "The site did not answer on /healthz. The log above shows why."
