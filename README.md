# Shop POC

Backend .NET 10: sklep (moduł **Orders**), rozliczenia z bramką płatności DemoPay (**Settlements**) i moduł odczytowy
**DataHub**. Modularny monolit, event sourcing (Marten), komunikacja asynchroniczna (Wolverine), lokalnie Aspire.
Zasady pracy, architektura i decyzje: [CLAUDE.md](CLAUDE.md), [docs/architecture.md](docs/architecture.md), [docs/decisions.md](docs/decisions.md).

## Wymagania

* .NET SDK 10 (wersja z `global.json`),
* Docker (u autora: Rancher Desktop) – Postgres, Flyway, Keycloak i testy integracyjne działają w kontenerach,
* `jq` i `curl` – używają ich skrypty w `scripts/`,
* narzędzie `dotnet-ef` (tylko przy migracjach DataHub): `dotnet tool restore`.

## Uruchomienie lokalne

```bash
dotnet run --project src/Host/Shop.AppHost
```

AppHost (Aspire) uruchamia po kolei: Postgres (port `55432`), migracje Flyway każdego modułu (kolejność z `db/flyway-modules.json`),
Keycloak (port `8080`, realm `shop` importowany z `src/Host/Shop.AppHost/Realms/shop-realm.json`) i Host API (`http://localhost:5080`).
Panel Aspire: `http://localhost:15180` (adres z tokenem logowania wypisuje AppHost w konsoli).

Sprawdzenie, że wszystko działa:

```bash
curl http://localhost:5080/health                                   # 200
curl -i http://localhost:5080/api/orders -X POST                     # 401 – brak tokenu
TOKEN=$(scripts/dev/get-token.sh anna)
curl -i -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"customer":{"name":"Anna","email":"a@x.pl"},"lines":[{"sku":"S1","productName":"P","quantity":1,"unitPrice":10}]}' \
  http://localhost:5080/api/orders                                   # 500 "Etap 3" – zaślepka kontrolera (do czasu etapu 3)
```

## Użytkownicy testowi (tylko development)

Hasło każdego: `Pass123!`

| Użytkownik | Rola | Do czego |
|---|---|---|
| `anna` | `sales` | zamówienia (składanie, anulowanie, zwroty), odczyt płatności |
| `jan` | `accounting` | import raportów rozliczeniowych, rozbieżności, odczyt płatności |
| `demopay` | `gateway` | potwierdzenia płatności z bramki |

Token: `scripts/dev/get-token.sh <użytkownik>`. Klient `shop-gui` (Blazor) ma sekret deweloperski `shop-gui-dev-secret`.

## Najważniejsze polecenia

| Cel | Polecenie |
|---|---|
| Build (ostrzeżenia = błędy) | `dotnet build Shop.slnx` |
| Wszystkie testy (wymaga Dockera) | `dotnet test --solution Shop.slnx` |
| Testy jednego projektu | `dotnet test --project tests/Shop.ArchitectureTests` |
| Regeneracja kontraktu API | `scripts/api/generate-contracts.sh` |
| Migracje na lokalnej bazie | `scripts/db/migrate-local.sh` |
| Nowa migracja Martena/Wolverine | `scripts/db/new-migration.sh <moduł> <opis_snake_case>` |
| Odtworzenie lokalnej bazy od zera | `scripts/db/reset-local.sh` (pyta o potwierdzenie) |
| Zgodność bazy z konfiguracją | `dotnet run --project src/Host/Shop.Host -- db-assert` |

Komenda `db-assert` i skrypty `scripts/db/*` zakładają działający AppHost (baza na porcie `55432`).
Schemat bazy zmienia wyłącznie Flyway – aplikacja startuje z `AutoCreate.None`.
