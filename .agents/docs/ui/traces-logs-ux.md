# Traces & Logs UX — stub

> Черновик. Детали и моки — отдельным обсуждением.  
> HTML-наброски (открыть в браузере): `wireframes/*.html`.

## Экраны

| Route | Что |
|-------|-----|
| `/traces` | Список traces: time range, service, status, duration → клик в detail |
| `/traces/{traceId}` | **Request view**: waterfall + correlated logs (union по `trace_id`) |
| `/logs` | Логи: filter + клик `trace_id` → request view |

## Главная идея

- Ключ = **`trace_id`**, не «три разных продукта».
- Detail = spans + logs вместе; если чего-то нет — banner, не 404.
- «Вау» позже: markers логов на span-баре (`●ERROR @+…ms`).
- Join только по id (`trace_id` / `span_id`), не по времени.

## Визуал (пока)

- Плотный список (instrument panel), B&W + deep red primary.
- Mono / tabular-nums для duration, id, timestamps.
- Цвет только для status / log level.

## Что есть в коде

- Pages: `web/apps/console/src/features/{traces,logs}/`
- APM: `web/apps/console/src/shared/ui/apm/` (waterfall, span-row, log-entry, …)
- Tokens: `web/apps/console/src/index.css`

## TODO (когда вернёмся)

- [ ] Density / keyboard / URL filters — уточнить
- [ ] Log markers on waterfall
- [ ] Degraded states (log-only / spans-only)
- [ ] Моки / MSW — отдельно
