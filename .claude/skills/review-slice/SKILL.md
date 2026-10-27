---
name: review-slice
description: Niezależny przegląd zmian względem specyfikacji i standardów projektu, wykonywany przez agenta slice-reviewer w świeżym kontekście.
argument-hint: "<ścieżka do specyfikacji> [gałąź bazowa, domyślnie main]"
disable-model-invocation: true
context: fork
agent: slice-reviewer
background: false
allowed-tools: Bash(git status *) Bash(git log *) Bash(git diff *)
---

# Przegląd slice'a

Argumenty: `$ARGUMENTS` – pierwszy to ścieżka specyfikacji, drugi (opcjonalny) to gałąź bazowa (domyślnie `main`).

## Stan repozytorium w chwili wywołania

```!
git status --short
git log --oneline -10
```

## Zadanie

1. Przeczytaj specyfikację oraz `CLAUDE.md`, `docs/architecture.md` i reguły w `.claude/rules/` dla dotkniętych warstw.
2. Ustal zakres zmian: `git diff <baza>...HEAD` oraz niezacommitowane zmiany (`git diff`, `git diff --cached`).
3. Przejdź listę kontrolną z instrukcji agenta.
4. Uruchom `dotnet build Shop.slnx` i `dotnet test Shop.slnx` – wynik jest częścią raportu.
5. Zwróć raport w formacie z instrukcji agenta. Nie zmieniaj żadnych plików.
