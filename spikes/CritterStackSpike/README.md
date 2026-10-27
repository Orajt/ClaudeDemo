# Spike: Critter Stack (etap 1)

Samodzielny projekt poza przyszłym `Shop.slnx`. Udowadnia hipotezy H1–H9 z `prompts/01-spike-techniczny.md`.
Wyniki i wnioski: `docs/decisions.md` (D-009, D-012). Ten katalog to dowód, nie kod produkcyjny.

## Struktura

| Projekt | Rola |
|---|---|
| `src/Spike.Orders.Domain` | agregat `Order` (record) bez referencji do Martena – tylko `JasperFx.Events` + generator (H5, H7) |
| `src/Spike.Orders` | store `IOrdersStore` (schemat `spike_orders`), handlery `PlaceOrder`, `ConfirmPayment`, `CancelOrder` |
| `src/Spike.Settlements` | store `ISettlementsStore` (schemat `spike_settlements`), reakcja na `OrderPaid`, import raportu do wielu strumieni (H8) |
| `src/Spike.Contracts` | zdarzenia integracyjne |
| `src/Spike.DataHub` | `DataHubDbContext` (schemat `spike_datahub`), handlery EF, migracje EF, polityka ponawiania |
| `src/Spike.Host` | kompozycja (`SpikeComposition`), polecenia JasperFx, wygenerowany kod Wolverine (`Internal/Generated`) |
| `sql/` | SQL dla Flyway, po jednym katalogu na schemat; `sql/flyway-modules.json` w formacie `db/flyway-modules.json` |
| `tests/Spike.Tests` | 13 testów: Testcontainers (Postgres 18.6 + Flyway 13.11.1 w kontenerze) |

## Uruchomienie testów

Wymaga Dockera. Baza powstaje wyłącznie z `sql/` przez Flyway w kontenerze.

```bash
cd spikes/CritterStackSpike
dotnet build CritterStackSpike.slnx
dotnet test --project tests/Spike.Tests
```

## Regeneracja SQL (po zmianie konfiguracji Martena / Wolverine / modelu EF)

```bash
cd spikes/CritterStackSpike

# 1. Pusta baza deweloperska (Program.cs domyślnie łączy się z localhost:5433/spike)
docker run -d --name critter-spike-pg -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=spike -p 5433:5432 postgres:18.6-alpine

# 2. Identyfikatory baz
dotnet run --project src/Spike.Host -- db-list

# 3. Marten i Wolverine: -d to PEŁNY DatabaseUri (store Martena) albo SubjectUri (magazyn wiadomości).
#    Dopasowanie jest po początku URI – "spike_orders" nie zadziała, a "postgresql://localhost/spike" trafi w store Orders.
dotnet run --project src/Spike.Host -- db-patch /tmp/orders.sql      -d postgresql://localhost/spike/spike_orders
dotnet run --project src/Spike.Host -- db-patch /tmp/settlements.sql -d postgresql://localhost/spike/spike_settlements
dotnet run --project src/Spike.Host -- db-patch /tmp/wolverine.sql   -d wolverine://messages/main
#    Pliki *.drop.sql nie trafiają do Flyway.

# 4. DataHub: migracja EF → SQL bez START TRANSACTION/COMMIT (transakcją zarządza Flyway)
dotnet tool restore
dotnet ef migrations add <Nazwa> --project src/Spike.DataHub --startup-project src/Spike.Host --output-dir Migrations
dotnet ef migrations script <Poprzednia> <Nazwa> --no-transactions --project src/Spike.DataHub --startup-project src/Spike.Host --output /tmp/datahub.sql

# 5. Skopiuj jako sql/<schemat>/V<n>__<opis>.sql i sprawdź (Flyway + db-assert)
dotnet run --project src/Spike.Host -- db-assert
```

## Regeneracja kodu Wolverine (po każdej zmianie handlerów lub konfiguracji Wolverine)

Rdzeń Wolverine 6 nie zawiera kompilatora, a spike nie dodaje pakietu `WolverineFx.RuntimeCompilation`.
Kod handlerów jest więc generowany wcześniej (`TypeLoadMode.Static`). Polecenie trzeba uruchomić **z katalogu projektu
Host**, bo pliki trafiają do `<content root>/Internal/Generated`:

```bash
cd spikes/CritterStackSpike/src/Spike.Host
dotnet run -- codegen delete
dotnet run -- codegen write
```
