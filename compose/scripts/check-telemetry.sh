# Tessera — Linux runtime observability companion.
#
# Helper that mirrors `compose/scripts/install-unix.sh` but only
# checks the OTel pipeline is up — useful to verify a compose
# stack emit before digging into Tessera.Host config.
#
# Usage:
#   ./compose/scripts/check-telemetry.sh
#
# Assumes `docker compose up` has already been run (no-op if stack
# is already up).

set -eu

script_dir=$(cd "$(dirname "$0")" && pwd)
host_dir=$(cd "$script_dir/../.." && pwd)

echo "=== Tessera own signals (otel-collector status) ==="
docker compose -f "$script_dir/../docker-compose.yml" ps otel-collector

echo
echo "=== collector.log tail (last 20 lines) ==="
docker compose -f "$script_dir/../docker-compose.yml" logs --tail 20 otel-collector

echo
echo "=== tessera.log tail (last 20 lines) — looking for OTLP errors ==="
docker compose -f "$script_dir/../docker-compose.yml" logs --tail 20 tessera || true
