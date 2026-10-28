#!/usr/bin/env bash
set -euo pipefail

KEYCLOAK_URL="${KEYCLOAK_URL:-http://localhost:8080}"
REALM="shop"
CLIENT_ID="shop-dev-cli"
DEVELOPMENT_PASSWORD="Pass123!"

usage() {
  cat <<'HELP'
Użycie: scripts/dev/get-token.sh <użytkownik>

Wypisuje access token użytkownika deweloperskiego z Keycloaka (klient shop-dev-cli, password grant).
Użytkownicy (hasło Pass123!): anna (sales), jan (accounting), demopay (gateway).

Zmienna KEYCLOAK_URL zmienia adres Keycloaka (domyślnie http://localhost:8080).
Przykład: curl -H "Authorization: Bearer $(scripts/dev/get-token.sh anna)" http://localhost:5080/api/orders
HELP
}

if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
  usage
  exit 0
fi

if [[ $# -ne 1 ]]; then
  usage >&2
  echo "BŁĄD: podaj dokładnie jednego użytkownika." >&2
  exit 1
fi

command -v curl >/dev/null 2>&1 || { echo "BŁĄD: brak polecenia 'curl'." >&2; exit 1; }
command -v jq >/dev/null 2>&1 || { echo "BŁĄD: brak polecenia 'jq'." >&2; exit 1; }

response="$(curl --silent --show-error --fail-with-body \
  --data-urlencode "grant_type=password" \
  --data-urlencode "client_id=$CLIENT_ID" \
  --data-urlencode "username=$1" \
  --data-urlencode "password=$DEVELOPMENT_PASSWORD" \
  "$KEYCLOAK_URL/realms/$REALM/protocol/openid-connect/token")" || {
  echo "BŁĄD: nie udało się pobrać tokenu (czy AppHost działa, a użytkownik '$1' istnieje?)." >&2
  exit 1
}

echo "$response" | jq -r '.access_token'
