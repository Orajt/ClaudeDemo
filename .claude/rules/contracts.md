---
paths:
  - "src/Modules/**/*.Contracts/**"
---

# Warstwa Contracts (zdarzenia integracyjne)

`*.Contracts` to publiczne API modułu dla innych modułów. Zmiana tu wpływa na odbiorców – traktuj jak kontrakt.

* Tylko `sealed record` zdarzeń integracyjnych i typy wartości, których używają (enumy). Zero logiki, zero zależności
  poza BCL.
* Nazwa: `<Fakt>IntegrationEvent`, namespace `Shop.<Moduł>.Contracts`.
* Każde zdarzenie ma komentarz XML (po polsku): co oznacza, kiedy jest publikowane, kto je konsumuje.
* Zdarzenia zmiany stanu mają `Guid <Agregat>Id` i `long Version`. Zdarzenia dziennikowe mają unikalny identyfikator
  faktu (np. `DiscrepancyId`).
* Zmiany kompatybilne: dodanie pola opcjonalnego (nullable) na końcu. Zmiana niekompatybilna (usunięcie, zmiana typu,
  zmiana nazwy) = nowe zdarzenie lub decyzja w `docs/decisions.md`. Wolverine identyfikuje typ wiadomości po pełnej
  nazwie – **nie przenoś typów między namespace'ami**.
* Kwoty `decimal`, czasy `DateTimeOffset`, daty rozliczeń `DateOnly`.
