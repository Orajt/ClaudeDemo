---
paths:
  - "src/Modules/**/*.Infrastructure/**"
---

# Warstwa Infrastructure

Infrastructure **tylko rejestruje i konfiguruje**. Zero logiki biznesowej. Jeśli piszesz tu `if` zależny od danych
domenowych albo mapowanie zdarzeń – to miejsce jest złe (przenieś do Application).

## Moduł zapisu (Marten)

Jedna klasa rozszerzeń `<Moduł>InfrastructureExtensions` oraz jedno rozszerzenie Wolverine:

* `AddMartenStore<I<Moduł>Store>(…)` z `DatabaseSchemaName = "<schemat modułu>"`,
  `AutoCreateSchemaObjects = AutoCreate.None`, rejestracją snapshotów agregatów (inline) i typów zdarzeń,
  `.IntegrateWithWolverine()`.
* `IWolverineExtension` modułu: discovery handlerów z assembly Application,
  `Policies.UseAncillaryStorageFromAssemblyContaining<…>(typeof(I<Moduł>Store))` – wszystkie handlery modułu
  zapisują przez store modułu, polityki ponowień specyficzne dla modułu.
* Marker store'a `I<Moduł>Store : IDocumentStore` mieszka tutaj.

## DataHub (EF Core)

* `AddDbContextWithWolverineIntegration<DataHubDbContext>(…)` z Npgsql, historia migracji EF w schemacie `datahub`.
* **Nie** używamy `UseEntityFrameworkCoreWolverineManagedMigrations()` – schemat robi Flyway.

## Zasady ogólne

* Konfiguracja czytana z `IConfiguration` (connection string `shop` dostarcza Aspire); bez sekretów w kodzie.
* Ustawienia wspólne dla wszystkich modułów (schemat `wolverine`, `MultipleHandlerBehavior.Separated`,
  trwałe kolejki lokalne) są w Host, nie powtarzaj ich w modułach.
* Każda niestandardowa opcja Martena/Wolverine ma komentarz „dlaczego”.
