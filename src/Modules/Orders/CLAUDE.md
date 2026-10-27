# Moduł Orders (Zamówienia)

Ten plik ładuje się, gdy pracujesz w `src/Modules/Orders/`. Opisuje język domeny i model.
Jeśli zmieniasz model (zdarzenie, regułę, zdarzenie integracyjne) – zaktualizuj ten plik w tym samym zadaniu.

## Słownik (PL → kod)

| Pojęcie | Kod | Znaczenie |
|---|---|---|
| Zamówienie | `Order` | agregat, strumień zdarzeń, id = Guid v7 |
| Pozycja zamówienia | `OrderLine` | SKU, nazwa produktu, ilość, cena jednostkowa |
| Klient | `Customer` | imię i nazwisko + e-mail (obiekt wartości wewnątrz zamówienia) |
| Wartość zamówienia | `TotalAmount` | suma pozycji, PLN, 2 miejsca po przecinku |
| Potwierdzenie płatności | `ConfirmPayment` | callback z bramki DemoPay (symulowany przez API) |
| Identyfikator transakcji bramki | `TransactionId` | np. `TX-1001`, nadaje bramka |
| Zwrot | `Refund` | zwrot całości kwoty; ma własny `RefundTransactionId`, np. `TX-R-1003` |
| Weryfikacja płatności | `PaymentReview` | stan, w którym rozliczenia wykryły rozbieżność; blokuje zwroty |

## Agregat `Order`

Stan (niemutowalny rekord, zmieniany wyłącznie przez `Apply`):

* `Id`, `Version` (ustawia Marten), `Customer`, `Lines`, `TotalAmount`
* `Status`: `AwaitingPayment` → `Paid` → `Refunded`; `AwaitingPayment` → `Cancelled`
* `Payment` (TransactionId, Amount, PaidAt) – po potwierdzeniu
* `Refund` (RefundTransactionId, Amount, RefundedAt) – po zwrocie
* `IsUnderPaymentReview` + `PaymentReviewReason`

## Zdarzenia domenowe

| Zdarzenie | Kiedy |
|---|---|
| `OrderPlaced` | złożono zamówienie |
| `PaymentConfirmed` | bramka potwierdziła płatność |
| `OrderCancelled` | anulowano przed płatnością |
| `OrderRefunded` | zwrócono całość kwoty |
| `PaymentReviewRequested` | rozliczenia zgłosiły rozbieżność dla płatności lub zwrotu tego zamówienia |

## Komendy i reguły

| Komenda | Wynik | Reguły (kod błędu przy naruszeniu → HTTP 422) |
|---|---|---|
| `PlaceOrder` | `OrderPlaced` | ≥ 1 pozycja; ilość 1–999; cena > 0; e-mail poprawny (walidacja → 400) |
| `ConfirmPayment` | `PaymentConfirmed` | status `AwaitingPayment` (`orders.payment-not-expected`); kwota = `TotalAmount` (`orders.payment-amount-mismatch`); **ponowny callback z tym samym `TransactionId` dla opłaconego zamówienia = brak zmian, sukces** (bramki ponawiają callbacki) |
| `CancelOrder` | `OrderCancelled` | tylko `AwaitingPayment` (`orders.cancel-not-allowed`) |
| `RefundOrder` | `OrderRefunded` | tylko `Paid` (`orders.refund-not-allowed`); nie w trakcie weryfikacji płatności (`orders.refund-blocked-by-payment-review`) |
| `RequestPaymentReview` (wewnętrzna, z integracji) | `PaymentReviewRequested` | jeśli już w weryfikacji – brak zmian (idempotencja) |

## Zdarzenia integracyjne – wychodzące (`Shop.Orders.Contracts`)

| Zdarzenie | Publikowane przy | Odbiorcy |
|---|---|---|
| `OrderPaidIntegrationEvent` | `PaymentConfirmed` | Settlements (oczekiwana płatność) |
| `OrderRefundedIntegrationEvent` | `OrderRefunded` | Settlements (oczekiwany zwrot) |
| `OrderStateChangedIntegrationEvent` | **każdej** zmianie strumienia `Order` | DataHub (`order_payments`) |

`OrderStateChangedIntegrationEvent` niesie: `OrderId`, `Version` (wersja strumienia po zmianie), dane klienta,
`TotalAmount`, `Status`, `PlacedAt`, `PaidAt`, `PaymentTransactionId`, `RefundedAt`, `RefundTransactionId`,
`IsUnderPaymentReview`. Budowany jedną metodą mapującą ze stanu agregatu **po** zmianie.

## Zdarzenia integracyjne – przychodzące

| Zdarzenie | Z modułu | Reakcja |
|---|---|---|
| `SettlementDiscrepancyDetectedIntegrationEvent` (z `OrderId`) | Settlements | komenda `RequestPaymentReview` |
