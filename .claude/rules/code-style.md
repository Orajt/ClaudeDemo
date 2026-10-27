# Styl kodu – czytelność przede wszystkim

Cel: programista, który pierwszy raz otwiera plik, rozumie **co** się dzieje z nazw, a **dlaczego** z komentarza.

## Funkcje

* Jedna funkcja = jeden poziom abstrakcji. Metoda publiczna czyta się jak lista kroków, szczegóły są w metodach prywatnych
  o nazwach opisujących intencję.
* Orientacyjnie do ~20 linii ciała. Dłuższa metoda to sygnał do podziału, nie twardy limit.
* Maksymalnie 3–4 parametry; więcej → rekord parametrów.
* Klauzule strażnicze (`if (...) throw/return`) zamiast zagnieżdżeń. Maksymalnie 2 poziomy wcięcia logiki.
* Bez efektów ubocznych ukrytych w nazwach typu `Get…`/`Is…`.

```csharp
// Dobrze: metoda publiczna jako lista kroków
public SettlementReconciliationResult Reconcile(SettlementReportFile report, IReadOnlyCollection<OrderSettlement> expected)
{
    var expectedByTransactionId = IndexByTransactionId(expected);
    var lineResults = report.Lines.Select(line => ReconcileLine(line, expectedByTransactionId)).ToList();
    return new SettlementReconciliationResult(report.ReportId, lineResults);
}
```

## Nazwy

* Nazwy z języka domeny (słownik w `CLAUDE.md` modułu). Bez skrótów (`transactionId`, nie `txId`).
* Klasy i rekordy: rzeczowniki. Metody: czasowniki. Zdarzenia: czas przeszły (`PaymentConfirmed`).
  Komendy: tryb rozkazujący (`ConfirmPayment`). Zdarzenia integracyjne: sufiks `IntegrationEvent`.
* Bool: `Is…`, `Has…`, `Can…`. Metody asynchroniczne: sufiks `Async`.
* Handler: `<Komenda>Handler`, walidator: `<Komenda>Validator`, test: `<Klasa>Tests`.

## Typy i dane

* Rekordy (`sealed record`) dla komend, zdarzeń, DTO i stanu agregatów. Klasy domyślnie `sealed`.
* Kwoty: `decimal`, waluta PLN, zaokrąglenie `Math.Round(x, 2, MidpointRounding.AwayFromZero)` tylko tam,
  gdzie reguła biznesowa tego wymaga.
* Czas: `DateTimeOffset` w UTC, z `TimeProvider` wstrzykiwanego do handlera. Domain dostaje czas jako parametr.
* Identyfikatory: `Guid.CreateVersion7()`.
* Brak „magicznych” liczb i napisów – stałe z nazwą (`const decimal RoundingTolerance = 0.05m;`).
* Kolekcje w API publicznym typów: `IReadOnlyList<T>` / `IReadOnlyCollection<T>`.

## Komentarze (po polsku)

* Komentarz wyjaśnia **dlaczego** albo **regułę biznesową**, nie powtarza kodu.
* Każda reguła biznesowa w Domain ma komentarz z jej sensem, np.
  `// Bramki ponawiają callbacki – ponowne potwierdzenie tej samej transakcji nie jest błędem.`
* Komentarz XML (`///`) na typach publicznych w `*.Contracts` – opisuje znaczenie zdarzenia dla odbiorców.
* Bez zakomentowanego kodu i bez komentarzy typu `// TODO` bez numeru zadania.

## Formatowanie i kompilator

* File-scoped namespaces, `var` gdy typ jest oczywisty, `Nullable` włączone, ostrzeżenia = błędy.
* Formatowanie wymusza `.editorconfig` i `dotnet format`; nie dyskutujemy o stylu w review.
* Wyjątki: tylko typy z `Shop.SharedKernel` lub własne, z kodem błędu. Nigdy `catch (Exception)` bez ponownego rzucenia.
* Żadnych refleksji, „sprytnych” generyków, extension methods na typach systemowych, jeśli prosty kod wystarcza.
