#!/usr/bin/env bash
set -euo pipefail
# shellcheck source=../lib/local-db.sh
source "$(dirname "${BASH_SOURCE[0]}")/../lib/local-db.sh"

MESSAGING_DATABASE_ID="wolverine://messages/main"
DATAHUB_NOT_READY_EXIT_CODE=2
MIGRATION_NUMBER_WIDTH=3

usage() {
  cat <<'HELP'
Użycie: scripts/db/new-migration.sh <moduł> <opis_snake_case>

Generuje migrację Flyway z różnic między konfiguracją Martena/Wolverine a lokalną bazą (db-patch)
i zapisuje ją jako Sql/V<NNN>__<opis>.sql w katalogu modułu z db/flyway-modules.json.
Plik rollbacku (.drop) trafia do katalogu Rollback/ obok Sql/ – nie jest uruchamiany przez Flyway.

Moduły: messaging, orders, settlements (datahub – dopiero w etapie 5, kod wyjścia 2).
Przykład: scripts/db/new-migration.sh orders add_payment_review_index

Przed użyciem lokalna baza musi mieć zastosowane wszystkie dotychczasowe migracje (scripts/db/migrate-local.sh),
inaczej różnica zawiera cudze zmiany. Po wygenerowaniu OBOWIĄZKOWO przejrzyj SQL pod kątem DROP.
HELP
}

if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
  usage
  exit 0
fi

if [[ $# -ne 2 ]]; then
  usage >&2
  fail "podaj moduł i opis migracji."
fi

module="$1"
description="$2"

[[ "$description" =~ ^[a-z][a-z0-9_]*$ ]] || fail "opis musi być w snake_case (małe litery, cyfry, podkreślenia), np. add_payment_index."

if [[ "$module" == "datahub" ]]; then
  echo "Migracje DataHub (EF Core) powstaną w etapie 5 – klasa DataHubDbContext jeszcze nie istnieje." >&2
  exit "$DATAHUB_NOT_READY_EXIT_CODE"
fi

require_command jq
require_command dotnet
location="$(module_field "$module" location)"
[[ -n "$location" ]] || fail "nieznany moduł '$module'. Dostępne: $(module_names | tr '\n' ' ')"

sql_directory="$REPO_ROOT/$location"
rollback_directory="$(dirname "$sql_directory")/Rollback"

host_command() {
  ASPNETCORE_ENVIRONMENT=Development dotnet run --project "$REPO_ROOT/src/Host/Shop.Host" --no-launch-profile -- "$@"
}

# db-patch wybiera bazę po URI. Dla Martena URI zawiera schemat modułu, magazyn wiadomości ma stały identyfikator.
database_id_for_module() {
  if [[ "$module" == "messaging" ]]; then
    echo "$MESSAGING_DATABASE_ID"
    return
  fi

  local schema
  schema="$(module_field "$module" schema)"
  host_command db-list | grep -oE "postgresql://[^ │]+/${DATABASE_NAME}/${schema}" | head -n 1
}

next_migration_number() {
  local last
  last="$(find "$sql_directory" -maxdepth 1 -name 'V*__*.sql' -printf '%f\n' 2>/dev/null | sed -E 's/^V([0-9]+)__.*/\1/' | sort -n | tail -n 1)"
  printf "%0${MIGRATION_NUMBER_WIDTH}d" "$((10#${last:-0} + 1))"
}

database_id="$(database_id_for_module)"
[[ -n "$database_id" ]] || fail "db-list nie zwróciło bazy modułu '$module'."

work_directory="$(mktemp -d)"
trap 'rm -rf "$work_directory"' EXIT

echo "==> db-patch dla bazy: $database_id"
host_command db-patch "$work_directory/patch.sql" -d "$database_id" >/dev/null

if [[ ! -f "$work_directory/patch.sql" ]]; then
  fail "brak różnic między konfiguracją a bazą – sprawdź, czy zmiana konfiguracji została zarejestrowana (i czy baza jest zmigrowana)."
fi

migration_file="V$(next_migration_number)__${description}.sql"
mkdir -p "$sql_directory" "$rollback_directory"
mv "$work_directory/patch.sql" "$sql_directory/$migration_file"
[[ ! -f "$work_directory/patch.drop.sql" ]] || mv "$work_directory/patch.drop.sql" "$rollback_directory/${migration_file%.sql}.drop.sql"

echo "Utworzono: $location/$migration_file"
echo "Przejrzyj SQL (szukaj DROP / ALTER … DROP / TRUNCATE) i zastosuj: scripts/db/migrate-local.sh"
