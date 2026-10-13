#!/usr/bin/env bash
# Restores the seeded demo dataset and the four demo accounts (docker-compose.demo.yml stack).
# Same effect as Admin -> "Reset demo data" in the UI. Only works when the API runs in demo mode.
set -euo pipefail
cd "$(dirname "$0")/.."
exec docker compose -f docker-compose.demo.yml run --rm api --reset-demo
