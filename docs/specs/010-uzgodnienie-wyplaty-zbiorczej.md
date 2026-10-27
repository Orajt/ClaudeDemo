# 010 – Uzgodnienie wypłaty zbiorczej DemoPay

| | |
|---|---|
| **Status** | Szkic – do akceptacji w etapie 7 przygotowań (feature na demo) |
| **Moduły** | Settlements, Orders, DataHub |
| **Autor** | Marcin + Claude |
| **Akceptuje** | Marcin |
| **Powiązane** | 002 (Settlements), 003 (DataHub), decyzje D-005, D-006 |

## 1. Cel biznesowy

DemoPay nie przelewa pieniędzy za każdą transakcję osobno. Raz na kilka dni wykonuje **jeden przelew zbiorczy**
(sprzedaż minus prowizje minus zwroty) i przysyła **specyfikację wypłaty** – listę transakcji, za które płaci.
Księgowość musi potwierdzić, że kwota z wyciągu bankowego zgadza się ze specyfikacją, a każda pozycja specyfikacji
odpowiada transakcji, którą wcześniej rozliczyliśmy. Dopiero wtedy uznajemy, że środki za zamówienie wpłynęły.
Niezgodności trafiają do wyjaśnienia, a zamówienie przechodzi w weryfikację płatności.

## 2. Zakres

**W zakresie:**
- import specyfikacji wypłaty (CSV) razem z kwotą i datą przelewu z wyciągu bankowego,
- uzgodnienie linii z rozliczonymi transakcjami i uzgodnienie kwoty przelewu z tolerancją groszową,
- zapis wypłaty w nowym agregacie `Payout`, uznanie środków w strumieniach `OrderSettlement`,
- reakcja Orders na rozbieżność wypłaty dotyczącą zamówienia (istniejąca komenda `RequestPaymentReview`),
- DataHub: status złożony `FundsReceived`, referencja wypłaty i data wpływu w `order_payments`.

**Poza zakresem:**
- widok listy wypłat w DataHub i ekran GUI (slice 011),
- pokazywanie w DataHub wypłaty transakcji zwrotu (zwroty są uzgadniane, ale DataHub pokazuje wypłatę tylko dla płatności),
- automatyczne pobieranie wyciągu z banku, waluty inne niż PLN, korekty wypłat.

## 3. Nowe pojęcia

| Pojęcie (PL) | Kod (EN) | Znaczenie |
|---|---|---|
| Wypłata zbiorcza | `Payout` | jeden przelew DemoPay za wiele transakcji; agregat, strumień o id = Guid v7 |
| Referencja wypłaty | `PayoutReference` | identyfikator nadany przez DemoPay, np. `PO-20261028-01` |
| Specyfikacja wypłaty | `PayoutSpecification` | plik CSV z liniami: transakcja + kwota netto |
| Kwota oczekiwana | `ExpectedAmount` | suma netto wszystkich linii specyfikacji |
| Kwota otrzymana | `ReceivedAmount` | kwota przelewu z wyciągu bankowego, wprowadza księgowa |
| Różnica | `Difference` | `ReceivedAmount − ExpectedAmount` |
| Tolerancja groszowa | `RoundingTolerance` | 0,05 zł – różnice do tej wartości (włącznie) akceptujemy |
| Uznanie środków | `FundsReceived` | fakt, że pieniądze za transakcję wpłynęły na rachunek |

Format specyfikacji (UTF-8, separator `;`, kropka dziesiętna, nagłówek obowiązkowy):

```
payout_id;payout_date;transaction_id;net_amount;currency
PO-20261028-01;2026-10-28;TX-1001;197.19;PLN
PO-20261028-01;2026-10-28;TX-R-1003;-49.00;PLN
```

Wszystkie linie pliku mają ten sam `payout_id`, `payout_date` i walutę `PLN`.

## 4. Kryteria akceptacji

- **AC-1** Gdy przelew równa się sumie netto specyfikacji, a wszystkie linie są zgodne, wypłata ma status `Reconciled`,
  a każda transakcja z linii dostaje uznanie środków (`FundsReceived`).
- **AC-2** Gdy |różnica| > 0 i ≤ 0,05 zł, a linie są zgodne, status to `ReconciledWithRoundingDifference`, różnica jest
  zapisana, środki są uznane.
- **AC-3** Gdy |różnica| > 0,05 zł, status to `TransferMismatch` i **żadna** transakcja nie dostaje uznania środków.
- **AC-4** Linia z transakcją oczekiwaną, ale nierozliczoną raportem, daje rozbieżność `NotSettled`.
- **AC-5** Linia, której netto różni się od netto z rozliczenia, daje rozbieżność `NetAmountMismatch`.
- **AC-6** Linia z transakcją, która ma już uznane środki (w tej lub wcześniejszej wypłacie), daje rozbieżność `AlreadyPaidOut`.
- **AC-7** Linia z transakcją, której nie oczekujemy, daje rozbieżność `UnknownTransaction` bez zamówienia.
- **AC-8** Gdy przelew się zgadza (z tolerancją), ale co najmniej jedna linia ma rozbieżność, status to
  `ReconciledWithDiscrepancies`; zgodne linie dostają uznanie środków.
- **AC-9** Rozbieżność linii dotycząca zamówienia powoduje przejście zamówienia w weryfikację płatności (moduł Orders).
- **AC-10** Ponowny import wypłaty o tej samej referencji jest odrzucany: HTTP 409, kod `settlements.payout-already-imported`.
- **AC-11** Plik z błędem formatu (różne `payout_id` lub daty w liniach, waluta ≠ PLN, niepoprawna liczba, brak nagłówka)
  jest odrzucany: HTTP 400 z numerami błędnych linii; nic nie jest zapisywane.
- **AC-12** Linia zwrotu (ujemne netto) jest uzgadniana tak samo jak linia sprzedaży.
- **AC-13** W DataHub zamówienie, którego płatność ma uznane środki i nie ma rozbieżności ani zwrotu, ma status
  `FundsReceived` oraz wypełnione `payoutReference` i `fundsReceivedOn`.

## 5. Przykłady

Stan wyjściowy dla P-1…P-7 (po imporcie raportu z 27.10, zgodnie z danymi demo):

| Transakcja | Zamówienie | Rodzaj | Rozliczona? | Netto z rozliczenia |
|---|---|---|---|---|
| TX-1001 | A | płatność | tak | 197,19 |
| TX-1002 | B | płatność | tak | 48,01 |
| TX-1003 | C | płatność | tak | 48,01 |
| TX-R-1003 | C | zwrot | tak | −49,00 |
| TX-1004 | D | płatność | nie (rozbieżność kwoty w raporcie) | – |
| TX-1005 | E | płatność | nie (brak w raporcie) | – |

### P-1 (AC-1) Wypłata zgodna

| | |
|---|---|
| **Given** | stan wyjściowy |
| **When** | wypłata `PO-T1`: TX-1001 197,19; TX-1002 48,01; przelew 245,20 zł z 28.10 |
| **Then** | `PayoutReconciled`: oczekiwana 245,20, otrzymana 245,20, różnica 0,00, status `Reconciled`, 2 zgodne, 0 rozbieżności; `FundsReceived` w strumieniach A i B; HTTP 201 |

### P-2 (AC-2) Różnica groszowa

| | |
|---|---|
| **When** | jak P-1, przelew 245,17 zł |
| **Then** | różnica −0,03, status `ReconciledWithRoundingDifference`, `FundsReceived` dla A i B |

### P-3 (AC-3) Przelew niezgodny

| | |
|---|---|
| **When** | jak P-1, przelew 240,00 zł |
| **Then** | różnica −5,20, status `TransferMismatch`, brak `FundsReceived` |

### P-4 (AC-4, AC-8) Linia nierozliczona

| | |
|---|---|
| **When** | wypłata `PO-T4`: TX-1001 197,19; TX-1005 74,19; przelew 271,38 zł |
| **Then** | TX-1001 zgodna → `FundsReceived` (A); TX-1005 → `PayoutDiscrepancyDetected` (`NotSettled`, oczekiwane netto: brak, z pliku 74,19) w strumieniu E; status `ReconciledWithDiscrepancies` |

### P-5 (AC-5) Inna kwota netto

| | |
|---|---|
| **When** | wypłata `PO-T5`: TX-1002 48,10; przelew 48,10 zł |
| **Then** | `NetAmountMismatch` (oczekiwane 48,01, z pliku 48,10) dla B; status `ReconciledWithDiscrepancies`; brak `FundsReceived` |

### P-6 (AC-6) Transakcja już wypłacona

| | |
|---|---|
| **Given** | stan wyjściowy + zaimportowana wypłata z P-1 |
| **When** | wypłata `PO-T6`: TX-1001 197,19; przelew 197,19 zł |
| **Then** | `AlreadyPaidOut` dla A; status `ReconciledWithDiscrepancies` |

Ta sama transakcja dwa razy w jednym pliku: pierwsza linia zgodna, druga `AlreadyPaidOut`.

### P-7 (AC-7) Transakcja nieznana

| | |
|---|---|
| **When** | wypłata `PO-T7`: TX-9999 9,48; przelew 9,48 zł |
| **Then** | rozbieżność `UnknownTransaction` zapisana tylko w `PayoutReconciled` (bez strumienia zamówienia); status `ReconciledWithDiscrepancies` |

### P-8 (AC-10) Duplikat

| | |
|---|---|
| **Given** | zaimportowana wypłata `PO-T1` |
| **When** | ponowny import pliku z `payout_id = PO-T1` |
| **Then** | HTTP 409, `code = settlements.payout-already-imported`, brak nowych zdarzeń |

### P-9 (AC-11) Błędny plik

| | |
|---|---|
| **When** | linia 3 ma `payout_id = PO-INNE`, linia 4 ma `net_amount = abc` |
| **Then** | HTTP 400, `errors` zawiera „Linia 3: …” i „Linia 4: …”, brak zdarzeń |

### P-10 (AC-12) Zwrot w wypłacie

| | |
|---|---|
| **When** | wypłata `PO-T10`: TX-1001 197,19; TX-1003 48,01; TX-R-1003 −49,00; przelew 196,20 zł |
| **Then** | wszystkie linie zgodne, oczekiwana 196,20, status `Reconciled`, `FundsReceived` dla A oraz dla obu transakcji w strumieniu C |

### P-11 (AC-9, AC-13) Scenariusz demo end-to-end

| | |
|---|---|
| **Given** | dane demo (`scripts/demo/seed.sh`) + zaimportowany raport z 27.10 |
| **When** | wypłata `PO-20261028-01`: TX-1001 197,19; TX-1002 48,01; TX-1003 48,01; TX-R-1003 −49,00; TX-1005 74,19; przelew 318,40 zł z 28.10 |
| **Then** | oczekiwana 318,40, różnica 0,00, status `ReconciledWithDiscrepancies`, 4 zgodne, 1 rozbieżność (TX-1005 `NotSettled`); zamówienie E w weryfikacji płatności (Orders); DataHub: A i B → `FundsReceived` z `PO-20261028-01` i 2026-10-28, C → `RefundSettled`, D → `UnderReview`, E → `UnderReview`, F → `AwaitingPayment`, G → `Cancelled` |

## 6. Model domeny

### Komendy

| Komenda | Pola | Kto wywołuje |
|---|---|---|
| `ImportPayout` | `FileName`, `Content` (strumień), `ReceivedAmount`, `ReceivedOn` | API (rola accounting) |
| `RequestPaymentReview` (Orders, istniejąca) | bez zmian | reakcja na `PayoutDiscrepancyDetectedIntegrationEvent` |

### Zdarzenia domenowe

| Zdarzenie | Pola | Agregat (strumień) |
|---|---|---|
| `PayoutReconciled` | PayoutId, PayoutReference, PayoutDate, ReceivedOn, ExpectedAmount, ReceivedAmount, Difference, Status, LineCount, MatchedCount, Discrepancies (lista: TransactionId, OrderId?, Kind, ExpectedNetAmount?, ReportedNetAmount), ReconciledAt | `Payout` (nowy) |
| `FundsReceived` | TransactionId, PayoutId, PayoutReference, ReceivedOn, NetAmount | `OrderSettlement` |
| `PayoutDiscrepancyDetected` | DiscrepancyId, PayoutId, PayoutReference, TransactionId, Kind, ExpectedNetAmount?, ReportedNetAmount, DetectedAt | `OrderSettlement` |

### Reguły biznesowe

`PayoutReconciliation.Reconcile(specification, receivedAmount, receivedOn, orderSettlements)` – czysta funkcja w Domain.

Linie, w tej kolejności:

| # | Warunek | Wynik |
|---|---|---|
| 1 | brak `OrderSettlement` z tą transakcją (płatność albo zwrot) | `UnknownTransaction` |
| 2 | transakcja nierozliczona raportem | `NotSettled` |
| 3 | transakcja ma już uznane środki (także wcześniej w tym samym pliku) | `AlreadyPaidOut` |
| 4 | netto linii ≠ netto z rozliczenia | `NetAmountMismatch` |
| 5 | w przeciwnym razie | zgodna |

Wypłata:

| Warunek (kolejność = priorytet) | Status | Uznanie środków dla zgodnych linii |
|---|---|---|
| \|różnica\| > 0,05 | `TransferMismatch` | nie |
| są rozbieżności linii | `ReconciledWithDiscrepancies` | tak |
| 0 < \|różnica\| ≤ 0,05 | `ReconciledWithRoundingDifference` | tak |
| różnica = 0 | `Reconciled` | tak |

Rozbieżności linii są zapisywane zawsze, także przy `TransferMismatch`.

| Reguła | Kod błędu | HTTP |
|---|---|---|
| wypłata o tej referencji już zaimportowana | `settlements.payout-already-imported` | 409 |
| błąd formatu pliku | `validation-failed` (z `errors` per linia) | 400 |

### Zmiany agregatów

- `OrderSettlement`: dla płatności i zwrotu – informacja o uznaniu środków (referencja wypłaty, data wpływu);
  licznik rozbieżności obejmuje rozbieżności wypłat.
- `Payout`: nowy agregat, snapshot inline (potrzebny do wykrycia duplikatu referencji).

## 7. Zdarzenia integracyjne

| Zdarzenie | Nowe / zmienione | Pola | Producent | Konsumenci | Reakcja konsumenta |
|---|---|---|---|---|---|
| `PayoutDiscrepancyDetectedIntegrationEvent` | nowe | DiscrepancyId, PayoutId, PayoutReference, OrderId?, TransactionId, Kind, ExpectedNetAmount?, ReportedNetAmount, DetectedAt | Settlements | Orders | gdy jest `OrderId` → `RequestPaymentReview` (idempotentnie) |
| `OrderSettlementStateChangedIntegrationEvent` | zmienione – nowe pola nullable na końcu | + `PaymentPayoutReference`, + `PaymentFundsReceivedOn` (`DateOnly?`) | Settlements | DataHub | nowe kolumny w `order_payments` |

Rozbieżności wypłat z `UnknownTransaction` nie publikujemy (nie dotyczą żadnego zamówienia) – są w `PayoutReconciled`.

Enum rodzaju rozbieżności w `Shop.Settlements.Contracts` dostaje nowe wartości na końcu: `NotSettled`, `AlreadyPaidOut`,
`NetAmountMismatch` (zmiana kompatybilna). Dzięki temu „rodzaj ostatniej rozbieżności” w zdarzeniu stanu
i w DataHub obejmuje też rozbieżności wypłat.

## 8. Kontrakt API

```yaml
# delta do api/openapi/shop-api.yaml

# --- paths ---
  /api/payouts:
    post:
      tags: [Settlements]
      operationId: ImportPayout
      summary: Import specyfikacji wypłaty zbiorczej DemoPay i uzgodnienie z przelewem
      description: "Rola: accounting. Referencja wypłaty (payout_id z pliku) identyfikuje wypłatę – ponowny import zwraca 409."
      requestBody:
        required: true
        content:
          multipart/form-data:
            schema:
              type: object
              required: [file, receivedAmount, receivedOn]
              properties:
                file:
                  type: string
                  format: binary
                  description: Specyfikacja wypłaty DemoPay (CSV)
                receivedAmount:
                  type: number
                  description: Kwota przelewu z wyciągu bankowego (PLN)
                receivedOn:
                  type: string
                  format: date
                  description: Data zaksięgowania przelewu
      responses:
        '201':
          description: Wypłata zaimportowana i uzgodniona
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/PayoutImportedResponse'
        '400':
          $ref: '#/components/responses/ValidationFailed'
        '401':
          $ref: '#/components/responses/Unauthorized'
        '403':
          $ref: '#/components/responses/Forbidden'
        '409':
          $ref: '#/components/responses/Conflict'

# --- components/schemas: nowe ---
    PayoutStatus:
      type: string
      enum:
        - Reconciled
        - ReconciledWithRoundingDifference
        - ReconciledWithDiscrepancies
        - TransferMismatch

    PayoutImportedResponse:
      type: object
      required: [payoutId, payoutReference, status, expectedAmount, receivedAmount, difference, lineCount, matchedCount, discrepancyCount]
      properties:
        payoutId:
          type: string
          format: uuid
        payoutReference:
          type: string
        status:
          $ref: '#/components/schemas/PayoutStatus'
        expectedAmount:
          type: number
        receivedAmount:
          type: number
        difference:
          type: number
        lineCount:
          type: integer
          format: int32
        matchedCount:
          type: integer
          format: int32
        discrepancyCount:
          type: integer
          format: int32

# --- components/schemas: zmiany ---
# OrderPaymentStatus.enum: dodać FundsReceived (po Settled)
# DiscrepancyKind.enum: dodać na końcu NotSettled, AlreadyPaidOut, NetAmountMismatch
# OrderPaymentView.properties: dodać
        payoutReference:
          type: string
          nullable: true
        fundsReceivedOn:
          type: string
          format: date
          nullable: true
```

| Operacja | Rola | Odpowiedzi |
|---|---|---|
| `ImportPayout` | accounting | 201, 400, 401, 403, 409 |
| `ListOrderPayments`, `GetOrderPayment` | bez zmian | nowy status i pola w widoku |

## 9. Baza danych

| Schemat | Zmiana | Źródło migracji | Czy usuwa dane? |
|---|---|---|---|
| `settlements` | dokument snapshotu `Payout` (+ indeks na `PayoutReference`, unikalny) | Marten `db-patch` | nie |
| `datahub` | `order_payments`: kolumny `payout_reference` (text, null), `funds_received_on` (date, null) | EF → `migrations script` | nie |

## 10. DataHub

| Widok | Kolumny / zmiana | Źródło (zdarzenie) | Wpływ na status złożony |
|---|---|---|---|
| `order_payments` | `payout_reference`, `funds_received_on` (właściciel: Settlements) | `OrderSettlementStateChangedIntegrationEvent` | nowy status `FundsReceived` |

Nowa tabela priorytetów statusu złożonego (zmiana: `FundsReceived` między `Refunded` a `Settled`):
`UnderReview` > `Cancelled` > `RefundSettled` > `Refunded` > **`FundsReceived`** > `Settled` > `PaidAwaitingSettlement` > `AwaitingPayment`.

Zapytania: `ListOrderPayments` przyjmuje filtr `FundsReceived`; `GetOrderPayment` zwraca nowe pola.

## 11. Plan testów

| AC | Poziom | Test (opis zachowania) |
|---|---|---|
| AC-1 | domena | wypłata zgodna → `Reconciled`, wszystkie linie zgodne |
| AC-2 | domena | różnica −0,03 → `ReconciledWithRoundingDifference`; różnica 0,05 → nadal tolerowana; 0,06 → `TransferMismatch` |
| AC-3 | domena | `TransferMismatch` → brak linii do uznania środków |
| AC-4…AC-7 | domena | tabelaryczny test reguł linii w kolejności z sekcji 6 (w tym duplikat w jednym pliku) |
| AC-8 | domena | rozbieżność linii + zgodny przelew → `ReconciledWithDiscrepancies` |
| AC-12 | domena | linie zwrotu z ujemnym netto |
| AC-11 | aplikacja | parser: błędy z numerami linii, brak nagłówka, mieszane `payout_id`, waluta |
| AC-1, AC-10, AC-11 | integracja | `POST /api/payouts`: 201 z podsumowaniem; duplikat → 409; zły plik → 400 i brak zdarzeń |
| AC-9 | integracja | rozbieżność wypłaty → zamówienie w weryfikacji płatności (tracked session przez 2 moduły) |
| AC-13 | jednostkowy DataHub + integracja | resolver: `FundsReceived` i jego miejsce w priorytetach; scenariusz P-11 przez API |

## 12. GUI

Nie dotyczy (ekran wypłat w slice 011). Nowy status `FundsReceived` musi mieć etykietę „Środki otrzymane” i kolor
w `StatusLabels`.

## 13. Ryzyka i pytania otwarte

- [x] Czy przy `TransferMismatch` zapisywać rozbieżności linii? – Tak, to niezależne fakty.
- [x] Czy pokazywać w DataHub wypłatę zwrotów? – Nie w tym slice'ie.
- [ ] Tolerancja 0,05 zł – potwierdzić z księgowością (na potrzeby demo przyjęte).

## 14. Notatki z implementacji

<!-- Wypełnia Claude w trakcie /implement-slice. -->
