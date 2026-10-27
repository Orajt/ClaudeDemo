---
paths:
  - "src/Modules/**/*.Application/**"
---

# Warstwa Application

Application realizuje przypadki użycia: przyjmuje komendę lub zdarzenie integracyjne, pobiera stan, woła Domain,
zwraca zdarzenia domenowe i wiadomości wychodzące. To tu jest orkiestracja – reguły są w Domain.

## Struktura

Folder na przypadek użycia (vertical slice wewnątrz warstwy):

```
Shop.Orders.Application/
  ConfirmPayment/
    ConfirmPayment.cs            // komenda (sealed record)
    ConfirmPaymentValidator.cs   // FluentValidation – kształt danych
    ConfirmPaymentHandler.cs     // handler Wolverine
  IntegrationEvents/
    OrderIntegrationEvents.cs    // mapowanie stanu agregatu → zdarzenia integracyjne
  Reactions/
    SettlementDiscrepancyDetectedHandler.cs   // reakcja na zdarzenie z innego modułu
```

## Handler komendy na agregacie (Wolverine 6 + Marten 9)

* Statyczna klasa `<Komenda>Handler`, metoda `Handle`. Agregat jako parametr z `[WriteModel]` (agregat ładowany
  przez `FetchForWriting` z optymistyczną współbieżnością). Id agregatu w komendzie: właściwość `<Agregat>Id`.
* Zwracasz `(Events, OutgoingMessages)`: zdarzenia domenowe trafiają do strumienia, wiadomości do outboxu
  **w tej samej transakcji**. Nie wywołujesz `SaveChangesAsync` – robi to middleware (`AutoApplyTransactions`).
* Nowy strumień: `MartenOps.StartStream<TAgregat>(id, zdarzenia)`.
* Brak agregatu: Host ustawia globalnie `opts.EntityDefaults.OnMissing = OnMissing.ThrowException`, więc
  `[WriteModel]` rzuca `RequiredDataMissingException` (API → 404, reakcja na zdarzenie → ponowienie).
  **Nie nadpisuj tego na poziomie atrybutu** – domyślnie Wolverine w handlerze wiadomości po cichu porzuca komendę,
  a kontroler zwróciłby 204 dla nieistniejącego zamówienia.
* Czas z `TimeProvider` (parametr metody), nigdy `DateTime.Now`.
* **Nie mutujesz agregatu.** Stan po zmianie liczysz przez `aggregate.Apply(event)` – wyłącznie po to, żeby zbudować
  zdarzenie integracyjne o zmianie stanu.

```csharp
public static class ConfirmPaymentHandler
{
    public static (Events, OutgoingMessages) Handle(
        ConfirmPayment command,
        [WriteModel] Order order)
    {
        var paymentConfirmed = order.ConfirmPayment(command.TransactionId, command.Amount, command.PaidAt);

        // Ponowiony callback bramki – nic się nie zmienia, nic nie publikujemy.
        if (paymentConfirmed is null)
            return ([], []);

        var orderAfterChange = order.Apply(paymentConfirmed);

        return (
            [paymentConfirmed],
            [
                OrderIntegrationEvents.Paid(orderAfterChange),
                OrderIntegrationEvents.StateChanged(orderAfterChange, version: order.Version + 1)
            ]);
    }
}
```

Jeśli składnia z przykładu nie kompiluje się w zainstalowanej wersji Wolverine – sprawdź dokumentację tej wersji
i popraw przykład w tym pliku (reguły muszą odpowiadać rzeczywistości).

## Zdarzenia integracyjne

* Budujesz je **tylko** w `IntegrationEvents/<Agregat>IntegrationEvents.cs` – jedno miejsce mapowania na moduł.
* Zdarzenie zmiany stanu (`…StateChangedIntegrationEvent`) publikujesz przy **każdej** zmianie strumienia,
  z `Version` = wersja strumienia po dopisaniu zdarzeń (`wersja przed + liczba dopisanych zdarzeń`).
* Fakty biznesowe (`OrderPaidIntegrationEvent` itd.) publikujesz tylko wtedy, gdy ktoś ich potrzebuje
  (tabela w `CLAUDE.md` modułu).

## Reakcje na zdarzenia z innych modułów

* Handler zdarzenia integracyjnego zamienia je na komendę modułu i wykonuje ją przez zwrócenie wiadomości
  albo bezpośrednio (ten sam wzorzec co handler komendy). Logika reakcji = Domain.
* Każda reakcja jest **idempotentna** – to samo zdarzenie może przyjść dwa razy.
* Kolejność zdarzeń między modułami nie jest gwarantowana. Jeśli reakcja wymaga wcześniejszego faktu, którego
  jeszcze nie ma – rzuć wyjątek; Wolverine ponowi (polityka ponowień w Infrastructure).

## Walidacja

* FluentValidation (`<Komenda>Validator`) dla kształtu danych: wymagane pola, długości, formaty, zakresy.
  Wolverine uruchamia walidatory jako middleware – błąd → HTTP 400.
* Reguły zależne od stanu (np. „tylko opłacone zamówienie można zwrócić”) są w Domain, nie w walidatorze.

## Zapytania

Moduły zapisu nie wystawiają zapytań dla UI – od tego jest DataHub. Odczyt w Application tylko na potrzeby
decyzji (np. Settlements szuka `OrderSettlement` po `TransactionId` przez `IQuerySession`).

## Czego tu nie ma

* Konfiguracji Martena / Wolverine / DI (to Infrastructure).
* Typów HTTP (`IActionResult`, `HttpContext`) – to Api.
* Referencji do Domain/Application innego modułu – tylko jego `Contracts`.
