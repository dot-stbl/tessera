# Tessera integration tests

House rules (not global testing-integration.md):

1. **Unit** = cheap, always. **Integration** = Docker Victoria + fixed seeds + HTTP.
2. **Live lab** (vironima) is optional and separate — not the PR gate.
3. Integration is **opt-in**: set `TESSERA_IT=1` or tests skip.
4. Prefer **known fixtures** over “whatever is in the cluster”.
5. Stack definition lives in `stack/docker-compose.yml` — one way to bring up backends.

## Layout

```
tests/integration/
  README.md                 ← this file
  Tessera.Integration/      ← xUnit project
  stack/docker-compose.yml  ← VT + VL (+ VM later)
  seed/                     ← golden IDs + future seeder
```

## Run

```powershell
$env:TESSERA_IT = "1"
docker compose -f tests/integration/stack/docker-compose.yml up -d
dotnet test tests/integration/Tessera.Integration/Tessera.Integration.csproj
docker compose -f tests/integration/stack/docker-compose.yml down
```

Without `TESSERA_IT=1`, the project still builds and tests **skip** (exit 0).

## Wave status

See `.agents/plans/mvp-2-core/integration-tests/PLAN.md`.
