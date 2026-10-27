---
name: implement-slice
description: Implementuje zaakceptowaną specyfikację slice'a z docs/specs – plan, testy, kod warstwa po warstwie, migracje, weryfikacja i raport AC → testy.
argument-hint: "<ścieżka do specyfikacji>"
disable-model-invocation: true
---

# Implementacja slice'a według specyfikacji

Specyfikacja: **$ARGUMENTS**

## 0. Warunki wstępne – sprawdź, zanim cokolwiek zmienisz

- Specyfikacja ma status `Zaakceptowana`. Jeśli nie – zatrzymaj się i poproś o akceptację.
- Sekcja 13 nie ma otwartych pytań blokujących. Jeśli ma – zadaj je.
- Jeśli specyfikacja zmienia API: wygenerowany kontrakt zawiera nowe operacje (zbuduj projekt `*.Api`).
  Jeśli nie zawiera – zatrzymaj się i zaproponuj `/api-contract`.
- `dotnet build Shop.slnx` przechodzi przed zmianami. Jeśli nie – zgłoś to, nie naprawiaj „przy okazji”.

## 1. Plan – i czekaj na akceptację

Przeczytaj specyfikację, `CLAUDE.md` dotkniętych modułów i istniejący kod podobnych slice'ów. Przedstaw plan:

- lista plików do utworzenia/zmiany pogrupowana warstwami (Contracts, Domain, Application, Infrastructure,
  Migrations, Api, DataHub, testy),
- mapowanie AC → testy (nazwy testów),
- migracje: które moduły, jakie zmiany,
- ryzyka i miejsca, gdzie specyfikacja jest niejednoznaczna.

**Zatrzymaj się i poczekaj na akceptację planu**, chyba że programista z góry napisał „bez akceptacji planu”.
Zmień status specyfikacji na `W realizacji`.

## 2. Implementacja – w tej kolejności

Po każdym kroku: `dotnet build` dotkniętych projektów. Przed przejściem dalej build musi być zielony.

1. **Contracts** – nowe/zmienione zdarzenia integracyjne z komentarzem XML.
2. **Domain – najpierw testy.** Zamień przykłady ze specyfikacji na testy jednostkowe (z `[Trait("AC", "AC-n")]`),
   uruchom – mają być czerwone. Potem zaimplementuj zdarzenia, reguły, `Apply`, usługi domenowe. Testy zielone.
3. **Application** – komendy, walidatory, handlery, mapowanie na zdarzenia integracyjne, reakcje na zdarzenia
   z innych modułów. Testy handlerów i walidatorów.
4. **Infrastructure** – tylko rejestracja (nowe typy zdarzeń/snapshoty w store, polityki).
5. **Api** – nadpisanie wygenerowanych metod (zastąp zaślepki `NotImplementedException`), mapowanie request → komenda.
6. **Migracje** – skill `db-migration` dla każdego dotkniętego schematu.
7. **DataHub** – skill `datahub-view`, jeśli specyfikacja zmienia widoki.
8. **Testy integracyjne** – po jednym na każde AC wymagające przepływu przez HTTP/kolejki/bazę.
9. **Dokumentacja** – `CLAUDE.md` modułu (słownik, zdarzenia, reguły), `docs/decisions.md` jeśli podjąłeś decyzję.

Zasady w trakcie:

- Trzymaj się specyfikacji. Brak informacji → zapytaj. Świadome odstępstwo → zapisz w sekcji 14 specyfikacji.
- Reguły warstw w `.claude/rules/` są obowiązujące – przeczytaj je, jeśli nie załadowały się same.
- Nie dotykaj kodu niezwiązanego ze slice'em. Zauważony problem → lista „do zrobienia później” w raporcie.

## 3. Weryfikacja

Uruchom i doprowadź do zieleni:

```bash
dotnet build Shop.slnx
dotnet test Shop.slnx
```

Jeśli test pada – napraw przyczynę w kodzie, nie w teście (chyba że test był błędny – wtedy wyjaśnij dlaczego).
Nie oznaczaj testów jako pominięte.

## 4. Raport końcowy

- Status specyfikacji → `Zaimplementowana`, sekcja 14 uzupełniona.
- W rozmowie:
  - tabela **AC → testy** (nazwa testu, poziom, wynik),
  - lista zmienionych plików pogrupowana warstwami,
  - migracje (pliki, czy zawierają `DROP`),
  - odstępstwa od specyfikacji i decyzje,
  - propozycja komunikatu commita (Conventional Commits, po angielsku).
- Zasugeruj `/review-slice $ARGUMENTS`.
- Nie commituj i nie pushuj bez prośby.
