---
name: datahub-view
description: Procedura dodania lub zmiany widoku w module DataHub – encja widoku, konfiguracja EF, handler projekcji z kontrolą wersji, status złożony, zapytanie, migracja i testy. Używaj zawsze, gdy zadanie dotyczy danych do odczytu w DataHub.
---

# Widok DataHub

Zasady: `.claude/rules/datahub.md`. Istniejące widoki: `src/Modules/DataHub/CLAUDE.md`.

## 1. Ustal rodzaj widoku

| Pytanie | Widok stanu | Widok dziennika |
|---|---|---|
| Ile wierszy? | 1 na encję źródłową (np. zamówienie) | 1 na fakt (np. rozbieżność) |
| Zdarzenie źródłowe | `…StateChangedIntegrationEvent` z `Version` | fakt biznesowy z unikalnym Id |
| Idempotencja | wersja źródła w wierszu | istnienie Id |

Jeśli widok stanu potrzebuje danych, których zdarzenie stanu nie niesie – **najpierw** rozszerz zdarzenie
w module źródłowym (pole nullable na końcu) i jego mapowanie w Application tego modułu.

## 2. Encja widoku i konfiguracja

- `Application/<Obszar>/<Nazwa>View.cs` – klasa z właściwościami, pogrupowanymi komentarzem wg właściciela
  (`// Orders`, `// Settlements`, `// DataHub`), kolumny wersji `<Źródło>Version`.
- `Application/Persistence/Configurations/<Nazwa>ViewConfiguration.cs`: tabela `snake_case` w schemacie `datahub`,
  `numeric(18,2)` dla kwot, enumy jako tekst, token współbieżności `xmin`, indeksy pod filtry i sortowanie zapytań.

## 3. Handler projekcji (widok stanu)

```csharp
public static class OrderStateChangedProjection
{
    public static async Task Handle(OrderStateChangedIntegrationEvent changed, DataHubDbContext db, CancellationToken cancellationToken)
    {
        var view = await db.OrderPayments.FindAsync([changed.OrderId], cancellationToken)
                   ?? db.OrderPayments.Add(OrderPaymentView.Empty(changed.OrderId)).Entity;

        // Zdarzenia mogą przyjść w innej kolejności lub dwukrotnie – starsze ignorujemy.
        if (changed.Version <= view.OrdersVersion)
            return;

        view.ApplyOrderState(changed);
        view.PaymentStatus = OrderPaymentStatusResolver.Resolve(view);
    }
}
```

- Metoda `Apply<Źródło>State` na encji widoku nadpisuje **tylko** kolumny źródła i jego wersję.
- `…StatusResolver` – statyczna, czysta funkcja; działa przy niepełnych danych (każda połówka może przyjść pierwsza).
- `SaveChangesAsync` woła middleware Wolverine – nie wołaj go sam.

Widok dziennika: `if (await db.X.AnyAsync(x => x.Id == fact.Id, ct)) return;` → `db.X.Add(...)`.

## 4. Zapytanie i API

- `Application/<Obszar>/Queries/<Nazwa>Query.cs` + handler: `AsNoTracking`, projekcja `Select` do DTO,
  stronicowanie i deterministyczne sortowanie.
- Kontroler w `Shop.DataHub.Api` nadpisuje wygenerowaną metodę, wywołuje `bus.InvokeAsync<…>(query)`.
- Jeśli operacji brak w kontrakcie – zatrzymaj się (`/api-contract`).

## 5. Migracja

Skill `db-migration`, moduł `datahub`. Sprawdź w SQL token `xmin` (nie tworzy kolumny – to kolumna systemowa)
i indeksy.

## 6. Testy

- Jednostkowe (`tests/Shop.DataHub.Tests`): resolver statusu tabelarycznie (każdy wiersz tabeli statusów z `CLAUDE.md`
  DataHub + przypadki niepełnych danych), metody `Apply…State`.
- Integracyjne (`tests/Shop.IntegrationTests`): komenda w module źródłowym przez HTTP w tracked session →
  zapytanie DataHub przez HTTP zwraca oczekiwany wiersz; duplikat i starsza wersja zdarzenia nie zmieniają wiersza.

## 7. Dokumentacja

Zaktualizuj tabelę widoków w `src/Modules/DataHub/CLAUDE.md`.
