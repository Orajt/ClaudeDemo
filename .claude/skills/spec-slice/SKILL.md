---
name: spec-slice
description: Tworzy szkic specyfikacji slice'a w docs/specs na podstawie opisu funkcjonalności, z pytaniami o niejasności i rekomendacją modelu oraz effortu do implementacji (docs/model-policy.md). Nie pisze kodu.
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

6. **Zaproponuj model i effort dla `/implement-slice`** według `docs/model-policy.md`. Przeczytaj plik przy każdym
   uruchomieniu – nie polegaj na pamięci, polityka się zmienia.
   - Oceń slice po tym, co wynika ze specyfikacji: liczba modułów, nowe zdarzenia integracyjne, zmiany kontraktu API,
     migracje, nowe reguły domenowe vs. zmiana mechaniczna (np. nowa kolumna w widoku, zmiana nazwy).
   - Dobierz model z polityki: domyślny model sesji; mocniejszy tylko w przypadkach, które polityka wymienia
     (np. zmiana między modułami); słabszy tylko dla zmian mechanicznych. Najmocniejszego modelu nie proponujesz –
     polityka wymaga eskalacji i decyzji człowieka; możesz jedynie zasygnalizować, że eskalacja może być potrzebna.
   - Effort: domyślny dla modelu; niższy tylko dla zmian mechanicznych. Nie podawaj poziomów, których polityka
     nie potwierdza.
   - Uzasadnij jednym–dwoma zdaniami z odwołaniem do konkretnej zasady polityki.
   - Jeśli polityka ma niewypełnione pozycje (`UZUPEŁNIĆ`, „do potwierdzenia”) dotyczące proponowanego modelu
     lub effortu – zaznacz, że rekomendacja jest warunkowa i wymaga potwierdzenia dostępności.
   - Rekomendacja trafia tylko do podsumowania w rozmowie, nie do specyfikacji.

7. **Podsumuj** w rozmowie: ścieżka pliku, lista AC w jednym zdaniu każde, otwarte pytania, zmiany API i bazy
   oraz rekomendacja z kroku 6 w formacie:

   ```
   Rekomendacja dla /implement-slice: model `<alias>`, effort `<poziom>`
   Uzasadnienie: <1–2 zdania, zasada z docs/model-policy.md>
   ```

   Poproś o akceptację. Status zmienia na `Zaakceptowana` **człowiek** (albo Ty – na jego wyraźne polecenie).
