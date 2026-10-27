#!/usr/bin/env bash
# Symulacja pipeline'u kontraktu API (D-007): walidacja api/openapi/shop-api.yaml i generowanie kodu
# openapi-generatorem w Dockerze.
#
#   serwer (abstrakcyjne kontrolery) → src/Api/Shop.Api.Contracts.Generated
#   klient dla GUI                   → src/Gui/Shop.Api.Client.Generated
#
# Oba katalogi są w całości produktem tego skryptu: przed generacją są czyszczone, a .openapi-generator-ignore
# i własny csproj (Central Package Management, bez TreatWarningsAsErrors) powstają z szablonów poniżej.
# W firmie tę rolę pełni pipeline publikujący paczkę do Artifactory.

set -euo pipefail

readonly GENERATOR_IMAGE="openapitools/openapi-generator-cli:v7.26.0"
readonly SPEC_PATH="api/openapi/shop-api.yaml"
readonly SERVER_DIR="src/Api/Shop.Api.Contracts.Generated"
readonly CLIENT_DIR="src/Gui/Shop.Api.Client.Generated"

readonly SERVER_OPTIONS="aspnetCoreVersion=10.0,buildTarget=library,classModifier=abstract,operationModifier=abstract,\
operationIsAsync=true,operationResultTask=true,useNewtonsoft=false,nullableReferenceTypes=true,useDateTimeOffset=true,\
packageName=Shop.Api.Contracts.Generated"
readonly CLIENT_OPTIONS="library=generichost,targetFramework=net10.0,packageName=Shop.Api.Client.Generated,\
nullableReferenceTypes=true,useDateTimeOffset=true"

# Ostrzeżenia kodu generowanego, którego nie poprawiamy (nie edytujemy go).
# CS8669: adnotacje "?" w pliku generowanym bez dyrektywy #nullable (.editorconfig oznacza katalog jako generated_code).
readonly GENERATED_NO_WARN='$(NoWarn);CS8669'

print_help() {
  cat <<EOF
Użycie: scripts/api/generate-contracts.sh [--help]

Waliduje ${SPEC_PATH} i generuje kod kontraktu (obraz ${GENERATOR_IMAGE}):
  - serwer: ${SERVER_DIR}
  - klient: ${CLIENT_DIR}

Wymaga Dockera. Uruchamiaj z dowolnego miejsca w repozytorium.
EOF
}

repository_root() {
  git -C "$(dirname "${BASH_SOURCE[0]}")" rev-parse --show-toplevel
}

run_generator() {
  # Repozytorium montujemy w /local – Docker (np. Rancher Desktop) widzi katalog domowy, ale nie /tmp hosta.
  docker run --rm \
    --user "$(id -u):$(id -g)" \
    --volume "${REPO_ROOT}:/local" \
    "${GENERATOR_IMAGE}" "$@"
}

validate_spec() {
  echo "Walidacja ${SPEC_PATH}..."
  run_generator validate --input-spec "/local/${SPEC_PATH}"
}

write_ignore_file() {
  local target_dir="$1"
  cat > "${target_dir}/.openapi-generator-ignore" <<'EOF'
# Plik tworzy scripts/api/generate-contracts.sh przy każdej generacji – nie edytuj.
# Projekt (csproj) jest nasz: Central Package Management, bez wersji pakietów i ustawień Dockera z generatora.
# Wzorce "**/" nie obejmują korzenia katalogu – dlatego pliki w korzeniu są wymienione osobno.
*.csproj
**/*.csproj
*.sln
**/*.sln
**/*.nuspec
README.md
**/README.md
build.*
docs/**
test/**
# Projekt testów klienta (generator csharp) i pliki CI generatora.
src/*.Test/**
appveyor.yml
api/**
.gitignore
**/.gitignore
EOF
}

write_server_project() {
  cat > "${SERVER_DIR}/Shop.Api.Contracts.Generated.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <!-- Plik tworzy scripts/api/generate-contracts.sh przy każdej generacji – nie edytuj. -->
  <PropertyGroup>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>false</EnforceCodeStyleInBuild>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
    <NoWarn>${GENERATED_NO_WARN}</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
</Project>
EOF
}

write_client_project() {
  cat > "${CLIENT_DIR}/Shop.Api.Client.Generated.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <!-- Plik tworzy scripts/api/generate-contracts.sh przy każdej generacji – nie edytuj. -->
  <PropertyGroup>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>false</EnforceCodeStyleInBuild>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
    <NoWarn>${GENERATED_NO_WARN}</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Http" />
    <PackageReference Include="Microsoft.Extensions.Hosting" />
    <PackageReference Include="Microsoft.Extensions.Http.Polly" />
    <PackageReference Include="Microsoft.Net.Http.Headers" />
  </ItemGroup>
</Project>
EOF
}

prepare_target_dir() {
  local target_dir="$1"
  rm -r -- "${target_dir:?}" 2>/dev/null || true
  mkdir -p "${target_dir}"
  write_ignore_file "${target_dir}"
}

generate_server() {
  echo "Generowanie serwera → ${SERVER_DIR}"
  prepare_target_dir "${SERVER_DIR}"
  write_server_project
  run_generator generate \
    --input-spec "/local/${SPEC_PATH}" \
    --generator-name aspnetcore \
    --output "/local/${SERVER_DIR}" \
    --additional-properties "${SERVER_OPTIONS}"
}

generate_client() {
  echo "Generowanie klienta → ${CLIENT_DIR}"
  prepare_target_dir "${CLIENT_DIR}"
  write_client_project
  run_generator generate \
    --input-spec "/local/${SPEC_PATH}" \
    --generator-name csharp \
    --output "/local/${CLIENT_DIR}" \
    --additional-properties "${CLIENT_OPTIONS}"
}

main() {
  if [[ "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
    print_help
    exit 0
  fi

  REPO_ROOT="$(repository_root)"
  readonly REPO_ROOT
  cd "${REPO_ROOT}"

  validate_spec
  generate_server
  generate_client
  echo "Gotowe. Zbuduj rozwiązanie: dotnet build Shop.slnx"
}

main "$@"
