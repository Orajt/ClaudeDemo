#!/usr/bin/env bash
set -euo pipefail
# shellcheck source=../lib/local-db.sh
source "$(dirname "${BASH_SOURCE[0]}")/../lib/local-db.sh"

CONFIRMATION_WORD="tak"

if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
  cat <<'HELP'
Użycie: scripts/db/reset-local.sh

Usuwa schematy wszystkich modułów z db/flyway-modules.json (razem z danymi i historią Flyway)
z lokalnej bazy, a następnie stosuje migracje od zera (scripts/db/migrate-local.sh).

Przed usunięciem prosi o potwierdzenie – wpisz "tak". Każda inna odpowiedź (lub brak odpowiedzi) niczego nie usuwa.
HELP
  exit 0
fi

require_command docker
require_command jq

postgres_container="$(find_postgres_container)"
schemas="$(jq -r '[.modules[].schema] | join(", ")' "$MODULES_FILE")"

echo "UWAGA: zostaną usunięte WSZYSTKIE dane ze schematów: $schemas (baza '$DATABASE_NAME')."
read -r -p "Wpisz \"$CONFIRMATION_WORD\", aby kontynuować: " answer || answer=""
if [[ "$answer" != "$CONFIRMATION_WORD" ]]; then
  echo "Przerwano – nic nie zostało usunięte."
  exit 1
fi

for schema in $(jq -r '.modules[].schema' "$MODULES_FILE"); do
  echo "==> Usuwam schemat '$schema'"
  docker exec "$postgres_container" psql -U "$POSTGRES_USER" -d "$DATABASE_NAME" -v ON_ERROR_STOP=1 \
    -c "DROP SCHEMA IF EXISTS \"$schema\" CASCADE;"
done

"$(dirname "${BASH_SOURCE[0]}")/migrate-local.sh"
