#!/usr/bin/env bash
# Hook PreToolUse (Edit|Write): blokuje zmianę migracji, która jest już na gałęzi bazowej (domyślnie main).
#
# Dlaczego: migracja zastosowana w jakimkolwiek środowisku jest historią bazy. Jej edycja rozjeżdża sumy kontrolne
# Flyway i schemat między środowiskami. Poprawka = nowa migracja.
#
# Wejście: JSON na stdin (pole tool_input.file_path). Blokada: exit 2 + komunikat na stderr (Claude go widzi).
# Gałąź bazową można zmienić zmienną MIGRATIONS_BASE_REF (np. origin/main).

set -uo pipefail

read_file_path() {
  local input="$1"
  if command -v jq >/dev/null 2>&1; then
    jq -r '.tool_input.file_path // empty' <<<"$input"
  elif command -v python3 >/dev/null 2>&1; then
    python3 -c 'import json,sys; print(json.load(sys.stdin).get("tool_input",{}).get("file_path",""))' <<<"$input"
  else
    echo "protect-applied-migrations: brak jq i python3 – nie mogę sprawdzić ścieżki. Zainstaluj jq." >&2
    exit 2
  fi
}

is_migration_file() {
  case "$1" in
    */*.Migrations/Sql/*.sql) return 0 ;;          # skrypty Flyway wszystkich modułów
    */Shop.DataHub.Migrations/EfMigrations/*.cs) return 0 ;;  # klasy migracji EF (źródło różnic)
    *) return 1 ;;
  esac
}

main() {
  local input file_path project_dir relative_path base_ref
  input="$(cat)"
  file_path="$(read_file_path "$input")"

  [[ -z "$file_path" ]] && exit 0
  is_migration_file "$file_path" || exit 0

  project_dir="${CLAUDE_PROJECT_DIR:-$(pwd)}"
  git -C "$project_dir" rev-parse --is-inside-work-tree >/dev/null 2>&1 || exit 0

  base_ref="${MIGRATIONS_BASE_REF:-main}"
  # Brak gałęzi bazowej (np. świeże repo) – nie ma czego chronić.
  git -C "$project_dir" rev-parse --verify --quiet "$base_ref" >/dev/null || exit 0

  relative_path="$(realpath -m --relative-to="$(git -C "$project_dir" rev-parse --show-toplevel)" "$file_path")"

  if git -C "$project_dir" cat-file -e "${base_ref}:${relative_path}" 2>/dev/null; then
    cat >&2 <<EOF
ZABLOKOWANE: ${relative_path} jest już na gałęzi ${base_ref}, więc mogła zostać zastosowana w innych środowiskach.
Nie zmieniaj istniejącej migracji. Utwórz nową migrację naprawczą (skill db-migration).
EOF
    exit 2
  fi

  exit 0
}

main
