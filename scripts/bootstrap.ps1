#!/usr/bin/env pwsh
$ErrorActionPreference = 'Stop'

$repoRoot = git rev-parse --show-toplevel 2>$null
if (-not $repoRoot) {
    Write-Error "Not inside a git repository."
    exit 1
}

Set-Location -LiteralPath $repoRoot

$current = git config --local --get core.ignorecase
if ($current -eq 'false') {
    Write-Host "core.ignorecase already false"
    exit 0
}

git config --local core.ignorecase false
if ($LASTEXITCODE -ne 0) {
    Write-Error "git config failed."
    exit 1
}

$was = if ([string]::IsNullOrEmpty($current)) { 'unset' } else { $current }
Write-Host "Set core.ignorecase=false (was: $was)"