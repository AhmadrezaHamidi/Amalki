#!/usr/bin/env bash
# Only needed when the Arvan server cannot download images from mcr.microsoft.com (blocked from Iran).
# Run on your own computer (Docker installed, VPN on if needed), from the project folder:
#
#   bash deploy/arvan/ship-images.sh root@SERVER_IP
#
# It builds the site image and downloads SQL Server here, then streams both to the server over SSH.
# Then on the server: sudo bash deploy/arvan/install.sh   (it uses the shipped images and skips the build)
# On Windows run it from WSL or Git Bash with Docker Desktop.
set -euo pipefail

target=${1:?usage: bash deploy/arvan/ship-images.sh root@SERVER_IP}
cd "$(dirname "$0")/../.."

echo "==> Building the site image (linux/amd64)"
docker build --platform linux/amd64 -t amalki-web:shipped .
echo "==> Downloading SQL Server"
docker pull --platform linux/amd64 mcr.microsoft.com/mssql/server:2022-latest

echo "==> Sending images to $target (about 700 MB, takes a while)"
docker save amalki-web:shipped mcr.microsoft.com/mssql/server:2022-latest | gzip -1 |
  ssh "$target" 'gunzip | docker load'

echo "==> Done. On the server run:  cd Amalki && sudo bash deploy/arvan/install.sh"
