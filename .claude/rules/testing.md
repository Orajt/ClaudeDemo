---
paths:
  - "tests/**"
---

# Testy

Stos: xUnit v3, Shouldly, NSubstitute, Alba, Testcontainers (PostgreSQL), Wolverine tracked sessions.

## Piramida i miejsce testów

| Co testujesz | Projekt | Jak |
|---|---|---|
| Reguły agregatu, usługi domenowe | `tests/Shop.<Moduł>.Tests/Domain` | czysto, bez mocków: dane zdarzenia → decyzja → oczekiwane zdarzenia/wyjątek |
| Handlery (mapowanie, orkiestracja) | `tests/Shop.<Moduł>.Tests/Application` | wywołanie statycznej metody `Handle` z agregatem i komendą; NSubstitute tylko dla interfejsów spoza Domain |
| Walidatory | `tests/Shop.<Moduł>.Tests/Application` | `TestValidate` z FluentValidation |
| Funkcje DataHub (resolver statusu, mapowanie) | `tests/Shop.DataHub.Tests` | czyste testy tabelaryczne |
| Przepływ end-to-end slice'a, projekcje DataHub, migracje | `tests/Shop.IntegrationTests` | Alba + prawdziwy Postgres z Flyway + tracked session |
| Zależności między warstwami/modułami, nazwy migracji | `tests/Shop.ArchitectureTests` | ArchUnitNET + testy plików |

## Konwencje

* Nazwa testu opisuje zachowanie po angielsku: `ConfirmPayment_WhenAmountDiffersFromTotal_ThrowsAmountMismatch`.
* Struktura Given / When / Then z pustymi liniami między sekcjami. Komentarze `// Given` itd. tylko w dłuższych testach.
* Jedno zachowanie na test. Przypadki tabelaryczne: `[Theory]` + `[InlineData]`/`TheoryData`.
* Asercje: Shouldly (`ShouldBe`, `ShouldBeOfType`, `Should.Throw<…>()`), dla wyjątku reguły sprawdź `Code`.
* Dane testowe przez małe metody fabrykujące (`OrderMother.Paid()`), nie przez kopiowanie konstrukcji.
* Każdy test ma odniesienie do kryterium akceptacji: `[Trait("AC", "AC-3")]` – na tej podstawie raportujesz
  mapowanie AC → test.
* Czas: `FakeTimeProvider`. Losowość: brak – identyfikatory jawne w teście.

## Testy integracyjne

* Jeden kontener Postgres na przebieg (fixture zestawu). Schemat budowany **tylko przez Flyway** z
  `db/flyway-modules.json` – te same pliki co produkcja.
* Test zgodności schematu: po Flyway konfiguracja Martena/Wolverine nie ma różnic z bazą, model EF nie ma
  oczekujących zmian. Ten test łapie zapomnianą migrację.
* Akcja przez HTTP (Alba `Scenario`) albo komendę, opakowana w tracked session Wolverine, aby poczekać na
  **wszystkie** kaskadowe wiadomości (także między modułami) przed asercją.
* Uwierzytelnianie: stub Alba z rolami – bez Keycloaka.
* Izolacja: każdy test używa nowych identyfikatorów; nie zakładaj pustej bazy.

## Zakazy

* `[Fact(Skip = …)]`, `Thread.Sleep`/`Task.Delay` jako synchronizacja, asercje na logach, testy zależne od kolejności.
* Mockowanie Martena, EF Core lub Wolverine – zamiast tego test integracyjny.
