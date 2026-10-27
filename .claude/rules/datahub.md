---
paths:
  - "src/Modules/DataHub/**"
---

# DataHub – zasady

DataHub buduje płaskie widoki do odczytu z zdarzeń integracyjnych i udostępnia je zapytaniami.
Procedura dodania lub zmiany widoku: skill `datahub-view`.

## Czego DataHub nie robi

* Nie przyjmuje komend biznesowych, nie publikuje zdarzeń, nie woła innych modułów, nie referencjonuje ich Domain.
* Nie podejmuje decyzji biznesowych. Jedyna „logika” to składanie danych i statusy złożone liczone czystymi funkcjami.

## Struktura

```
Shop.DataHub.Application/
  Persistence/       DataHubDbContext, konfiguracje encji (IEntityTypeConfiguration)
  OrderPayments/     OrderPaymentView, OrderPaymentStatusResolver, handlery projekcji, zapytania
  SettlementReports/ …
Shop.DataHub.Infrastructure/  tylko rejestracja DbContextu
Shop.DataHub.Migrations/      migracje EF (C#) + Sql/ dla Flyway
Shop.DataHub.Api/             kontroler zapytań
```

`DataHubDbContext` jest w Application, bo transakcyjny middleware Wolverine wymaga, żeby handler przyjmował
konkretny typ DbContextu (decyzja D-008).

## Widok stanu (1 wiersz = 1 encja źródłowa)

Handler zdarzenia `…StateChangedIntegrationEvent`:

1. wczytaj wiersz po kluczu (albo utwórz nowy z samym kluczem),
2. jeśli `event.Version <= wiersz.<Źródło>Version` → zakończ (duplikat lub stare zdarzenie),
3. nadpisz **wyłącznie kolumny należące do źródła** + jego wersję,
4. przelicz status złożony czystą funkcją (`…StatusResolver`) z obu połówek,
5. nie wołaj `SaveChangesAsync` – robi to middleware Wolverine.

Wiersz ma token współbieżności `xmin`. Konflikt (`DbUpdateConcurrencyException`) i wyścig wstawienia
(naruszenie klucza głównego) → ponowienie handlera (polityka w Infrastructure DataHub).

## Widok dziennika (1 wiersz = 1 fakt)

Insert, jeśli identyfikator faktu nie istnieje; w przeciwnym razie nic nie rób.

## Model i SQL

* Tabele i kolumny `snake_case`, schemat `datahub`, typy: kwoty `numeric(18,2)`, czasy `timestamptz`, daty `date`.
* Indeks pod każdy filtr i sortowanie używane przez zapytania API.
* Enumy zapisywane jako tekst.

## Zapytania

* Handler zapytania zwraca DTO (nie encję EF), używa `AsNoTracking()` i projekcji `Select`.
* Stronicowanie: `page` od 1, `pageSize` domyślnie 25, maksymalnie 100; sortowanie deterministyczne.
* Złączenia tylko wewnątrz schematu `datahub`.
