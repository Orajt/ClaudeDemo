# Polityka modeli Claude w projekcie

Plik czytają skille (m.in. `/refine-task`) przy doborze modelu. Właściciel: zespół, zmiany przez PR.
Weryfikuj co najmniej raz na kwartał lub po ogłoszeniu nowych modeli.

**Ostatnia weryfikacja:** 2026-10-28 – UZUPEŁNIĆ: kto sprawdził
**Źródła:** https://docs.claude.com (modele, effort), ustawienia organizacji w claude.ai / konsoli

## Dostępne na planie organizacji

UZUPEŁNIĆ po sprawdzeniu w `/model` w Claude Code i u administratora planu.

| Alias | Model | Dostępny u nas | Uwagi |
|---|---|---|---|
| `haiku` | Claude Haiku 5.5 | do potwierdzenia | zmiany mechaniczne |
| `sonnet` | Claude Sonnet 5.5 | do potwierdzenia | domyślny do implementacji |
| `opus` | Claude Opus 5.5 | do potwierdzenia | refinement wielu źródeł, zmiany między modułami |
| – | Claude Fable 5.1 | do potwierdzenia | tylko za zgodą właściciela budżetu |

## Effort

Poziomy dostępne w Claude Code zależą od modelu – UZUPEŁNIĆ z `/model` lub dokumentacji.
Zasada zespołu: domyślny effort modelu; niższy tylko dla zmian mechanicznych.

## Zasady budżetowe zespołu

- Domyślny model sesji: `sonnet`.
- `opus`: refinement i podzadania oznaczone w raporcie jako `opus`.
- Najmocniejszy model: tylko po eskalacji z `opus` i decyzji człowieka.
