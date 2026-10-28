#!/usr/bin/env bash
set -euo pipefail
# shellcheck source=../lib/local-db.sh
source "$(dirname "${BASH_SOURCE[0]}")/../lib/local-db.sh"

if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
  cat <<'HELP'
Użycie: scripts/db/migrate-local.sh

Stosuje migracje Flyway wszystkich modułów z db/flyway-modules.json (w kolejności z pliku)
na lokalnej bazie Postgres uruchomionej przez AppHost.

Wymagania: Docker, jq oraz działający AppHost (dotnet run --project src/Host/Shop.AppHost).
Hasło dev można nadpisać zmienną POSTGRES_PASSWORD.
HELP
  exit 0
fi

require_command docker
require_command jq

postgres_container="$(find_postgres_container)"
for module in $(module_names); do
  run_flyway_for_module "$module" "$postgres_container"
done
echo "Gotowe: migracje wszystkich modułów zastosowane."
