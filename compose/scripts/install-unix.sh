#!/usr/bin/env bash
# Tessera — Linux dev / single-node install script.
#
# Brings up the compose stack + smoke-tests /api/v1/health. Idempotent
# on re-run (docker compose up -d is idempotent by default). Pure
# Linux land: Phase 7 covers the Windows installer separately — see
# `.github/ISSUE_DRAFT_phase-7-windows.md`.
#
# Usage:
#   ./compose/scripts/install-unix.sh                # bring up + smoke
#   ./compose/scripts/install-unix.sh --detach        # identical to no flag
#   ./compose/scripts/install-unix.sh --rebuild       # rebuild images first
#   ./compose/scripts/install-unix.sh --teardown      # bring the stack down
#
# Environment:
#   TESSERA_IMAGE_OVERRIDE  override the tessera image tag
#                            (default: build from compose/Dockerfile)
#
# Exit codes:
#   0 success
#   1 docker compose unavailable or failed
#   2 health probe timed out (tessera container up but unhealthy)

set -eu

script_dir=$(cd "$(dirname "$0")" && pwd)
compose_file="$script_dir/../docker-compose.yml"
host_dir=$(cd "$script_dir/../.." && pwd)
docker_compose=(docker compose -f "$compose_file")

rebuild=0
teardown=0

for arg in "$@"; do
    case "$arg" in
    --rebuild)
        rebuild=1
        ;;
    --teardown)
        teardown=1
        ;;
    --detach)
        : # default — kept for explicit invocation
        ;;
    -h|--help)
        sed -n '3,21p' "$0" | sed 's/^# \?//'
        exit 0
        ;;
    *)
        printf 'unknown argument: %s\n' "$arg" >&2
        exit 1
        ;;
    esac
done

if [ "$teardown" -eq 1 ]; then
    "$docker_compose[@]" down
    exit 0
fi

compose_up_args=(--detach)
if [ "$rebuild" -eq 1 ]; then
    compose_up_args+=(--build)
fi

"${docker_compose[@]}" up "${compose_up_args[@]}"

# Health probe — tessera container /api/v1/health returns 200 once
# Tessera.Host + Victoria + OTel collector are wired.
probe_host=127.0.0.1
probe_port=1990
probe_path=/api/v1/health
deadline=$(( $(date +%s) + 60 ))

printf 'probing http://%s:%s%s ... ' "$probe_host" "$probe_port" "$probe_path"

while :; do
    if curl --fail --silent "http://${probe_host}:${probe_port}${probe_path}" >/dev/null; then
        echo "ok"
        echo "tessera compose stack is up."
        exit 0
    fi
    if [ "$(date +%s)" -ge "$deadline" ]; then
        echo "timeout (60s)"
        "${docker_compose[@]}" logs --tail 80 tessera || true
        exit 2
    fi
    sleep 2
done
