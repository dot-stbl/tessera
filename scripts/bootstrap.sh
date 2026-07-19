#!/usr/bin/env bash
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel 2>/dev/null || true)"
if [[ -z "$repo_root" ]]; then
    echo "Not inside a git repository." >&2
    exit 1
fi

cd "$repo_root"

current="$(git config --local --get core.ignorecase || true)"
if [[ "$current" == "false" ]]; then
    echo "core.ignorecase already false"
    exit 0
fi

if ! git config --local core.ignorecase false; then
    echo "git config failed." >&2
    exit 1
fi

was="${current:-unset}"
echo "Set core.ignorecase=false (was: $was)"