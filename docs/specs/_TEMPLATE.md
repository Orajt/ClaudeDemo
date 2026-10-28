# NNN – Tytuł slice'a (rzeczownik odczasownikowy, np. „Import raportu rozliczeniowego”)

| | |
|---|---|
| **Status** | Szkic · Zaakceptowana · W realizacji · Zaimplementowana |
| **Moduły** | Orders / Settlements / DataHub / GUI |
| **Autor** | imię (człowiek) + Claude |
| **Akceptuje** | imię |
| **Powiązane** | NNN-…, decyzje D-0xx |
| **Rekomendacja modelu** | informacyjnie: `<alias>`, effort `<poziom>` – <zasada z `docs/model-policy.md`> (warunkowo, jeśli polityka ma pozycje „do potwierdzenia”) |

<!--
Zasady pisania:
- Piszemy po polsku, nazwy z kodu w `backtickach` po angielsku zgodnie ze słownikiem modułu.
- Każde zdanie ma być sprawdzalne. Unikaj „szybko”, „wygodnie”, „poprawnie”.
- Jeśli czegoś nie wiesz – wpisz do sekcji 13 jako pytanie, nie zgaduj.
- Sekcje nieistotne dla slice'a zostaw z wpisem „Nie dotyczy” (nie usuwaj – to też informacja).
-->

## 1. Cel biznesowy

Kto, czego potrzebuje i po co. 2–4 zdania.

## 2. Zakres

**W zakresie:**
- …

**Poza zakresem:**
- …

## 3. Nowe pojęcia

| Pojęcie (PL) | Kod (EN) | Znaczenie |
|---|---|---|

## 4. Kryteria akceptacji

<!-- Numerowane, każde testowalne. Jedno zachowanie na kryterium. -->

- **AC-1** Gdy …, to …
- **AC-2** …

## 5. Przykłady

<!-- Konkretne liczby i identyfikatory. Przykłady stają się testami. Format Given / When / Then. -->

### P-1 (AC-1) Nazwa przykładu

| | |
|---|---|
| **Given** | stan / zdarzenia w strumieniu |
| **When** | komenda / zdarzenie integracyjne / plik |
| **Then** | zdarzenia domenowe, zdarzenia integracyjne, odpowiedź HTTP, stan widoku |

## 6. Model domeny

### Komendy

| Komenda | Pola | Kto wywołuje |
|---|---|---|

### Zdarzenia domenowe

| Zdarzenie | Pola | Agregat (strumień) |
|---|---|---|

### Reguły biznesowe

| Reguła | Kod błędu | HTTP |
|---|---|---|

### Zmiany agregatów / usług domenowych

…

## 7. Zdarzenia integracyjne

| Zdarzenie | Nowe / zmienione | Pola | Producent | Konsumenci | Reakcja konsumenta |
|---|---|---|---|---|---|

## 8. Kontrakt API

<!-- Zmiana w api/openapi/shop-api.yaml jako fragment YAML. Tę sekcję zatwierdza człowiek przed implementacją. -->

```yaml
# delta do shop-api.yaml
```

| Operacja | Rola | Odpowiedzi |
|---|---|---|

## 9. Baza danych

| Schemat | Zmiana | Źródło migracji | Czy usuwa dane? |
|---|---|---|---|
| `orders` | … | Marten `db-patch` | nie |

## 10. DataHub

| Widok | Kolumny / zmiana | Źródło (zdarzenie) | Wpływ na status złożony |
|---|---|---|---|

Zapytania (nowe/zmienione): …

## 11. Plan testów

| AC | Poziom (domena / aplikacja / integracja / architektura) | Test (opis zachowania) |
|---|---|---|
| AC-1 | domena | … |

## 12. GUI

Nie dotyczy / opis ekranu.

## 13. Ryzyka i pytania otwarte

- [ ] …

## 14. Notatki z implementacji

<!-- Wypełnia Claude w trakcie /implement-slice: odstępstwa od specyfikacji, decyzje techniczne, rzeczy do zrobienia później. -->
