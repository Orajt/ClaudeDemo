---
paths:
  - "src/Modules/**/*.Api/**"
  - "src/Host/Shop.Host/**"
---

# Warstwa Api i Host

## Kontroler modułu

* Dziedziczy po **wygenerowanym** abstrakcyjnym kontrolerze z `Shop.Api.Contracts.Generated`
  (np. `OrdersApiController`) i nadpisuje jego metody. Trasy, nazwy operacji i modele pochodzą z kontraktu.
* Metoda kontrolera ma 3 kroki i nic więcej:
  1. zmapuj request → komendę/zapytanie (metoda mapująca w `Mapping/`),
  2. `await bus.InvokeAsync<TWynik>(komenda, cancellationToken)`,
  3. zmapuj wynik → odpowiedź (`Created`, `NoContent`, `Ok`).
* Bez `try/catch` – wyjątki mapuje globalnie Host na ProblemDetails.
* Bez dostępu do bazy, bez `IDocumentSession`, bez `DbContext`, bez logiki warunkowej związanej z biznesem.
* Autoryzacja: atrybut `[Authorize(Policy = …)]` na nadpisanej metodzie; nazwy polityk ze stałych w Host.

```csharp
public sealed class OrdersController(IMessageBus bus) : OrdersApiController
{
    [Authorize(Policy = AuthorizationPolicies.Gateway)]
    public override async Task<IActionResult> ConfirmPayment(Guid orderId, ConfirmPaymentRequest confirmPaymentRequest)
    {
        var command = confirmPaymentRequest.ToCommand(orderId);
        await bus.InvokeAsync(command, HttpContext.RequestAborted);
        return NoContent();
    }
}
```

Sygnatury metod wynikają z generatora (openapi-generator `aspnetcore` nie dodaje `CancellationToken` – używaj
`HttpContext.RequestAborted`). Wygenerowane metody mają już `[Authorize]`; Ty dodajesz politykę z rolą.

## Zmiana kontraktu

* Nie edytujesz `src/Api/Shop.Api.Contracts.Generated/**`. Jeśli brakuje operacji lub pola – zatrzymaj się
  i zaproponuj zmianę `api/openapi/shop-api.yaml` (skill `/api-contract`). Po regeneracji nowe metody abstrakcyjne
  pojawią się jako błędy kompilacji – to oczekiwane.

## Host (`Shop.Host`)

* Tylko kompozycja: ServiceDefaults, uwierzytelnianie (Keycloak JWT), polityki autoryzacji, kontrolery,
  ProblemDetails, bazowa konfiguracja Wolverine, rejestracja modułów (`builder.AddOrdersModule()` itd.),
  `return await app.RunJasperFxCommands(args);` (komendy `db-*`).
* Kod handlerów Wolverine jest generowany w czasie działania (`WolverineFx.RuntimeCompilation`,
  `TypeLoadMode.Dynamic`, decyzja D-013). Nie uruchamiasz `codegen write`, nie commitujesz `Internal/Generated`
  i nie pytasz ponownie o tryb Static.
* Pakiety zatwierdzone w D-014 możesz używać bez pytania; nowy pakiet lub zmiana wersji – zgodnie z zasadą 9 `CLAUDE.md`.
* Mapowanie wyjątków → ProblemDetails w jednym miejscu (`ExceptionToProblemDetailsMapper`):

| Wyjątek | Status | `code` |
|---|---|---|
| `FluentValidation.ValidationException` | 400 | `validation-failed` + `errors` |
| `NotFoundException`, `RequiredDataMissingException` (brak agregatu dla `[WriteModel]`) | 404 | `<moduł>.not-found` / `not-found` |
| konflikt współbieżności Martena, `DuplicateException` | 409 | `concurrency-conflict` / kod z wyjątku |
| `BusinessRuleViolationException` | 422 | kod z wyjątku |
