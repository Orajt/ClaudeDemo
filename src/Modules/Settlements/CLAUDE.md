# Moduł Settlements (Rozliczenia)

Ten plik ładuje się, gdy pracujesz w `src/Modules/Settlements/`. Opisuje język domeny i model.
Jeśli zmieniasz model – zaktualizuj ten plik w tym samym zadaniu.

## Słownik (PL → kod)

| Pojęcie | Kod | Znaczenie |
|---|---|---|
| Bramka płatności | DemoPay | fikcyjny operator płatności |
| Rozliczenie zamówienia | `OrderSettlement` | agregat, strumień o id = `OrderId`; transakcje bramki oczekiwane dla zamówienia i ich rozliczenie |
| Transakcja oczekiwana | `ExpectedTransaction` | płatność lub zwrot, o którym wiemy z modułu Orders i który bramka powinna rozliczyć |
| Raport rozliczeniowy | `SettlementReport` | agregat, jeden plik CSV od DemoPay, strumień o id = Guid v7 |
| Linia raportu | `SettlementReportLine` | jedna transakcja w raporcie |
| Kwota brutto / prowizja / netto | `GrossAmount` / `FeeAmount` / `NetAmount` | netto = brutto − prowizja |
| Uzgodnienie | `SettlementReconciliation` | porównanie raportu z transakcjami oczekiwanymi (czysta funkcja w Domain) |
| Rozbieżność | `SettlementDiscrepancy` | wynik uzgodnienia, który wymaga wyjaśnienia przez księgowość |

## Format raportu DemoPay (CSV, UTF-8, separator `;`, kropka dziesiętna)

```
transaction_id;merchant_reference;type;gross_amount;fee_amount;net_amount;currency;transaction_date;settlement_date
TX-1001;0199a6f0-...;SALE;199.99;2.80;197.19;PLN;2026-10-25;2026-10-27
TX-R-1003;0199a6f2-...;REFUND;-49.00;0.00;-49.00;PLN;2026-10-26;2026-10-27
```

* `type`: `SALE` (kwoty dodatnie) albo `REFUND` (brutto i netto ujemne).
* `merchant_reference` = `OrderId` – informacyjnie; **dopasowanie wyłącznie po `transaction_id`**.
* Wszystkie linie jednego pliku mają tę samą `settlement_date`. Nazwa pliku identyfikuje raport.

## Agregat `OrderSettlement` (id strumienia = `OrderId`)

Zdarzenia domenowe:

| Zdarzenie | Kiedy |
|---|---|
| `PaymentExpected` | Orders potwierdziło płatność (TransactionId, Amount, PaidAt) |
| `RefundExpected` | Orders wykonało zwrot (RefundTransactionId, Amount, RefundedAt) |
| `GatewayTransactionSettled` | linia raportu zgodna z transakcją oczekiwaną (ReportId, typ, brutto, prowizja, netto, data rozliczenia) |
| `SettlementDiscrepancyDetected` | linia raportu niezgodna (DiscrepancyId, ReportId, TransactionId, Kind, kwota oczekiwana, kwota z raportu) |

## Agregat `SettlementReport`

* `SettlementReportImported`: ReportId, FileName, SettlementDate, LineCount, TotalGross, TotalFee, TotalNet,
  MatchedCount, DiscrepancyCount, ImportedAt.

## Uzgodnienie – reguły (`SettlementReconciliation`, czysta funkcja)

Dla każdej linii raportu, w kolejności:

| Warunek | Wynik (`Kind`) |
|---|---|
| waluta ≠ PLN albo netto ≠ brutto − prowizja albo znak kwoty niezgodny z typem | `InvalidLine` |
| brak transakcji oczekiwanej o tym `transaction_id` | `UnknownTransaction` (bez `OrderId`) |
| transakcja już rozliczona wcześniejszym raportem | `AlreadySettled` |
| `type` niezgodny z rodzajem transakcji oczekiwanej (SALE ↔ płatność, REFUND ↔ zwrot) | `TypeMismatch` |
| \|brutto\| ≠ kwota oczekiwana | `AmountMismatch` |
| w przeciwnym razie | dopasowanie → `GatewayTransactionSettled` |

## Komendy i reguły

| Komenda | Źródło | Wynik | Reguły |
|---|---|---|---|
| `RegisterExpectedPayment` | `OrderPaidIntegrationEvent` | start strumienia + `PaymentExpected` | strumień już istnieje z tą samą transakcją → brak zmian (idempotencja) |
| `RegisterExpectedRefund` | `OrderRefundedIntegrationEvent` | `RefundExpected` | brak oczekiwanej płatności → wyjątek (Wolverine ponowi; kolejność zdarzeń może być różna) |
| `ImportSettlementReport` | API (upload CSV) | `SettlementReportImported` + zdarzenia w strumieniach `OrderSettlement` | nieprawidłowy format → 400 z numerami linii; plik o tej samej nazwie już zaimportowany → 409 (`settlements.report-already-imported`) |

Import działa w jednej transakcji: parsowanie → wczytanie `OrderSettlement` po `transaction_id` →
`SettlementReconciliation.Reconcile(...)` → zapis zdarzeń do wszystkich dotkniętych strumieni → zdarzenia integracyjne.

## Zdarzenia integracyjne – wychodzące (`Shop.Settlements.Contracts`)

| Zdarzenie | Publikowane przy | Odbiorcy |
|---|---|---|
| `SettlementDiscrepancyDetectedIntegrationEvent` | każdej rozbieżności (także `UnknownTransaction`, wtedy `OrderId = null`) | Orders (jeśli jest `OrderId`), DataHub (`settlement_discrepancies`) |
| `SettlementReportImportedIntegrationEvent` | imporcie raportu | DataHub (`settlement_reports`) |
| `OrderSettlementStateChangedIntegrationEvent` | **każdej** zmianie strumienia `OrderSettlement` | DataHub (`order_payments`) |

`OrderSettlementStateChangedIntegrationEvent` niesie: `OrderId`, `Version`, stan płatności (TransactionId,
oczekiwana kwota, rozliczenie: brutto/prowizja/netto/data/raport), analogicznie dla zwrotu, liczbę rozbieżności
i rodzaj ostatniej.

## Zdarzenia integracyjne – przychodzące

| Zdarzenie | Z modułu | Reakcja |
|---|---|---|
| `OrderPaidIntegrationEvent` | Orders | `RegisterExpectedPayment` |
| `OrderRefundedIntegrationEvent` | Orders | `RegisterExpectedRefund` |
