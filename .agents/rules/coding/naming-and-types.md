---
description: c# naming, sealed classes, record vs class, no redundant namespace prefix — type declaration rules
globs: ["**/*.cs"]
always: true
---

# Naming, sealed, record vs class, type references

Этот файл — правила декларации типов и имён. Логика конструкторов,
паттерн-матчинг, async, anti-patterns — в соседних файлах.

## 1. Naming

### Общие правила

- **Interfaces**: префикс `I` — `ITraceProvider`, `IVictoriaLogsClient`.
- **Без сокращений**: `trace`, `service`, `request` — не `tr`, `svc`, `req`.
- **Methods**: verb phrases — `GetTraceAsync`, `ListLogsAsync`.
- **Access modifiers**: всегда явно (включая `public` на членах интерфейса).
- **Lambda parameters**: осмысленные имена. **Никогда** одной буквы.
  Исключение — `_` (discard) для неиспользуемых параметров.

**Test:** при виде `u`, `x`, `tmp`, `req`, `resp`, `ct` — переименовать.

### Postfixes

**Главное правило:** суффикс ОБЯЗАН описывать роль / ответственность класса или DTO, не generic-категорию. Если суффикс не добавляет информации о том ЧТО класс делает — он запрещён.

**Запрещённые generic-категории** (описывают pattern, не роль — ВСЕГДА):
- `*Dto` — запрещён. Каждый Web API класс — это "data transfer object" в широком смысле. Суффикс не описывает роль. Используй `*Request` / `*Response` / `*Summary` / `*Detail` / etc.
- `*Model` — запрещён в DTO-имени. ДОПУСТИМ как `TraceEntity` в EF/DDD контексте (но у tessera нет entities).
- `*ViewModel` — запрещён всегда (тот же generic-pattern антипаттерн).
- `*Impl` — Java-стиль, не нужен в C#.

**Разрешённые role-specific суффиксы:**
- `*Manager` — управляет чем-то: `TraceManager`, `ConnectionManager`.
- `*Helper` — помогает с чем-то: `QueryHelper`, `JsonHelper`.
- `*Utility` / `*Util` — утилита для чего-то: `StringUtility`, `PathUtil`.
- `*Service` — сервис чего-то: `DiscoveryService`, `HealthService`.
- `*Provider`, `*Registry`, `*Factory` — конкретная ответственность.

**Суффиксы DTO / response envelope / query / event:**
| Суффикс | Когда использовать |
|---------|------------------|
| `*Request` | DTO входящего HTTP-запроса (body / query) |
| `*Response` | DTO исходящего HTTP-ответа (body) |
| `*Result` | результат внутренней операции |
| `*Handler` | конкретный handler, названный по тому что обрабатывает |
| `*Options` | IOptions-класс для конфигурации |

`tessera` DTO примеры:
- `ListTracesRequest` / `TraceSummary` / `TraceDetail` — НЕ `TraceDto`/`TraceModel`/`TraceService`
- `ListLogsRequest` / `LogEntry` / `LogsResponse` — НЕ `LogsDto`/`LogsModel`

### Async suffix

Все методы с `Task` / `ValueTask` / `Task<T>` / `ValueTask<T>` в return
type оканчиваются на `Async`. Без исключений. Простой проброс Task тоже
получает суффикс.

**Enforcement:** VSTHRD200 (severity=error).

### `Task` vs `ValueTask`

- **`Task<T>`** — IO, HTTP (всегда async).
- **`ValueTask<T>`** — может быть синхронной (кеш-хит).

### `ConfigureAwait` — запрещён в app code

```csharp
// ❌ Wrong — устаревший паттерн из .NET 4.x
await client.GetTraceAsync(id, cancellationToken).ConfigureAwait(false);

// ✅ Correct — .NET 8+ async/await не имеет накладных
await client.GetTraceAsync(id, cancellationToken);
```

**Enforcement:** CA2007 (severity=error in `.editorconfig`).

### Parameter naming

| Bad | Good |
|-----|------|
| `ct` | `cancellationToken` |
| `sp` | `serviceProvider` |
| `id` | `traceId`, `serviceName`, `tenantId` |
| `req` | `request` |
| `resp` | `response` |
| `msg` | `message` |
| `err` | `error` |

Исключение: переменная цикла с коротким скоупом — `foreach (var item in items)`.

### Lambda parameters

```csharp
// ✅ Correct
traces.Where(trace => trace.IsError)
      .Select(errorTrace => errorTrace.Id)
      .ToList();

// ❌ Wrong
traces.Where(t => t.IsError)
      .Select(x => x.Id)
      .ToList();
```

**Исключение для `_` (discard):** для неиспользуемых параметров.

### Private fields — NO underscore prefix

```csharp
// ✅ Best — primary constructor, параметр доступен по имени
public sealed class TraceHandler(IVictoriaTracesClient client, ILogger<TraceHandler> logger)
{
    public Task DoAsync() => client.GetTraceAsync(...);
}

// ❌ Wrong — explicit ctor + дублирующее private readonly
public sealed class TraceHandler : ITraceHandler
{
    private readonly IVictoriaTracesClient _client;
    private readonly ILogger<TraceHandler> _logger;
    // ... + копирование в ctor
}
```

---

## 2. `sealed` — REQUIRED на concrete classes

**Default:** `sealed` на каждом конкретном классе.

**Исключения (только эти два):**
1. `abstract` базовый класс.
2. Класс с документированной точкой расширения — наследник **существует** в кодовой базе или design-decision зафиксирован в ADR.

```csharp
// ✅ Default
public sealed class ListTracesHandler(IVictoriaTracesClient client) { ... }

// ✅ Abstract база — не sealed
public abstract class HandlerBase<T> { ... }
```

**Enforcement:** CA1852 (severity=error в `.editorconfig`).

---

## 3. Record vs class

**Default — `sealed class`.**

`record` используется **только** когда явно нужны:
- Value-equality.
- `with`-expressions.
- Сжатый синтаксис для immutable value-объектов.

```csharp
// ✅ Value object — record оправдан
public sealed record TimeRange(long StartUnixMs, long EndUnixMs);

// ✅ DTO без value-equality — class
public sealed class ListTracesRequest
{
    public required string Service { get; init; }
    public required long StartUnixMs { get; init; }
    public required long EndUnixMs { get; init; }
}
```

### Required members

```csharp
public required string TraceId { get; init; }
```

---

## 4. Type references — без redundant namespace prefix

Если тип уже в скоупе (текущий namespace или `using`) — **не** дублируй
namespace-prefix.

```csharp
namespace Tessera.Modules.Traces;

// ❌ Wrong — префикс избыточен
public sealed class ListTracesHandler : Handlers.ITraceHandler

// ✅ Correct — ITraceHandler уже в скоупе
public sealed class ListTracesHandler : ITraceHandler
```

Применимо ко всем type-references: сигнатуры классов, параметры primary
constructor, generic-аргументы, возвращаемые типы, field/property types.

**Префикс остаётся** (не redundant) когда:
- Тип НЕ в скоупе — добавить `using` или полный путь.
- `Type.Member` — статический member access.
- Вложенный тип `MyClass.NestedType` standalone не доступен.
- Disambiguation — в текущем скоупе есть другой тип с тем же именем.

---

## Связанные правила

- `constructors-and-fields.md` — primary constructor, private fields, constants
- `code-shape.md` — pattern matching, var, braces, XML docs
- `async-and-tasks.md` — async/await
- `anti-patterns.md` — enum anti-patterns, tuple ban, validation
- `naming-tessera-theme.md` — two-name system, theme words for internal naming

## Self-audit grep

```bash
# Class / record names ending in *Dto
rg -in "(class|record)\s+\w+Dto\b" src/ --type cs

# Class names ending in *Model, *Impl
rg -in "class\s+\w+(Model|Impl)\b" src/ --type cs

# Forbidden generic parameter name (use 'cancellationToken' not 'ct')
rg -n " ct\b" src/ --type cs

# Underscore-prefixed private fields (forbidden; use primary ctor)
rg -n "private\s+\w+_\w+\s*=" src/ --type cs
```

Любой результат grep'а = потенциальный violation. Перед merge'ем каждый результат
либо fixed (переименовать), либо подавлен в комментарии с обоснованием.
