#!/usr/bin/env bash
# Wspólne funkcje skryptów pracujących na lokalnej bazie z AppHost. Plik jest "source'owany", nie uruchamiany.

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
MODULES_FILE="$REPO_ROOT/db/flyway-modules.json"
DATABASE_NAME="shop"
POSTGRES_USER="postgres"
POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-shop-dev-password}"

fail() {
  echo "BŁĄD: $*" >&2
  exit 1
}

require_command() {
  command -v "$1" >/dev/null 2>&1 || fail "Brak polecenia '$1' w PATH."
}

# Aspire nazywa kontener "postgres-<losowy sufiks>".
find_postgres_container() {
  local container
  container="$(docker ps --format '{{.Names}}' | grep -E '^postgres-' | head -n 1 || true)"
  [[ -n "$container" ]] || fail "Nie znaleziono kontenera Postgres z AppHost. Uruchom: dotnet run --project src/Host/Shop.AppHost"
  echo "$container"
}

flyway_image() {
  jq -r '.flywayImage' "$MODULES_FILE"
}

module_names() {
  jq -r '.modules[].name' "$MODULES_FILE"
}

module_field() {
  local module="$1" field="$2"
  jq -r --arg module "$module" --arg field "$field" '.modules[] | select(.name == $module) | .[$field]' "$MODULES_FILE"
}

# Flyway współdzieli sieć z kontenerem Postgres (port hosta 55432 to proxy po stronie hosta, niewidoczny z kontenera).
run_flyway_for_module() {
  local module="$1" postgres_container="$2"
  local schema location
  schema="$(module_field "$module" schema)"
  location="$REPO_ROOT/$(module_field "$module" location)"
  [[ -d "$location" ]] || fail "Brak katalogu migracji modułu '$module': $location"

  local extra_arguments=()
  while IFS= read -r argument; do
    extra_arguments+=("$argument")
  done < <(jq -r '.flywayArguments[]' "$MODULES_FILE")

  echo "==> Flyway: moduł '$module' (schemat '$schema')"
  docker run --rm \
    --network "container:$postgres_container" \
    -v "$location:/flyway/sql:ro" \
    "$(flyway_image)" migrate \
    "-url=jdbc:postgresql://localhost:5432/$DATABASE_NAME" \
    "-user=$POSTGRES_USER" \
    "-password=$POSTGRES_PASSWORD" \
    "-schemas=$schema" \
    "-locations=filesystem:/flyway/sql" \
    "${extra_arguments[@]}"
}
