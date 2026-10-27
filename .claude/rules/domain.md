---
paths:
  - "src/Modules/**/*.Domain/**"
---

# Warstwa Domain

Domain to serce modułu: agregaty, zdarzenia domenowe, reguły i czyste usługi domenowe (np. uzgodnienie raportu).

## Zależności

* Tylko BCL i `Shop.SharedKernel`. **Żadnych typów** z Marten, Wolverine, EF Core, ASP.NET, FluentValidation.
* Wyjątek techniczny (decyzja D-009): projekt może mieć referencję potrzebną do uruchomienia source generatora
  Martena 9 dla agregatów. Kod Domain nadal nie używa typów Martena – pilnują tego testy architektury.

## Agregat (styl „decider”)

* Stan agregatu to **niemutowalny `sealed record`**. Zmiana stanu wyłącznie przez publiczne metody
  `Apply(<Zdarzenie>)` zwracające nową instancję oraz `static Create(<ZdarzeniePoczątkowe>)`.
* Marten 9: metody `Apply`/`Create` i konstruktory wołane przez Marten **muszą być publiczne** – prywatne są
  po cichu pomijane.
* Agregat ma właściwość `Id` (Guid) i `Version` (long), ustawiane przez Marten.
* Decyzje to metody, które sprawdzają reguły i **zwracają zdarzenie(a)**, nie zmieniają stanu:

```csharp
public sealed record Order(...)
{
    public PaymentConfirmed? ConfirmPayment(string transactionId, decimal amount, DateTimeOffset paidAt)
    {
        // Bramki ponawiają callbacki – ponowne potwierdzenie tej samej transakcji nie jest błędem.
        if (IsPaidWith(transactionId))
            return null;

        EnsureAwaitingPayment();
        EnsureAmountMatchesTotal(amount);

        return new PaymentConfirmed(transactionId, amount, paidAt);
    }

    public Order Apply(PaymentConfirmed confirmed) =>
        this with { Status = OrderStatus.Paid, Payment = new Payment(confirmed.TransactionId, confirmed.Amount, confirmed.PaidAt) };
}
```

* Marten 9 domyślnie ponownie używa instancji agregatu (`UseIdentityMapForAggregates = true`) – dlatego handler
  **nigdy** nie mutuje agregatu; każda zmiana idzie przez zdarzenie.

## Reguły biznesowe

* Naruszenie reguły → `BusinessRuleViolationException(code, message)` z `Shop.SharedKernel`.
  Kod: `<moduł>.<reguła-kebab-case>`, np. `orders.refund-blocked-by-payment-review`. Komunikat po polsku.
* Każda reguła ma własną, nazwaną metodę `Ensure…` lub predykat `Is…`/`Can…` z komentarzem wyjaśniającym sens.
* Walidacja kształtu danych wejściowych (puste pola, format e-mail) to nie domena – to walidator w Application.

## Zdarzenia domenowe

* `sealed record`, czas przeszły, tylko dane potrzebne do odtworzenia stanu i zrozumienia faktu.
* Nie zawierają identyfikatora strumienia, jeśli nie jest potrzebny w treści (Marten zna strumień).
* Zdarzeń domenowych **nie** zmieniamy wstecznie – zmiana kształtu = nowe zdarzenie lub upcasting (z decyzją w docs).

## Usługi domenowe

* Czyste funkcje statyczne lub `sealed` klasy bez zależności, np. `SettlementReconciliation.Reconcile(...)`.
* Wynik jako jawny typ (`SettlementReconciliationResult`) z listą wyników per element – bez wyjątków dla
  „spodziewanych” niezgodności.

## Testy

Każda reguła i każde zdarzenie mają test jednostkowy bez mocków – patrz `.claude/rules/testing.md`.
