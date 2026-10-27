---
name: db-migration
description: Procedura tworzenia migracji Flyway po zmianie konfiguracji Martena, Wolverine lub modelu EF Core DataHub. Używaj zawsze, gdy zmieniasz snapshoty/dokumenty/indeksy Martena, encje lub konfigurację EF albo ustawienia magazynu wiadomości.
---

# Migracja bazy danych

Zasady: `.claude/rules/migrations.md`. Lista modułów, schematów i folderów: `db/flyway-modules.json`.

## 0. Przygotowanie

1. Lokalna baza musi mieć zastosowane **wszystkie dotychczasowe** migracje – inaczej różnica będzie zawierała
   cudze zmiany. Uruchom `scripts/db/migrate-local.sh` (Flyway na lokalnym Postgresie z AppHost).
2. Ustal moduł i schemat, którego dotyczy zmiana. Jedna migracja = jeden moduł.

## 1A. Moduł Martena (orders, settlements) lub magazyn wiadomości (wolverine)

```bash
scripts/db/new-migration.sh <moduł> <opis_snake_case>
# przykład: scripts/db/new-migration.sh orders add_payment_review_index
```

Skrypt:
- znajduje identyfikator bazy modułu (`dotnet run --project src/Host/Shop.Host -- db-list`),
- uruchamia `db-patch -d <id>` do katalogu tymczasowego,
- nadaje kolejny numer `V<NNN>__<opis>.sql` i przenosi plik do `Sql/` modułu, a `.drop` do `Rollback/`,
- kończy się błędem, jeśli nie ma różnic (wtedy sprawdź, czy zmiana konfiguracji została zarejestrowana).

## 1B. DataHub (EF Core)

```bash
scripts/db/new-migration.sh datahub <OpisPascalCase>
```

Skrypt dodaje migrację EF w `Shop.DataHub.Migrations` (`dotnet ef migrations add`) i generuje SQL od poprzedniej
migracji do nowej (`dotnet ef migrations script <poprzednia> <nowa>`) jako `V<NNN>__<opis>.sql` w `Sql/`.
Klasy migracji EF (C#) też commitujemy – są źródłem kolejnych różnic.

## 2. Przegląd SQL (obowiązkowy)

Przeczytaj wygenerowany plik i sprawdź:
- [ ] zmienia tylko schemat tego modułu,
- [ ] każde `DROP` / `ALTER … DROP` / `TRUNCATE` jest zamierzone i opisane w specyfikacji,
- [ ] zmiana typu kolumny nie utnie danych,
- [ ] nowe kolumny `NOT NULL` na istniejących tabelach mają wartość domyślną albo migrację danych,
- [ ] indeksy są tam, gdzie wymagają ich zapytania.

Wątpliwość → zatrzymaj się i opisz ją programiście. Nie poprawiaj ręcznie wygenerowanego SQL bez uzasadnienia
w komentarzu na początku pliku.

## 3. Weryfikacja

```bash
scripts/db/migrate-local.sh                                   # zastosuj nową migrację lokalnie
dotnet run --project src/Host/Shop.Host -- db-assert          # konfiguracja = baza
dotnet test tests/Shop.IntegrationTests --filter "FullyQualifiedName~SchemaConsistency"
dotnet test tests/Shop.ArchitectureTests                      # nazwy i numeracja plików
```

## 4. Gdy coś pójdzie nie tak

- Migracja z błędem **nie jest jeszcze na `main`** → usuń plik, popraw konfigurację, wygeneruj ponownie
  (lokalnie: `scripts/db/reset-local.sh` odtwarza bazę od zera).
- Migracja jest już na `main` → nowa migracja naprawcza. Edycja jest zablokowana hookiem – nie próbuj go obchodzić.
