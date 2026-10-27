---
paths:
  - "src/**/*.Migrations/**"
  - "db/**"
---

# Migracje bazy (Flyway)

Procedura krok po kroku: skill `db-migration`. Tu tylko zasady.

* Jedynym wykonawcą zmian schematu jest Flyway. Każdy moduł ma swój folder `Sql/` i własną tabelę historii
  w swoim schemacie. Lista modułów: `db/flyway-modules.json`.
* Nazwa pliku: `V<NNN>__<opis_snake_case>.sql`, numeracja ciągła w obrębie modułu (`V001`, `V002`…).
  Pilnuje tego test w `Shop.ArchitectureTests`.
* **Nie ręcznie, tylko z generatora**: Marten/Wolverine → `db-patch` (Weasel), DataHub → `dotnet ef migrations script`.
  Ręczny SQL tylko dla migracji danych – wtedy z komentarzem „dlaczego” na początku pliku.
* **Migracja na `main` jest nienaruszalna.** Hook `.claude/hooks/protect-applied-migrations.sh` blokuje jej edycję.
  Błąd w migracji = nowa migracja naprawcza.
* Przed dodaniem pliku przejrzyj SQL: każde `DROP` (kolumny, indeksu, tabeli, funkcji) musi być zamierzone
  i opisane w specyfikacji. Marten w trybie aktualizacji potrafi usuwać kolumny, których konfiguracja już nie zna.
* Plik `.drop` z generatora Weasel (rollback) nie trafia do `Sql/` – odkładasz go do `Rollback/`.
* Flyway działa z `-placeholderReplacement=false` (SQL Martena zawiera znaki `$`).
* Obrazy Postgresa i Flyway są tylko w `db/flyway-modules.json` (`postgresImage`, `flywayImage`, decyzja D-015) –
  AppHost, testy i skrypty czytają je stamtąd. Nie wpisujesz wersji obrazu nigdzie indziej.
* Weryfikacja po migracji: test zgodności schematu w `Shop.IntegrationTests` oraz
  `dotnet run --project src/Host/Shop.Host -- db-assert` na lokalnej bazie.
