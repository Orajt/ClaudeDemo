---
name: spec-slice
description: Tworzy szkic specyfikacji slice'a w docs/specs na podstawie opisu funkcjonalności, z pytaniami o niejasności. Nie pisze kodu.
argument-hint: "<opis funkcjonalności>"
disable-model-invocation: true
---

# Szkic specyfikacji slice'a

Funkcjonalność do opisania: **$ARGUMENTS**

Twoje zadanie: przygotować specyfikację, którą człowiek zaakceptuje, a potem `/implement-slice` zrealizuje bez zgadywania.
**Nie piszesz ani nie zmieniasz kodu.** Jedyny plik, który tworzysz, to specyfikacja.

## Kroki

1. **Zbierz kontekst (tylko odczyt).**
   - `docs/specs/_TEMPLATE.md` – szablon, którego trzymasz się dokładnie.
   - `docs/architecture.md` i `CLAUDE.md` modułów, których dotyczy zmiana.
   - Istniejące specyfikacje w `docs/specs/` – numeracja i podobne slice'y.
   - Kod, który zmiana dotknie: agregaty, zdarzenia, handlery, widoki DataHub, `api/openapi/shop-api.yaml`.
     Opieraj specyfikację na tym, co naprawdę jest w kodzie (nazwy zdarzeń, pól, statusów).

2. **Zidentyfikuj luki.** Wypisz dla siebie: reguły biznesowe, przypadki brzegowe, wpływ na inne moduły i DataHub,
   zmiany API, zmiany bazy, uprawnienia.

3. **Zapytaj o niejasności – zanim napiszesz.** Maksymalnie 5 pytań naraz, każde z proponowaną odpowiedzią domyślną
   i krótkim uzasadnieniem. Pytaj tylko o rzeczy, których nie da się ustalić z kodu i dokumentacji.
   Jeśli programista odpowie „decyduj sam” – przyjmij odpowiedź domyślną i zapisz ją w sekcji 13 jako założenie.

4. **Napisz specyfikację** do `docs/specs/<NNN>-<slug>.md` (NNN = kolejny wolny numer, slug po polsku bez znaków
   diakrytycznych). Status: `Szkic`.
   - Kryteria akceptacji: numerowane `AC-n`, jedno zachowanie na kryterium, sprawdzalne.
   - Przykłady: konkretne kwoty, identyfikatory (`TX-1001`), daty; każdy przykład wskazuje swoje AC.
     Przykłady muszą być spójne arytmetycznie – policz je.
   - Kody błędów reguł: `<moduł>.<reguła-kebab-case>`.
   - Zdarzenia integracyjne: pamiętaj o `…StateChangedIntegrationEvent` przy każdej zmianie strumienia,
     jeśli DataHub pokazuje dane tego agregatu (decyzja D-006).
   - Kontrakt API: delta YAML zgodna ze stylem istniejącego `shop-api.yaml` (nazwy operacji, ProblemDetails, role).
   - Baza: dla każdej zmiany wskaż źródło migracji i czy usuwa dane.
   - Plan testów: każde AC ma co najmniej jeden test na właściwym poziomie.

5. **Sprawdź specyfikację** zanim oddasz:
   - każde AC ma przykład i test w planie,
   - nie ma sprzeczności z `docs/architecture.md` i `docs/decisions.md` (jeśli są celowe – pytanie w sekcji 13),
   - słownictwo zgodne z `CLAUDE.md` modułów.

6. **Podsumuj** w rozmowie: ścieżka pliku, lista AC w jednym zdaniu każde, otwarte pytania, zmiany API i bazy.
   Poproś o akceptację. Status zmienia na `Zaakceptowana` **człowiek** (albo Ty – na jego wyraźne polecenie).
