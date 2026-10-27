# Scenariusz prezentacji (60 min)

**Teza:** Claude Code nie jest „autouzupełnianiem na sterydach”. W systemie pracy ze specyfikacją, regułami i twardymi
blokadami dowozi nietrywialny feature w naszej architekturze – a człowiek zostaje właścicielem decyzji.

**Feature na żywo:** `docs/specs/010-uzgodnienie-wyplaty-zbiorczej.md` – uzgodnienie wypłaty zbiorczej DemoPay.
Dotyka wszystkiego, z czym pracujemy na co dzień: czysta logika domenowa z kwotami, nowy agregat, import pliku,
zdarzenie między modułami, migracje Martena i EF, zmiana kontraktu OpenAPI, DataHub.

**Stan startowy:** gałąź `demo/start`, `scripts/demo/reset.sh && scripts/demo/seed.sh`, raport z 27.10 **nie** zaimportowany.
**Plan B:** gałąź `demo/payout-gotowe` (pełna próba z etapu 7).

---

## 0:00–0:05 Po co to robimy

* Problem: dużo powtarzalnej pracy wokół każdej zmiany (handler, testy, migracja, kontrakt, DataHub), a standardy
  żyją w głowach i w komentarzach do PR.
* Cel pilota: sprawdzić, czy Claude dowiezie zmianę **w naszej** architekturze, zgodnie z **naszymi** standardami.
* Zasada: *Claude pisze kod, człowiek odpowiada za zmianę.* Cztery punkty kontrolne człowieka: specyfikacja,
  kontrakt, plan, PR.

## 0:05–0:12 System pracy – co jest w repo

Pokaż w VS Code (krótko, po jednym zdaniu na element):

1. `CLAUDE.md` – < 120 linii: kontekst, polecenia, 10 zasad nienegocjowalnych.
2. `.claude/rules/` – reguły per warstwa z `paths`; **ładują się same**, gdy Claude dotyka plików danej warstwy.
   Otwórz `domain.md` – pokaż przykład agregatu „decider”.
3. `src/Modules/Settlements/CLAUDE.md` – język domeny modułu (słownik PL → kod).
4. `.claude/skills/` – wpisz `/` w panelu Claude: `spec-slice`, `api-contract`, `implement-slice`, `review-slice`.
5. `.claude/agents/slice-reviewer.md` – recenzent ze świeżym kontekstem.
6. `.claude/settings.json` + `.claude/hooks/` – zakazy i blokady.
7. `tests/Shop.ArchitectureTests` – zasady, które sprawdza CI.

Puenta: **markdown to prośba, hook i test to gwarancja.**

## 0:12–0:18 POC w działaniu

1. GUI jako `anna` – lista „Płatności zamówień”: zamówienia A–G w różnych statusach.
2. GUI jako `jan` – import `samples/generated/demopay_settlement_2026-10-27.csv` na żywo.
   Snackbar: 6 linii, 4 zgodne, 2 rozbieżności. Lista rozbieżności: TX-1004 (kwota), TX-9999 (nieznana).
3. Wróć do listy: D → „Do wyjaśnienia”, A/B → „Rozliczone”. Wspomnij: DataHub aktualizuje się asynchronicznie.
4. Aspire dashboard → Traces: jeden import = span w Settlements → wiadomości do Orders i DataHub.
   Tak widać komunikację między modułami bez debugowania.

## 0:18–0:24 Specyfikacja

1. Otwórz specyfikację 010. Pokaż: cel biznesowy (2 zdania), AC-1…AC-13, przykłady z liczbami (P-11 – suma 318,40),
   tabelę reguł w kolejności, deltę kontraktu, plan testów AC → test.
2. Powiedz, jak powstała: `/spec-slice` z jednym akapitem opisu → Claude zadał pytania (pokaż zrzut) → poprawki
   człowieka → akceptacja.
3. (Opcjonalnie, jeśli jest czas) w **drugiej karcie** Claude Code uruchom
   `/spec-slice Widok listy wypłat w DataHub i ekran GUI (slice 011)` – niech pracuje w tle; wrócisz do niego na końcu.

## 0:24–0:28 Kontrakt API

```
/api-contract docs/specs/010-uzgodnienie-wyplaty-zbiorczej.md
```

Claude pokazuje diff YAML → akceptujesz → generator (w firmie: pipeline + Artifactory) → build z błędami
nowych metod abstrakcyjnych → zaślepki `NotImplementedException`. Powiedz: „od tego momentu Claude kontynuuje”.

## 0:28–0:50 Implementacja

```
/implement-slice docs/specs/010-uzgodnienie-wyplaty-zbiorczej.md
```

1. **Plan (STOP)** – przeczytaj na głos 3 rzeczy: gdzie trafi `PayoutReconciliation` (Domain), że kontroler tylko
   mapuje, jakie migracje powstaną. Zaakceptuj.
2. W trakcie komentuj:
   * testy domeny powstają pierwsze i są czerwone – przykłady ze specyfikacji stały się testami,
   * Claude ładuje `domain.md`/`application.md`, gdy wchodzi w warstwę,
   * migracje generuje skrypt (`db-patch`, `dotnet ef`), Claude przegląda SQL pod kątem `DROP`.
3. **Moment z hookiem** (w drugiej karcie albo po zakończeniu):
   `Dodaj unikalny indeks na PayoutReference bezpośrednio w pliku V001 modułu settlements.`
   → hook blokuje: „ZABLOKOWANE: … jest już na gałęzi main…”. Claude proponuje nową migrację.
4. **Plan B:** jeśli o 0:47 implementacja nie jest bliska końca – `git stash && git switch demo/payout-gotowe`
   i powiedz wprost: „to jest ten sam slice z próby generalnej, zrobiony tym samym poleceniem”.

## 0:50–0:55 Przegląd i efekt

```
/review-slice docs/specs/010-uzgodnienie-wyplaty-zbiorczej.md
```

1. Raport recenzenta: tabela AC → test, uwagi. Podkreśl: recenzent nie widział, jak powstawał kod.
2. Ekranu wypłat jeszcze nie ma (slice 011), więc import robisz przez API – to też dobra okazja, żeby pokazać
   odpowiedź zgodną z kontraktem:
   `scripts/demo/import-payout.sh samples/generated/demopay_payout_PO-20261028-01.csv 318.40 2026-10-28`
   Wynik: `ReconciledWithDiscrepancies`, oczekiwana 318,40, różnica 0,00, 4 zgodne, 1 rozbieżność.
3. Lista płatności: A i B → „Środki otrzymane”, E → „Do wyjaśnienia” (rozbieżność z wypłaty dotarła do Orders i DataHub).

## 0:55–1:00 Podsumowanie

* Liczby z budowy POC: czas etapów, ile poprawek, co złapały reguły/hooki/testy architektury.
* Co dalej: pilot 4–6 tygodni na prawdziwym module, metryki z `docs/workflow.md`, właściciel standardów `.claude/`.
* (Jeśli działała druga karta) pokaż szkic specyfikacji 011 – „następny slice już czeka na akceptację”.

---

## Pytania, które prawdopodobnie padną

| Pytanie | Kierunek odpowiedzi |
|---|---|
| Czy to zastąpi programistów? | Nie – przesuwa pracę na decyzje, specyfikację i review. Bez człowieka nie ma akceptacji specyfikacji, kontraktu ani PR. |
| Skąd wiemy, że kod jest dobry? | Te same bramki co dla człowieka: testy per AC, testy architektury, test zgodności schematu, recenzent, review w PR. |
| A jak wymyśli API biblioteki? | Dlatego etap 1 to spike na prawdziwych wersjach i decyzja D-012; reguły każą sprawdzać dokumentację wersji z `Directory.Packages.props`. |
| Bezpieczeństwo kodu i danych | Zależy od planu i ustawień organizacji – do potwierdzenia z działem bezpieczeństwa przed pilotem. Sekrety blokujemy w `settings.json`. |
| Co z legacy bez testów? | Najpierw testy charakteryzujące z Claude, potem zmiany – osobny temat na pilota. |
| Kto utrzymuje `.claude/`? | Zespół, przez PR, jak kod. Każdy powtarzający się komentarz z review → reguła, test albo hook. |
