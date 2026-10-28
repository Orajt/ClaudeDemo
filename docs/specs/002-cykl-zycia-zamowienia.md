# 002 – Cykl życia zamówienia i płatności (moduł Orders, etap 3)

| | |
|---|---|
| **Status** | Zaakce[tpwama] |
| **Moduły** | Orders (Contracts, Domain, Application, Infrastructure, Migrations, Api) · testy |
| **Autor** | Marcin + Claude |
| **Akceptuje** | Marcin |
| **Powiązane** | `prompts/03-modul-orders.md`, `src/Modules/Orders/CLAUDE.md`, 001, decyzje D-003, D-005, D-006, D-010, D-012 |
| **Rekomendacja modelu** | informacyjnie: `sonnet`, effort `domyślny` – jeden moduł, więc domyślny model sesji (`opus` tylko dla zmian między modułami); warunkowo – dostępność modeli i poziomy effortu w `docs/model-policy.md` „do potwierdzenia” |

## 1. Cel biznesowy

Sprzedaż (rola `sales`) składa zamówienie klienta, a bramka DemoPay (rola `gateway`) potwierdza jego opłacenie.
Sprzedaż może anulować zamówienie, dopóki nie jest opłacone, i zwrócić całą kwotę opłaconego zamówienia.
Moduł Orders publikuje fakty o płatności i zwrocie (dla Settlements w etapie 4) oraz pełny stan zamówienia po każdej
zmianie (dla DataHub w etapie 5). Bramka ponawia callbacki – ponowne potwierdzenie tej samej transakcji nie może być błędem.

## 2. Zakres

**W zakresie:**
- agregat `Order` (strumień Martena w store `IOrdersStore`, snapshot inline) i zdarzenia domenowe `OrderPlaced`,
  `PaymentConfirmed`, `OrderCancelled`, `OrderRefunded`,
- komendy `PlaceOrder`, `ConfirmPayment` (z idempotencją ponowionego callbacku), `CancelOrder`, `RefundOrder` (zwrot całości kwoty),
  walidatory FluentValidation, handlery Wolverine,
- zdarzenia integracyjne `OrderPaidIntegrationEvent`, `OrderRefundedIntegrationEvent`, `OrderStateChangedIntegrationEvent`
  (`IsUnderPaymentReview` zawsze `false`),
- implementacja `OrdersController` dla czterech operacji istniejącego kontraktu (zastąpienie zaślepek),
- migracja Flyway schematu `orders` (tabele zdarzeń i snapshotu),
- nowy projekt testów `tests/Shop.Orders.Tests` (Domain, Application) + testy integracyjne w `tests/Shop.IntegrationTests`,
- aktualizacja testów etapu 2, które zakładały zaślepkę `OrdersController` („Etap 3”).

**Poza zakresem:**
- `RequestPaymentReview`, `PaymentReviewRequested`, reakcja na `SettlementDiscrepancyDetectedIntegrationEvent`
  i reguła `orders.refund-blocked-by-payment-review` (etap 4),
- odbiorcy zdarzeń integracyjnych – Settlements (etap 4), DataHub (etap 5); w tym slice'ie zdarzenia nie mają odbiorców,
- zwrot częściowy, zmiana pozycji zamówienia, odczyt zamówienia przez API Orders (odczyt = DataHub),
- zmiana `api/openapi/shop-api.yaml` (kontrakt bez zmian – sekcja 8), GUI (etap 6).

## 3. Nowe pojęcia

Pojęcia pochodzą ze słownika `src/Modules/Orders/CLAUDE.md`. Nowe w kodzie (nie w słowniku):

| Pojęcie (PL) | Kod (EN) | Znaczenie |
|---|---|---|
| Status zamówienia | `OrderStatus` | `AwaitingPayment`, `Paid`, `Cancelled`, `Refunded` – enum w Domain i osobny enum w Contracts (o tych samych wartościach) |
| Płatność | `Payment` | obiekt wartości w `Order`: `TransactionId`, `Amount`, `PaidAt` |
| Zwrot (stan) | `Refund` | obiekt wartości w `Order`: `RefundTransactionId`, `Amount`, `RefundedAt` |
| Wynik złożenia zamówienia | `OrderPlacedResult` | `OrderId`, `TotalAmount` – odpowiedź handlera `PlaceOrder` dla Api |

## 4. Kryteria akceptacji

**Składanie zamówienia (`PlaceOrder`)**
- **AC-1** Gdy `sales` składa poprawne zamówienie, to powstaje nowy strumień `Order` (id = Guid v7) ze zdarzeniem `OrderPlaced`
  (klient, pozycje, `TotalAmount`, `PlacedAt` = bieżący czas z `TimeProvider`), status `AwaitingPayment`,
  a API zwraca `201` z `orderId` i `totalAmount`.
- **AC-2** `TotalAmount` = suma `Quantity × UnitPrice` wszystkich pozycji, bez zaokrąglania (ceny mają maks. 2 miejsca po przecinku – AC-3).
- **AC-3** Gdy pozycje mają niepoprawny kształt (brak pozycji, więcej niż 50 pozycji, ilość spoza 1–999, cena ≤ 0, cena z więcej niż
  2 miejscami po przecinku, pusty lub dłuższy niż 50 znaków `sku`, pusta lub dłuższa niż 200 znaków `productName`), to API zwraca `400`
  `validation-failed` z `errors`, a strumień nie powstaje.
- **AC-4** Gdy dane klienta są niepoprawne (puste lub dłuższe niż 200 znaków `name`; pusty, niepoprawny lub dłuższy niż 200 znaków `email`),
  to API zwraca `400` `validation-failed` z `errors`, a strumień nie powstaje.
- **AC-5** Ten sam SKU w dwóch pozycjach jest dozwolony – to dwie osobne pozycje zamówienia.

**Potwierdzenie płatności (`ConfirmPayment`)**
- **AC-6** Gdy zamówienie ma status `AwaitingPayment`, a `amount` = `TotalAmount`, to do strumienia trafia `PaymentConfirmed`
  (`TransactionId`, `Amount`, `PaidAt` z żądania), status zmienia się na `Paid`, a API zwraca `204`.
- **AC-7** Gdy `amount` ≠ `TotalAmount` (porównanie dokładne `decimal`), to API zwraca `422` `orders.payment-amount-mismatch`, a strumień się nie zmienia.
- **AC-8** Gdy zamówienie nie ma statusu `AwaitingPayment` i nie zostało opłacone transakcją o tym samym `TransactionId`
  (status `Cancelled` albo opłacone inną transakcją), to API zwraca `422` `orders.payment-not-expected`, a strumień się nie zmienia.
- **AC-9** Gdy zamówienie zostało już opłacone transakcją o tym samym `TransactionId` (status `Paid` albo `Refunded`), to API zwraca `204`,
  do strumienia nie trafia żadne zdarzenie i nie jest publikowane żadne zdarzenie integracyjne – niezależnie od `amount` i `paidAt` w ponowieniu.
- **AC-10** Gdy `transactionId` jest pusty lub dłuższy niż 50 znaków, `amount` ≤ 0 albo brak `paidAt`, to API zwraca `400` `validation-failed`.

**Anulowanie (`CancelOrder`)**
- **AC-11** Gdy zamówienie ma status `AwaitingPayment`, to do strumienia trafia `OrderCancelled` (`Reason`, `CancelledAt` = bieżący czas
  z `TimeProvider`), status zmienia się na `Cancelled`, a API zwraca `204`.
- **AC-12** Gdy zamówienie ma inny status niż `AwaitingPayment` (`Paid`, `Cancelled`, `Refunded`), to API zwraca `422` `orders.cancel-not-allowed`,
  a strumień się nie zmienia.
- **AC-13** Gdy `reason` jest pusty lub dłuższy niż 500 znaków, to API zwraca `400` `validation-failed`.

**Zwrot (`RefundOrder`)**
- **AC-14** Gdy zamówienie ma status `Paid`, to do strumienia trafia `OrderRefunded` (`RefundTransactionId`, `Amount` = kwota płatności,
  `Reason`, `RefundedAt` z żądania), status zmienia się na `Refunded`, a API zwraca `204`.
- **AC-15** Gdy zamówienie ma inny status niż `Paid` (`AwaitingPayment`, `Cancelled`, `Refunded` – także przy ponowieniu z tym samym
  `RefundTransactionId`), to API zwraca `422` `orders.refund-not-allowed`, a strumień się nie zmienia.
- **AC-16** Gdy `refundTransactionId` jest pusty lub dłuższy niż 50 znaków, `reason` pusty lub dłuższy niż 500 znaków albo brak `refundedAt`,
  to API zwraca `400` `validation-failed`.

**Zdarzenia integracyjne**
- **AC-17** Przy każdej zmianie strumienia `Order` (AC-1, AC-6, AC-11, AC-14) publikowane jest dokładnie jedno
  `OrderStateChangedIntegrationEvent` ze stanem **po** zmianie i `Version` = wersja strumienia po dopisaniu zdarzeń
  (`wersja przed + liczba dopisanych zdarzeń`, D-005); `IsUnderPaymentReview` = `false`.
- **AC-18** Przy `PaymentConfirmed` publikowane jest `OrderPaidIntegrationEvent` (`OrderId`, `TransactionId`, `Amount`, `PaidAt`).
- **AC-19** Przy `OrderRefunded` publikowane jest `OrderRefundedIntegrationEvent` (`OrderId`, `RefundTransactionId`, `Amount`, `RefundedAt`).
- **AC-20** Zdarzenia integracyjne trafiają do outboxu w tej samej transakcji co zdarzenia domenowe; komenda odrzucona (400, 404, 422)
  nie publikuje żadnego zdarzenia integracyjnego.

**Wspólne**
- **AC-21** Gdy zamówienie o danym `orderId` nie istnieje, to `ConfirmPayment`, `CancelOrder` i `RefundOrder` zwracają `404` `not-found`.
- **AC-22** `PlaceOrder`, `CancelOrder`, `RefundOrder` wymagają roli `sales`, a `ConfirmPayment` roli `gateway`; inna rola → `403`.
- **AC-23** Schemat `orders` po migracjach Flyway nie ma różnic względem konfiguracji store'a `IOrdersStore`
  (test zgodności schematu zielony), a nowa migracja nie zawiera `DROP` ani `TRUNCATE`.

## 5. Przykłady

Dane wspólne:

| Symbol | Wartość |
|---|---|
| `O-1` | `orderId` = `0199a6f0-1001-7000-8000-000000000001` |
| Klient | „Anna Nowak”, `anna@example.com` |
| Pozycja 1 | `MUG-01`, „Kubek DemoPay”, 2 × 49,99 = 99,98 |
| Pozycja 2 | `TS-01`, „Koszulka”, 1 × 100,01 = 100,01 |
| `TotalAmount` | 99,98 + 100,01 = **199,99** |
| Czas złożenia (`TimeProvider`) | `2026-10-25T10:00:00Z` |

Stany wyjściowe (strumień `O-1`):

| Stan | Zdarzenia w strumieniu | Wersja |
|---|---|---|
| **S-Awaiting** | `OrderPlaced(O-1, Anna Nowak, [MUG-01 ×2 @49.99, TS-01 ×1 @100.01], 199.99, 2026-10-25T10:00:00Z)` | 1 |
| **S-Paid** | S-Awaiting + `PaymentConfirmed(TX-1001, 199.99, 2026-10-25T10:05:00Z)` | 2 |
| **S-Cancelled** | S-Awaiting + `OrderCancelled("Klient zrezygnował", 2026-10-25T11:00:00Z)` | 2 |
| **S-Refunded** | S-Paid + `OrderRefunded(TX-R-1001, 199.99, "Reklamacja", 2026-10-26T09:00:00Z)` | 3 |

### P-1 (AC-1, AC-2, AC-17) Złożenie zamówienia Anny Nowak

| | |
|---|---|
| **Given** | brak strumienia; `TimeProvider` = `2026-10-25T10:00:00Z`; rola `sales` |
| **When** | `POST /api/orders` `{"customer":{"name":"Anna Nowak","email":"anna@example.com"},"lines":[{"sku":"MUG-01","productName":"Kubek DemoPay","quantity":2,"unitPrice":49.99},{"sku":"TS-01","productName":"Koszulka","quantity":1,"unitPrice":100.01}]}` |
| **Then** | `201` `{"orderId":"<nowy Guid v7>","totalAmount":199.99}`; strumień = S-Awaiting; `OrderStateChangedIntegrationEvent(OrderId, Version=1, "Anna Nowak", "anna@example.com", 199.99, AwaitingPayment, PlacedAt=2026-10-25T10:00:00Z, PaidAt=null, PaymentTransactionId=null, RefundedAt=null, RefundTransactionId=null, IsUnderPaymentReview=false)`; brak `OrderPaidIntegrationEvent` |

### P-2 (AC-3) Niepoprawne pozycje

| Przypadek | `lines` | Wynik |
|---|---|---|
| brak pozycji | `[]` | `400`, błąd dla `Lines` |
| 51 pozycji | 51 × `{"sku":"MUG-01",…,"quantity":1,"unitPrice":49.99}` | `400`, błąd dla `Lines` |
| ilość 0 | `MUG-01`, `quantity: 0` | `400`, błąd dla `Lines[0].Quantity` |
| ilość 1000 | `MUG-01`, `quantity: 1000` | `400`, błąd dla `Lines[0].Quantity` |
| cena 0 | `MUG-01`, `unitPrice: 0` | `400`, błąd dla `Lines[0].UnitPrice` |
| 3 miejsca po przecinku | `MUG-01`, `unitPrice: 49.999` | `400`, błąd dla `Lines[0].UnitPrice` |
| pusty SKU | `sku: ""` | `400`, błąd dla `Lines[0].Sku` |
| pusta nazwa | `productName: ""` | `400`, błąd dla `Lines[0].ProductName` |

W każdym przypadku żaden strumień nie powstaje i nie ma zdarzeń integracyjnych (AC-20).

### P-3 (AC-4) Niepoprawny klient

| Przypadek | `customer` | Wynik |
|---|---|---|
| pusta nazwa | `{"name":"","email":"anna@example.com"}` | `400`, błąd dla `Customer.Name` |
| e-mail bez `@` | `{"name":"Anna Nowak","email":"anna.example.com"}` | `400`, błąd dla `Customer.Email` |
| nazwa 201 znaków | `{"name":"<201 × 'a'>","email":"anna@example.com"}` | `400`, błąd dla `Customer.Name` |

### P-4 (AC-5, AC-2) Powtórzony SKU

| | |
|---|---|
| **Given** | brak strumienia |
| **When** | `PlaceOrder` z pozycjami `MUG-01 ×2 @49.99` i `MUG-01 ×1 @49.99` |
| **Then** | `OrderPlaced` z dwiema pozycjami; `TotalAmount` = 99,98 + 49,99 = **149,97** |

### P-5 (AC-6, AC-17, AC-18) Potwierdzenie płatności TX-1001

| | |
|---|---|
| **Given** | S-Awaiting (wersja 1) |
| **When** | rola `gateway`: `POST /api/orders/O-1/payment-confirmation` `{"transactionId":"TX-1001","amount":199.99,"paidAt":"2026-10-25T10:05:00Z"}` |
| **Then** | `204`; strumień = S-Paid (wersja 2); `OrderPaidIntegrationEvent(O-1, TX-1001, 199.99, 2026-10-25T10:05:00Z)`; `OrderStateChangedIntegrationEvent(Version=2, Status=Paid, PaidAt=2026-10-25T10:05:00Z, PaymentTransactionId=TX-1001, RefundedAt=null, RefundTransactionId=null, IsUnderPaymentReview=false, …)` |

### P-6 (AC-7) Kwota niezgodna z wartością zamówienia

| | |
|---|---|
| **Given** | S-Awaiting |
| **When** | `ConfirmPayment(O-1, TX-1001, amount=199.98, 2026-10-25T10:05:00Z)` |
| **Then** | `422` `orders.payment-amount-mismatch`; strumień w wersji 1; brak zdarzeń integracyjnych |

### P-7 (AC-8) Płatność nieoczekiwana

| Given | When | Then |
|---|---|---|
| S-Cancelled | `ConfirmPayment(O-1, TX-1001, 199.99, …)` | `422` `orders.payment-not-expected`, strumień w wersji 2 |
| S-Paid (TX-1001) | `ConfirmPayment(O-1, TX-1002, 199.99, …)` | `422` `orders.payment-not-expected`, strumień w wersji 2 |
| S-Refunded (TX-1001) | `ConfirmPayment(O-1, TX-1002, 199.99, …)` | `422` `orders.payment-not-expected`, strumień w wersji 3 |

### P-8 (AC-9) Ponowiony callback bramki

| Given | When | Then |
|---|---|---|
| S-Paid (TX-1001) | `ConfirmPayment(O-1, TX-1001, 199.99, 2026-10-25T10:05:00Z)` | `204`, strumień w wersji 2, brak zdarzeń integracyjnych |
| S-Paid (TX-1001) | `ConfirmPayment(O-1, TX-1001, 150.00, 2026-10-25T10:07:00Z)` | `204`, strumień w wersji 2, `Payment.Amount` nadal 199,99, brak zdarzeń integracyjnych |
| S-Refunded (TX-1001) | `ConfirmPayment(O-1, TX-1001, 199.99, …)` | `204`, strumień w wersji 3, brak zdarzeń integracyjnych |

### P-9 (AC-10) Niepoprawne potwierdzenie płatności

| Przypadek | Body | Wynik |
|---|---|---|
| pusty identyfikator | `{"transactionId":"","amount":199.99,"paidAt":"2026-10-25T10:05:00Z"}` | `400`, błąd dla `TransactionId` |
| identyfikator 51 znaków | `transactionId` = `"TX-"` + 48 × `1` | `400`, błąd dla `TransactionId` |
| kwota 0 | `{"transactionId":"TX-1001","amount":0,"paidAt":"…"}` | `400`, błąd dla `Amount` |
| kwota ujemna | `amount: -199.99` | `400`, błąd dla `Amount` |
| brak daty | `paidAt` = wartość domyślna (`0001-01-01T00:00:00Z`) | `400`, błąd dla `PaidAt` |

### P-10 (AC-11, AC-17) Anulowanie nieopłaconego zamówienia

| | |
|---|---|
| **Given** | S-Awaiting; `TimeProvider` = `2026-10-25T11:00:00Z` |
| **When** | rola `sales`: `POST /api/orders/O-1/cancellation` `{"reason":"Klient zrezygnował"}` |
| **Then** | `204`; strumień = S-Cancelled (wersja 2); `OrderStateChangedIntegrationEvent(Version=2, Status=Cancelled, PaidAt=null, …)`; brak `OrderPaid`/`OrderRefunded` |

### P-11 (AC-12) Anulowanie niedozwolone

| Given | When | Then |
|---|---|---|
| S-Paid | `CancelOrder(O-1, "Pomyłka")` | `422` `orders.cancel-not-allowed`, wersja 2 |
| S-Cancelled | `CancelOrder(O-1, "Ponownie")` | `422` `orders.cancel-not-allowed`, wersja 2 |
| S-Refunded | `CancelOrder(O-1, "Pomyłka")` | `422` `orders.cancel-not-allowed`, wersja 3 |

### P-12 (AC-13) Niepoprawny powód anulowania

`{"reason":""}` → `400` (błąd dla `Reason`); `reason` = 501 × `a` → `400`.

### P-13 (AC-14, AC-17, AC-19) Zwrot TX-R-1001

| | |
|---|---|
| **Given** | S-Paid (wersja 2, `Payment.Amount` = 199,99) |
| **When** | rola `sales`: `POST /api/orders/O-1/refund` `{"refundTransactionId":"TX-R-1001","reason":"Reklamacja","refundedAt":"2026-10-26T09:00:00Z"}` |
| **Then** | `204`; strumień = S-Refunded (wersja 3); `OrderRefundedIntegrationEvent(O-1, TX-R-1001, 199.99, 2026-10-26T09:00:00Z)`; `OrderStateChangedIntegrationEvent(Version=3, Status=Refunded, PaidAt=2026-10-25T10:05:00Z, PaymentTransactionId=TX-1001, RefundedAt=2026-10-26T09:00:00Z, RefundTransactionId=TX-R-1001, IsUnderPaymentReview=false, …)` |

### P-14 (AC-15) Zwrot niedozwolony

| Given | When | Then |
|---|---|---|
| S-Awaiting | `RefundOrder(O-1, TX-R-1001, "Reklamacja", …)` | `422` `orders.refund-not-allowed`, wersja 1 |
| S-Cancelled | `RefundOrder(O-1, TX-R-1001, "Reklamacja", …)` | `422` `orders.refund-not-allowed`, wersja 2 |
| S-Refunded (TX-R-1001) | `RefundOrder(O-1, TX-R-1001, "Reklamacja", …)` (ponowienie) | `422` `orders.refund-not-allowed`, wersja 3 |

### P-15 (AC-16) Niepoprawny zwrot

| Przypadek | Wynik |
|---|---|
| `refundTransactionId: ""` | `400`, błąd dla `RefundTransactionId` |
| `refundTransactionId` 51 znaków | `400`, błąd dla `RefundTransactionId` |
| `reason: ""` | `400`, błąd dla `Reason` |
| `refundedAt` = wartość domyślna | `400`, błąd dla `RefundedAt` |

### P-16 (AC-21) Nieistniejące zamówienie

| | |
|---|---|
| **Given** | brak strumienia `0199a6f0-9999-7000-8000-000000000999` |
| **When** | `ConfirmPayment` / `CancelOrder` / `RefundOrder` dla tego id (poprawne body, właściwa rola) |
| **Then** | `404` `not-found`; brak zdarzeń integracyjnych |

### P-17 (AC-22) Role

| Operacja | `sales` | `gateway` | `accounting` |
|---|---|---|---|
| `POST /api/orders` | 201 | 403 | 403 |
| `POST /api/orders/O-1/payment-confirmation` | 403 | 204 | 403 |
| `POST /api/orders/O-1/cancellation` | 204 | 403 | 403 |
| `POST /api/orders/O-1/refund` | 204 | 403 | 403 |

(Status sukcesu przy poprawnych danych i właściwym stanie zamówienia.)

### P-18 (AC-23) Migracja

| | |
|---|---|
| **Given** | baza zbudowana wyłącznie przez Flyway z `db/flyway-modules.json` |
| **When** | test zgodności schematu (`AssertDatabaseMatchesConfigurationAsync` dla `IOrdersStore`) |
| **Then** | brak różnic; plik `V002__…sql` w `Shop.Orders.Migrations/Sql` bez `DROP`/`TRUNCATE` |

## 6. Model domeny

### Komendy

| Komenda | Pola | Kto wywołuje |
|---|---|---|
| `PlaceOrder` | `Customer` (`Name`, `Email`), `Lines` (`Sku`, `ProductName`, `Quantity`, `UnitPrice`) | `sales` przez `POST /api/orders` |
| `ConfirmPayment` | `OrderId`, `TransactionId`, `Amount`, `PaidAt` | `gateway` przez `POST /api/orders/{orderId}/payment-confirmation` |
| `CancelOrder` | `OrderId`, `Reason` | `sales` przez `POST /api/orders/{orderId}/cancellation` |
| `RefundOrder` | `OrderId`, `RefundTransactionId`, `Reason`, `RefundedAt` | `sales` przez `POST /api/orders/{orderId}/refund` |

Komendy nie mają pola `Version` (kontrakt go nie przewiduje) – `[WriteModel]` ładuje agregat bez oczekiwanej wersji,
więc nieistniejący strumień daje `RequiredDataMissingException` → 404 (pułapka „expected 1, but was 0” z D-012 nie dotyczy).
`OrderId` nowego zamówienia nadaje handler `PlaceOrder` (`Guid.CreateVersion7()`); handler zwraca `OrderPlacedResult`.

### Zdarzenia domenowe

| Zdarzenie | Pola | Agregat (strumień) |
|---|---|---|
| `OrderPlaced` | `OrderId`, `Customer` (`Name`, `Email`), `Lines` (`Sku`, `ProductName`, `Quantity`, `UnitPrice`), `TotalAmount`, `PlacedAt` | `Order` (start strumienia) |
| `PaymentConfirmed` | `TransactionId`, `Amount`, `PaidAt` | `Order` |
| `OrderCancelled` | `Reason`, `CancelledAt` | `Order` |
| `OrderRefunded` | `RefundTransactionId`, `Amount`, `Reason`, `RefundedAt` | `Order` |

`OrderPlaced` zawiera `OrderId`, bo `Order.Create(OrderPlaced)` musi ustawić `Id` agregatu.

### Reguły biznesowe

| Reguła | Kod błędu | HTTP |
|---|---|---|
| Kształt `PlaceOrder` (AC-3, AC-4) | `validation-failed` | 400 |
| Kształt `ConfirmPayment` (AC-10) | `validation-failed` | 400 |
| Kształt `CancelOrder` (AC-13) | `validation-failed` | 400 |
| Kształt `RefundOrder` (AC-16) | `validation-failed` | 400 |
| Płatność tylko dla zamówienia oczekującego na płatność | `orders.payment-not-expected` | 422 |
| Kwota płatności = `TotalAmount` | `orders.payment-amount-mismatch` | 422 |
| Ponowione potwierdzenie tej samej transakcji = brak zmian (sprawdzane **przed** dwiema powyższymi) | – | 204 |
| Anulowanie tylko przed płatnością | `orders.cancel-not-allowed` | 422 |
| Zwrot tylko opłaconego zamówienia | `orders.refund-not-allowed` | 422 |
| Brak zamówienia | `not-found` | 404 |

Kolejność sprawdzeń w `Order.ConfirmPayment`: (1) opłacone tym samym `TransactionId` → `null` (brak zmian);
(2) `EnsureAwaitingPayment`; (3) `EnsureAmountMatchesTotal`. Dzięki temu ponowienie z inną kwotą (P-8) jest sukcesem,
a płatność z inną kwotą dla anulowanego zamówienia zwraca `payment-not-expected`.

### Zmiany agregatów / usług domenowych

Nowy agregat `Order` (niemutowalny `sealed record`, styl decider – `.claude/rules/domain.md`):

* stan: `Id`, `Version`, `Customer`, `Lines`, `TotalAmount`, `Status`, `PlacedAt`, `Payment?`, `Refund?`;
  pól `IsUnderPaymentReview` / `PaymentReviewReason` **nie** dodajemy w tym slice'ie (etap 4 doda je razem z `PaymentReviewRequested`),
* `static Create(OrderPlaced)`, `Apply(PaymentConfirmed)`, `Apply(OrderCancelled)`, `Apply(OrderRefunded)` – publiczne,
* decyzje: `static Place(...)` → `OrderPlaced` (liczy `TotalAmount`), `ConfirmPayment(...)` → `PaymentConfirmed?`,
  `Cancel(reason, cancelledAt)` → `OrderCancelled`, `RefundInFull(refundTransactionId, reason, refundedAt)` → `OrderRefunded`
  (`Amount` = `Payment.Amount`),
* każda reguła w nazwanej metodzie `Ensure…` / `Is…` z komentarzem (sens biznesowy).

## 7. Zdarzenia integracyjne

| Zdarzenie | Nowe / zmienione | Pola | Producent | Konsumenci | Reakcja konsumenta |
|---|---|---|---|---|---|
| `OrderPaidIntegrationEvent` | nowe | `Guid OrderId`, `string TransactionId`, `decimal Amount`, `DateTimeOffset PaidAt` | Orders (`ConfirmPayment`) | brak w tym slice'ie; Settlements w etapie 4 | (etap 4) `RegisterExpectedPayment` |
| `OrderRefundedIntegrationEvent` | nowe | `Guid OrderId`, `string RefundTransactionId`, `decimal Amount`, `DateTimeOffset RefundedAt` | Orders (`RefundOrder`) | brak; Settlements w etapie 4 | (etap 4) `RegisterExpectedRefund` |
| `OrderStateChangedIntegrationEvent` | nowe | `Guid OrderId`, `long Version`, `string CustomerName`, `string CustomerEmail`, `decimal TotalAmount`, `OrderStatus Status`, `DateTimeOffset PlacedAt`, `DateTimeOffset? PaidAt`, `string? PaymentTransactionId`, `DateTimeOffset? RefundedAt`, `string? RefundTransactionId`, `bool IsUnderPaymentReview` | Orders (każda zmiana strumienia) | brak; DataHub w etapie 5 (`order_payments`) | (etap 5) nadpisanie kolumn Orders, gdy `Version` nowsza (D-006) |

* `OrderStatus` – publiczny enum w `Shop.Orders.Contracts` (`AwaitingPayment`, `Paid`, `Cancelled`, `Refunded`); test nazw
  `ContractNamingTests` pomija enumy.
* Pozycji zamówienia **nie** ma w `OrderStateChangedIntegrationEvent` – `order_payments` ich nie pokazuje (pola zgodne z `CLAUDE.md` modułu).
* Wszystkie zdarzenia budowane wyłącznie w `Application/IntegrationEvents/OrderIntegrationEvents.cs`; zdarzenie stanu
  jedną metodą ze stanu agregatu po zmianie (`order.Apply(event)`).
* Komentarz XML (po polsku) na każdym typie: znaczenie, moment publikacji, odbiorcy.
* Brak odbiorców jest oczekiwany: tracked session raportuje te wiadomości w `NoRoutes` (pułapka z D-012) – testy integracyjne
  sprawdzają publikację tam, a nie w `Sent`.

## 8. Kontrakt API

Bez zmian – operacje `PlaceOrder`, `ConfirmPayment`, `CancelOrder`, `RefundOrder` istnieją w `shop-api.yaml` i w wygenerowanym
`OrdersApiController`. Ograniczenia z kontraktu (`maxLength`, `minItems`/`maxItems`, `minimum`/`maximum`) są powtórzone
w walidatorach FluentValidation, bo wygenerowany kod ich nie wymusza w całości. Reguła „maks. 2 miejsca po przecinku”
dla `unitPrice` i `amount > 0` są tylko w walidatorach (kontrakt ich nie opisuje – patrz sekcja 13).

```yaml
# delta do shop-api.yaml – brak
```

| Operacja | Rola | Odpowiedzi |
|---|---|---|
| `PlaceOrder` | `sales` | 201 `OrderPlacedResponse`, 400, 401, 403 |
| `ConfirmPayment` | `gateway` | 204, 400, 401, 403, 404, 409, 422 |
| `CancelOrder` | `sales` | 204, 400, 401, 403, 404, 409, 422 |
| `RefundOrder` | `sales` | 204, 400, 401, 403, 404, 409, 422 |

`409` (`concurrency-conflict`) – dwie równoległe komendy na tym samym strumieniu; mapowanie istnieje w Host (spec 001), w tym slice'ie bez nowego AC.
`201` bez nagłówka `Location` – kontrakt nie ma operacji odczytu zamówienia w Orders.

## 9. Baza danych

| Schemat | Zmiana | Źródło migracji | Czy usuwa dane? |
|---|---|---|---|
| `orders` | tabele zdarzeń Martena (`mt_events`, `mt_streams`, sekwencje, funkcje pomocnicze), tabela snapshotu `Order` (`mt_doc_order`) i inne obiekty, które `db-patch` zwróci po rejestracji snapshotu – plik `V002__order_event_store.sql` | Marten `db-patch` (skill `db-migration`) | nie |
| `wolverine` | brak zmian (oczekiwane – do potwierdzenia `db-patch`) | – | nie |
| `settlements`, `datahub` | brak zmian | – | nie |

Rejestracja w `OrdersInfrastructureExtensions`: `options.Projections.Snapshot<Order>(SnapshotLifecycle.Inline)` (D-012).
Rollback (`*.drop.sql`) do `Shop.Orders.Migrations/Rollback/`.

## 10. DataHub

Nie dotyczy (etap 5). Zdarzenie `OrderStateChangedIntegrationEvent` ma pola potrzebne kolumnom Orders w `order_payments`
(`src/Modules/DataHub/CLAUDE.md`), ale DataHub go jeszcze nie konsumuje.

## 11. Plan testów

Projekt `tests/Shop.Orders.Tests` (nowy, w `Shop.slnx`): xUnit v3, Shouldly, `Microsoft.Extensions.TimeProvider.Testing` (`FakeTimeProvider` – nowy pakiet, sekcja 13).
Dane: `OrderMother` (`Awaiting()`, `Paid()`, `Cancelled()`, `Refunded()` – stany S-* z sekcji 5).

| AC | Poziom | Test (opis zachowania) |
|---|---|---|
| AC-1 | domena | `Place_WithValidLines_ReturnsOrderPlacedWithCustomerLinesAndPlacedAt`; `Create_FromOrderPlaced_SetsStatusAwaitingPayment` |
| AC-1 | aplikacja | `PlaceOrderHandler_StartsStreamWithOrderPlacedAndReturnsResult` (czas z `FakeTimeProvider`) |
| AC-1 | integracja | `PlaceOrder_AsSales_Returns201WithOrderIdAndTotal` (P-1, zapis w strumieniu) |
| AC-2 | domena | `Place_ComputesTotalAmountAsSumOfLines` (`[Theory]`: P-1 = 199.99, P-4 = 149.97) |
| AC-3 | aplikacja | `PlaceOrderValidator_RejectsInvalidLines` (`[Theory]` – przypadki P-2) |
| AC-3 | integracja | `PlaceOrder_WithoutLines_Returns400AndCreatesNoStream` |
| AC-4 | aplikacja | `PlaceOrderValidator_RejectsInvalidCustomer` (`[Theory]` – przypadki P-3) |
| AC-5 | aplikacja | `PlaceOrderValidator_AcceptsRepeatedSku` |
| AC-5 | domena | `Place_WithRepeatedSku_KeepsSeparateLines` (P-4) |
| AC-6 | domena | `ConfirmPayment_WhenAwaitingAndAmountMatches_ReturnsPaymentConfirmed`; `Apply_PaymentConfirmed_SetsStatusPaidAndPayment` |
| AC-6 | integracja | `ConfirmPayment_AsGateway_Returns204AndAppendsPaymentConfirmed` (P-5) |
| AC-7 | domena | `ConfirmPayment_WhenAmountDiffersFromTotal_ThrowsAmountMismatch` (P-6) |
| AC-7 | integracja | `ConfirmPayment_WithWrongAmount_Returns422WithCode` |
| AC-8 | domena | `ConfirmPayment_WhenNotAwaitingPayment_ThrowsPaymentNotExpected` (`[Theory]` – P-7) |
| AC-9 | domena | `ConfirmPayment_WhenAlreadyPaidWithSameTransaction_ReturnsNull` (`[Theory]` – P-8: Paid, Paid z inną kwotą, Refunded) |
| AC-9 | aplikacja | `ConfirmPaymentHandler_WhenRetriedCallback_ReturnsNoEventsAndNoMessages` |
| AC-9 | integracja | `ConfirmPayment_RetriedCallback_Returns204AndPublishesNothing` (wersja strumienia bez zmian) |
| AC-10 | aplikacja | `ConfirmPaymentValidator_RejectsInvalidRequest` (`[Theory]` – P-9) |
| AC-11 | domena | `Cancel_WhenAwaitingPayment_ReturnsOrderCancelled`; `Apply_OrderCancelled_SetsStatusCancelled` |
| AC-11 | integracja | `CancelOrder_AsSales_Returns204AndAppendsOrderCancelled` (P-10) |
| AC-12 | domena | `Cancel_WhenNotAwaitingPayment_ThrowsCancelNotAllowed` (`[Theory]` – P-11) |
| AC-13 | aplikacja | `CancelOrderValidator_RejectsInvalidReason` (P-12) |
| AC-14 | domena | `RefundInFull_WhenPaid_ReturnsOrderRefundedWithPaymentAmount`; `Apply_OrderRefunded_SetsStatusRefundedAndRefund` |
| AC-14 | integracja | `RefundOrder_AsSales_Returns204AndAppendsOrderRefunded` (P-13) |
| AC-15 | domena | `RefundInFull_WhenNotPaid_ThrowsRefundNotAllowed` (`[Theory]` – P-14) |
| AC-15 | integracja | `RefundOrder_WhenAwaitingPayment_Returns422WithCode` |
| AC-16 | aplikacja | `RefundOrderValidator_RejectsInvalidRequest` (`[Theory]` – P-15) |
| AC-17 | aplikacja | `OrderIntegrationEvents_StateChanged_MapsStateAfterChange` (`[Theory]`: S-Awaiting v1, S-Paid v2, S-Cancelled v2, S-Refunded v3); w każdym teście handlera – `StateChanged` z `Version` = wersja przed + 1 |
| AC-17 | integracja | `OrderLifecycle_PublishesStateChangedWithStreamVersionAfterEachChange` (P-1 → P-5 → P-13: wersje 1, 2, 3 w `NoRoutes`) |
| AC-18 | aplikacja | `ConfirmPaymentHandler_PublishesOrderPaidAndStateChanged` |
| AC-18 | integracja | `ConfirmPayment_PublishesOrderPaidIntegrationEvent` (tracked session) |
| AC-19 | aplikacja | `RefundOrderHandler_PublishesOrderRefundedAndStateChanged` |
| AC-19 | integracja | `RefundOrder_PublishesOrderRefundedIntegrationEvent` |
| AC-20 | integracja | `RejectedCommand_PublishesNoIntegrationEvents` (`[Theory]`: 400 z P-2, 422 z P-6, 404 z P-16) |
| AC-21 | integracja | `CommandOnMissingOrder_Returns404` (`[Theory]` – trzy operacje, P-16) |
| AC-22 | integracja | `OrdersEndpoint_WithoutRequiredRole_Returns403` (`[Theory]` – P-17); `ApiControllersTests` (polityki) bez zmian |
| AC-23 | integracja | istniejący `SchemaConsistencyTests` (zielony po migracji); istniejący `MigrationFilesTests` (numeracja `V002`) |
| AC-23 | architektura | przegląd SQL migracji pod kątem `DROP`/`TRUNCATE` (raport w sekcji 14) |

**Zmiany istniejących testów etapu 2** (konsekwencja zastąpienia zaślepek – nie są nowymi AC):
* `ApiControllersTests`: usunąć `OrdersApiController → "Etap 3"` z `StageByContract` (test zaślepek dotyczy już tylko Settlements i DataHub);
  test pokrycia operacji polityką zostaje.
* `AuthorizationTests`: `POST /api/orders` z rolą `sales` oczekuje `201` zamiast `500`; test `ControllerStub_ReturnsProblemJsonWithStageMessage`
  przechodzi na zaślepkę innego modułu (np. `GET /api/datahub/order-payments`, „Etap 5”).

## 12. GUI

Nie dotyczy (etap 6).

## 13. Ryzyka i pytania otwarte

**Odpowiedzi programisty (28.10.2026):**
- [x] **Q-1** Numer specyfikacji: `002` dla Orders. Odwołania w `010` („002 (Settlements), 003 (DataHub)”) przesuną się na 003 i 004 –
  do poprawienia przy specyfikacji Settlements (etap 4); `010` nie jest zmieniana w tym slice'ie.
- [x] **Q-2** Pakiet `Microsoft.Extensions.TimeProvider.Testing` – **zgoda** na dodanie do `Directory.Packages.props` i `tests/Shop.Orders.Tests`
  (wersja z linii `Microsoft.Extensions.*` 10.x używanej w repo – do potwierdzenia przy implementacji, wpis w `docs/decisions.md`).
  `NSubstitute` nie jest dodawany – handlery są statyczne, testy bez mocków.
- [x] **Q-3** `unitPrice` z więcej niż 2 miejscami po przecinku → `400`; powtórzony SKU dozwolony (AC-3, AC-5).
- [x] **Q-4** Ponowny callback z tym samym `TransactionId`, ale inną kwotą → `204` bez zmian (AC-9); ponowny `RefundOrder`
  z tym samym `RefundTransactionId` → `422` `orders.refund-not-allowed` (AC-15).

**Założenia przyjęte (decyzja Claude – do potwierdzenia przy akceptacji):**
- `amount > 0` w `ConfirmPayment` jest regułą walidatora (400), choć kontrakt nie ma `minimum` dla `amount`.
- Brak `paidAt` / `refundedAt` = wartość domyślna `DateTimeOffset` po deserializacji – walidator odrzuca ją jako „wymagane”.
- `paidAt` i `refundedAt` przyjmujemy z żądania bez porównywania z `PlacedAt` ani z bieżącym czasem (czas bramki jest źródłem prawdy).
- `CancelledAt` i `PlacedAt` pochodzą z `TimeProvider` (kontrakt nie przekazuje czasu). Host rejestruje `TimeProvider.System`
  w DI (dziś nie ma jawnej rejestracji) – rejestracja w Infrastructure modułu albo w Host, bez logiki.
- Agregat nie dostaje jeszcze `IsUnderPaymentReview` – zdarzenie stanu ustawia `false` stałą w mapowaniu (etap 4 podmieni na stan agregatu).
- `OrderStateChangedIntegrationEvent` bez pozycji zamówienia i bez `CancelledAt` (pola zgodnie z `CLAUDE.md` modułu i `order_payments`).

**Ryzyka:**
- [ ] Zwrócenie wyniku (`OrderPlacedResult`) razem z `MartenOps.StartStream` i `OutgoingMessages` z jednego handlera Wolverine 6 –
  kolejność elementów krotki i „pierwszy element = odpowiedź dla `InvokeAsync<T>`” do sprawdzenia w dokumentacji wersji 6.56.0.
  Jeśli nie zadziała – alternatywa z `IDocumentSession` (D-012, „handler wielostrumieniowy”), zapisana w sekcji 14.
- [ ] Mapowanie `OrderStatus` Domain → Contracts: dwa enumy o tej samej nazwie w różnych namespace'ach – mapowanie jawne (`switch`), nie rzutowanie.
- [ ] Równoległe ponowienia callbacku DemoPay mogą dać `409 concurrency-conflict` zamiast `204` (oba żądania widzą `AwaitingPayment`).
  Bramka ponawia, więc kolejne ponowienie dostanie `204` (AC-9). Akceptowalne w POC – bez AC.
- [ ] `db-patch` może dodać do schematu `orders` obiekty spoza listy z sekcji 9 (np. `mt_doc_deadletterevent`, tabele progresji) – to normalne,
  byle bez `DROP`.

## 14. Notatki z implementacji

<!-- Wypełnia Claude w trakcie /implement-slice: odstępstwa od specyfikacji, decyzje techniczne, rzeczy do zrobienia później. -->
