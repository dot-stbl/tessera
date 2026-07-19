# syntax=docker/dockerfile:1.7
#
# Tessera.Host multi-stage build.
#
# Stage 1 (build): restore + publish Tessera.Host (and its transitive
# deps) with the SDK image, producing a self-contained /app directory
# that contains Tessera.Host.dll + everything it needs to run.
#
# Stage 2 (runtime): copy the published artefacts onto the official
# aspnet-only runtime image (smaller surface area than the SDK image,
# no SDK bits in production).
#
# Build & run outside this repo:
#   docker build -f docker/Dockerfile -t ghcr.io/dot-stbl/tessera:latest .
#   docker run --rm -p 1990:1990 -v $PWD/tessera.toml:/etc/tessera/tessera.toml:ro \
#     -e TESSERA_ADMIN_TOKEN=... ghcr.io/dot-stbl/tessera:latest
#
# Compose variant (tessera + Victoria stack): see docker-compose.yml
# at the repository root.

# ----------------------------------------------------------------------------
# Stage 1: build
# ----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

# Disable NuGet HTTP traffic during restore; this image is air-gapped-friendly
# (the host build context provides no internet access by default for our CI).
ENV DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    NUGET_PACKAGES=/root/.nuget/packages \
    # The publish step writes restored package assets into obj/ — share
    # /tmp for restore cache with publish.
    DOTNET_CLI_HOME=/tmp/dotnet

WORKDIR /src

# Copy only the csproj manifests first so restore can be cached as a layer
# even when source edits don't change package versions.
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/host/Tessera.Build.Tools/Tessera.Build.Tools.csproj src/host/Tessera.Build.Tools/
COPY src/shared/Tessera.Shared.Kernel/Tessera.Shared.Kernel.csproj src/shared/Tessera.Shared.Kernel/
COPY src/shared/Tessera.Shared.Http/Tessera.Shared.Http.csproj src/shared/Tessera.Shared.Http/
COPY src/shared/Tessera.Shared.Web/Tessera.Shared.Web.csproj src/shared/Tessera.Shared.Web/
COPY src/shared/Tessera.Shared.Authentication/Tessera.Shared.Authentication.csproj src/shared/Tessera.Shared.Authentication/
COPY src/shared/Tessera.Shared.Validation/Tessera.Shared.Validation.csproj src/shared/Tessera.Shared.Validation/
COPY src/providers/Tessera.Providers.Victoria/Tessera.Providers.Victoria.csproj src/providers/Tessera.Providers.Victoria/
COPY src/modules/Tessera.Modules.Health/Tessera.Modules.Health.csproj src/modules/Tessera.Modules.Health/
COPY src/modules/Tessera.Modules.Discovery/Tessera.Modules.Discovery.csproj src/modules/Tessera.Modules.Discovery/
COPY src/modules/Tessera.Modules.Traces/Tessera.Modules.Traces.csproj src/modules/Tessera.Modules.Traces/
COPY src/modules/Tessera.Modules.Logs/Tessera.Modules.Logs.csproj src/modules/Tessera.Modules.Logs/
COPY src/host/Tessera.Host/Tessera.Host.csproj src/host/Tessera.Host/
COPY tests/unit/core/Tessera.Shared.Unit/Tessera.Shared.Unit.csproj tests/unit/core/Tessera.Shared.Unit/
COPY tests/unit/providers/Tessera.Providers.Victoria.Unit/Tessera.Providers.Victoria.Unit.csproj tests/unit/providers/Tessera.Providers.Victoria.Unit/

# Single restore touches the entire dependency graph — `dotnet restore`
# reads all csproj files referenced via ProjectReference transitively.
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore src/host/Tessera.Host/Tessera.Host.csproj

# Bring in the rest of the source now that restore is cached.
COPY . .

# Publish the host with `dotnet publish` (self-contained=false so the
# runtime image supplies the shared framework). /p:UseAppHost=false
# strips the apphost shim that .NET defaults in — we only need the
# .dll in the runtime image. Trimmed + ReadyToRun so the cold start is
# warm (faster first request at the cost of a slightly bigger image).
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/host/Tessera.Host/Tessera.Host.csproj \
        --configuration Release \
        --no-restore \
        --output /app \
        --no-self-contained \
        /p:UseAppHost=false \
        /p:PublishTrimmed=false \
        /p:PublishReadyToRun=true \
        /p:DebugType=embedded

# ----------------------------------------------------------------------------
# Stage 2: runtime
# ----------------------------------------------------------------------------
# Use the official ASP.NET runtime on Alpine (smaller than the Debian
# variant; 100MB vs 200MB). Tessera.Host targets net10.0 so the runtime
# tag must be 10.0-alpine to match.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

# Non-root user — runtime image ships a built-in `app` user; the host
# Dockerfile baked in pre-Phase 3 declares <ContainerUser>tessera</ContainerUser>
# but for hand-written runtime image we follow the official base-image
# convention.
WORKDIR /app
COPY --from=build --chown=app:app /app ./

# Compose/template mounts expect the production-style config directory
# at /etc/tessera by default. Default-bind-mount points in docker-compose.yml;
# the paths & TesseraConfigPaths.ResolveMainPath 4-level lookup also
# cover cwd + XDG_CONFIG_HOME + TESSERA_CONFIG env override.
ENV \
    # ASPNETCORE_URLS=http://+:1990 (per Tessera reserved port 1990,
    # coding/project-ports.md). + binds to all interfaces inside the
    # container.
    ASPNETCORE_URLS=http://+:1990 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    # TZ=UTC so any DateTimeOffset round-trips match VT/VL/VM native format
    # (which assumes UTC unix ms everywhere — see time-and-wire-format rule).
    TZ=UTC

EXPOSE 1990

# Health probe: the host exposes GET /api/v1/health (composite across
# wired providers). Use `--fail` so curl exits non-zero on any 5xx
# (TesseraExceptionHandler maps provider failures to 502/504).
HEALTHCHECK --interval=30s --timeout=3s --start-period=15s --retries=3 \
    CMD wget -q -O /dev/null http://127.0.0.1:1990/api/v1/health || exit 1

USER app
ENTRYPOINT ["dotnet", "Tessera.Host.dll"]
