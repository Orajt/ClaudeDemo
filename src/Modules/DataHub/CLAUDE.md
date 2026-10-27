# Moduł DataHub (odczyt)

Ten plik ładuje się, gdy pracujesz w `src/Modules/DataHub/`. DataHub **tylko czyta i składa** dane.
Nie przyjmuje komend biznesowych, nie publikuje zdarzeń, nie woła innych modułów.
Szczegółowe standardy: `.claude/rules/datahub.md`. Procedura dodania widoku: skill `datahub-view`.

## Widoki (schemat `datahub`)

### `order_payments` – widok stanu, 1 wiersz na zamówienie

| Kolumny | Właściciel | Źródło |
|---|---|---|
| `order_id` (PK) | wspólny | oba zdarzenia stanu |
| `customer_name`, `customer_email`, `total_amount`, `order_status`, `placed_at`, `paid_at`, `payment_transaction_id`, `refunded_at`, `refund_transaction_id`, `is_under_payment_review`, `orders_version` | Orders | `OrderStateChangedIntegrationEvent` |
| `payment_settlement_status`, `settled_gross_amount`, `settled_fee_amount`, `settled_net_amount`, `settlement_date`, `settlement_report_id`, `refund_settlement_status`, `discrepancy_count`, `last_discrepancy_kind`, `settlement_version` | Settlements | `OrderSettlementStateChangedIntegrationEvent` |
| `payment_status` | DataHub | wyliczany funkcją `OrderPaymentStatusResolver` po każdej zmianie |
| `xmin` | Postgres | token współbieżności |

`payment_status` (status złożony, kolejność sprawdzania = priorytet):

| Status | Warunek |
|---|---|
| `UnderReview` | `discrepancy_count > 0` albo `is_under_payment_review` |
| `Cancelled` | zamówienie anulowane |
| `RefundSettled` | zwrot zrobiony i rozliczony przez bramkę |
| `Refunded` | zwrot zrobiony, nierozliczony |
| `Settled` | płatność rozliczona przez bramkę |
| `PaidAwaitingSettlement` | wiadomo, że zamówienie opłacono (z Orders albo z oczekiwanej płatności w Settlements), brak rozliczenia |
| `AwaitingPayment` | wszystkie pozostałe przypadki |

Każda połówka wiersza może dotrzeć jako pierwsza – resolver musi działać na niepełnych danych.

### `settlement_reports` – widok dziennika, 1 wiersz na zaimportowany raport

Źródło: `SettlementReportImportedIntegrationEvent`. Insert, jeśli `report_id` nie istnieje.

### `settlement_discrepancies` – widok dziennika, 1 wiersz na rozbieżność

Źródło: `SettlementDiscrepancyDetectedIntegrationEvent`. Insert, jeśli `discrepancy_id` nie istnieje.
Nazwa klienta w odpowiedzi API pochodzi ze złączenia z `order_payments` w zapytaniu (nie jest kopiowana).

## Zapytania (API, tag `DataHub`)

| Endpoint | Opis |
|---|---|
| `GET /api/datahub/order-payments` | lista ze stronicowaniem, filtr po `payment_status`, wyszukiwanie po kliencie / transakcji |
| `GET /api/datahub/order-payments/{orderId}` | szczegóły jednego zamówienia |
| `GET /api/datahub/settlement-reports` | lista raportów, najnowsze pierwsze |
| `GET /api/datahub/settlement-discrepancies` | lista rozbieżności, filtr po raporcie i rodzaju |
