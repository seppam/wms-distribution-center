#!/usr/bin/env bash
# Try the demo WITHOUT the full Docker stack: PostgreSQL runs in Docker (the only thing Docker is used for),
# the API runs with `dotnet run` and the web app with the Vite dev server, both in demo mode.
#
#   ./scripts/demo.sh            start everything, open http://localhost:5173, Ctrl+C stops it
#   ./scripts/demo.sh --reset    restore the demo dataset (API must be running) and exit
#
# Needs: Docker (database only), .NET SDK 9, Node.js 20.19+ or 22+.
# There is no Docker-free database option: the app uses PostgreSQL features (jsonb, migrations). Docker is the supported way.
# Demo mode: public accounts admin / supervisor / operator / viewer, shared PUBLIC password below. Never use real data.
set -euo pipefail
cd "$(dirname "$0")/.."

API_PORT="${API_PORT:-5300}"
WEB_PORT=5173            # fixed by frontend/vite.config.js
DB_PORT="${DEMO_DB_PORT:-5334}"
export DEMO_PASSWORD="${DEMO_PASSWORD:-Demo!Pass123}"
COMPOSE=(docker compose -f docker-compose.demo.yml)

die() { echo "demo.sh: $*" >&2; exit 1; }
need() { command -v "$1" >/dev/null 2>&1 || die "'$1' not found. $2"; }

need docker "Install Docker (it only runs the PostgreSQL database)."
need dotnet "Install the .NET SDK 9: https://dotnet.microsoft.com/download"
need node   "Install Node.js 22: https://nodejs.org"
need npm    "Install Node.js 22: https://nodejs.org"
[ "$(dotnet --version | cut -d. -f1)" -ge 9 ] || die ".NET SDK 9 or newer is required (found $(dotnet --version))."
node -e 'const [a,b]=process.versions.node.split(".").map(Number); process.exit(a>20||(a===20&&b>=19)?0:1)' \
  || die "Node.js 20.19+ or 22+ is required (found $(node -v))."
docker info >/dev/null 2>&1 || die "The Docker daemon is not running. Start Docker Desktop and retry."

busy() { lsof -iTCP:"$1" -sTCP:LISTEN -P -n >/dev/null 2>&1; }

export ASPNETCORE_ENVIRONMENT=Production
export ASPNETCORE_URLS="http://localhost:${API_PORT}"
export ConnectionStrings__DefaultConnection="Host=localhost;Port=${DB_PORT};Database=wms_demo;Username=wms_demo;Password=wms_demo_db"
export Database__MigrateOnStartup=true
export Demo__Enabled=true
export Demo__Password="$DEMO_PASSWORD"
export RateLimiting__AuthPermitLimit=60

if [ "${1:-}" = "--reset" ]; then
  export ASPNETCORE_URLS="http://127.0.0.1:0"   # the reset runs inside its own short-lived host, never clash with the running API
  exec dotnet run --project backend --configuration Release --no-launch-profile -- --reset-demo
fi

busy "$API_PORT" && die "Port $API_PORT is already in use. Stop that program or set API_PORT=<free port> (the Vite proxy expects $API_PORT; also set VITE_PROXY_TARGET)."
busy "$WEB_PORT" && die "Port $WEB_PORT is already in use. Stop that program first."

pids=()
# `dotnet run` and `npm run dev` start child processes: stop the whole tree we started (and only that)
kill_tree() {
  local child
  for child in $(pgrep -P "$1" 2>/dev/null || true); do kill_tree "$child"; done
  kill "$1" 2>/dev/null || true
}
cleanup() {
  trap - EXIT INT TERM
  echo; echo "Stopping the demo..."
  for p in "${pids[@]:-}"; do [ -n "$p" ] && kill_tree "$p"; done
  "${COMPOSE[@]}" stop db >/dev/null 2>&1 || true
}
trap cleanup EXIT INT TERM

echo "Starting PostgreSQL (Docker, port ${DB_PORT})..."
DEMO_DB_PORT="$DB_PORT" "${COMPOSE[@]}" up -d --wait db >/dev/null

echo "Starting the API (dotnet run, demo mode, port ${API_PORT})..."
dotnet run --project backend --configuration Release --no-launch-profile > demo-api.log 2>&1 &
pids+=("$!")

for i in $(seq 1 120); do
  curl -sf "http://localhost:${API_PORT}/readyz" >/dev/null 2>&1 && break
  kill -0 "${pids[0]}" 2>/dev/null || { cat demo-api.log; die "the API stopped, see output above."; }
  sleep 2
done
curl -sf "http://localhost:${API_PORT}/readyz" >/dev/null || die "the API did not become ready; see demo-api.log"
# the dataset is seeded while the host starts; wait until the demo-info endpoint answers
curl -sf "http://localhost:${API_PORT}/api/demo-info" | grep -q '"enabled":true' || die "the API is not in demo mode; see demo-api.log"

echo "Starting the web app (Vite dev server, port ${WEB_PORT})..."
(cd frontend && { [ -d node_modules ] || npm ci --no-audit --no-fund; } && VITE_DEMO_PASSWORD="$DEMO_PASSWORD" VITE_PROXY_TARGET="http://localhost:${API_PORT}" exec npm run dev -- --host 127.0.0.1) > demo-web.log 2>&1 &
pids+=("$!")
for i in $(seq 1 90); do curl -sf "http://localhost:${WEB_PORT}/" >/dev/null 2>&1 && break; sleep 2; done
curl -sf "http://localhost:${WEB_PORT}/" >/dev/null || die "the web app did not start; see demo-web.log"

cat <<MSG

  WMS demo is running:   http://localhost:${WEB_PORT}
  Sign in with the "Sign in as <role>" buttons on the login page (accounts: admin, supervisor, operator, viewer).
  Reset the data: Admin -> "Reset demo data", or  ./scripts/demo.sh --reset  (from another terminal)
  Logs: demo-api.log, demo-web.log.   Ctrl+C stops the API, the web app and the database container.

MSG
wait
