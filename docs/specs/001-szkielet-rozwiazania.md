# 001 – Szkielet rozwiązania i infrastruktura (etap 2)

| | |
|---|---|
| **Status** | Zaimplementowana |
| **Moduły** | Orders / Settlements / DataHub (tylko szkielet) · Host · AppHost · Shared |
| **Autor** | Marcin + Claude |
| **Akceptuje** | Marcin |
| **Powiązane** | `prompts/02-szkielet-rozwiazania.md`, `prompts/02-postep.md`, decyzje D-004, D-010, D-011, D-012, D-013, D-014, D-015 |

## 1. Cel biznesowy

Programista dostaje kompletny, pusty biznesowo szkielet: `dotnet run --project src/Host/Shop.AppHost` stawia Postgres,
Flyway i Keycloak oraz uruchamia Host, a `dotnet test Shop.slnx` przechodzi. Etapy 3–5 dodają już tylko logikę modułów –
bez dotykania infrastruktury, uwierzytelniania, migracji ani testów architektury. Slice nie ma funkcji biznesowych
widocznych dla użytkownika sklepu.

## 2. Zakres

**W zakresie** (sekcje A–I promptu etapu 2):
- A. solution, konwencje, projekty z referencjami wg reguł zależności, `Shop.SharedKernel`,
- B. generowanie kontraktu API skryptem i kontrolery-zaślepki modułów,
- C. Host: ServiceDefaults, uwierzytelnianie JWT, polityki, ProblemDetails, bazowa konfiguracja Wolverine, rejestracja modułów,
- D. AppHost (Postgres, Flyway per moduł, Keycloak, Host),
- E. realm Keycloak `shop`,
- F. skrypty `scripts/db/*` i `scripts/dev/get-token.sh`,
- G. migracje startowe (magazyn wiadomości Wolverine; moduły Martena tylko przy realnych różnicach),
- H. testy architektury i testy integracyjne (`/health`, zgodność schematu),
- I. `README.md`.

**Poza zakresem:**
- logika, agregaty, zdarzenia i handlery modułów (etapy 3–5), widoki DataHub i klasy EF (etap 5), GUI (etap 6),
- projekty testów jednostkowych modułów (etapy 3–5),
- zmiana `api/openapi/shop-api.yaml` (kontrakt bez zmian – sekcja 8),
- `TypeLoadMode.Static` i `codegen write` (D-013, nie podnosimy ponownie),
- automatyczny test AppHost (wymagałby nowego pakietu `Aspire.Hosting.Testing` – pytanie Q-2).

**Stan prac na dzień specyfikacji** (z `prompts/02-postep.md`): A i B wykonane; C napisane, build czerwony
(brak `using Microsoft.Extensions.Logging;` w `ServiceDefaultsExtensions.cs`, błąd CS1501); D–I nie rozpoczęte.
Plan ze wstępnych uzgodnień został zaakceptowany; ta specyfikacja zamienia go na sprawdzalne kryteria.

## 3. Nowe pojęcia

| Pojęcie (PL) | Kod (EN) | Znaczenie |
|---|---|---|
| Polityka autoryzacji | `AuthorizationPolicies` | stałe nazw polityk (`Sales`, `Accounting`, `Gateway`, `SalesOrAccounting`) w `Shop.SharedKernel`, bo Api nie może referencjonować Host |
| Rozszerzenie Wolverine modułu | `<Moduł>WolverineExtension` | `IWolverineExtension` dołączane w Host przez `options.Include<…>()`; zawiera politykę store'a modułu |
| Moduł migracji | wpis w `db/flyway-modules.json` | schemat + katalog `Sql/` z własną historią Flyway |
| Użytkownik deweloperski | `anna`, `jan`, `demopay` | konta realmu `shop` (hasło `Pass123!`, tylko dev) z rolami `sales`, `accounting`, `gateway` |

## 4. Kryteria akceptacji

**Solution i konwencje**
- **AC-1** `dotnet build Shop.slnx` kończy się bez błędów i bez ostrzeżeń (ostrzeżenia = błędy).
- **AC-2** Referencje projektów są zgodne z regułami zależności z `docs/architecture.md`: Domain bez Marten/Wolverine/EF/ASP.NET; Application bez Infrastructure/Api i bez Domain/Application innych modułów; Api bez Infrastructure i bez Marten/EF; Contracts bez zależności projektowych; DataHub bez Domain innych modułów.
- **AC-3** `Shop.SharedKernel` udostępnia `BusinessRuleViolationException(code, message)`, `NotFoundException(code, message)` i `DuplicateException(code, message)`; każdy z nich wystawia `Code`.

**Kontrakt API**
- **AC-4** `scripts/api/generate-contracts.sh` waliduje `shop-api.yaml` i generuje serwer do `src/Api/Shop.Api.Contracts.Generated` oraz klienta do `src/Gui/Shop.Api.Client.Generated`; kolejne uruchomienie na niezmienionym kontrakcie nie zmienia żadnego pliku (`git status` czysty).
- **AC-5** Każda operacja z `shop-api.yaml` ma nadpisaną metodę w kontrolerze modułu (`OrdersController`, `SettlementsController`, `DataHubController`), z polityką zgodną z rolą w kontrakcie, i rzuca `NotImplementedException("Etap 3/4/5")`.

**Host i bezpieczeństwo**
- **AC-6** Wywołanie chronionego endpointu bez tokenu zwraca 401; z tokenem bez wymaganej roli – 403; z tokenem z wymaganą rolą w claimie `roles` dociera do kontrolera (zaślepka → 500, patrz AC-7).
- **AC-7** Wyjątki są mapowane globalnie na `application/problem+json` z polem `code`: `ValidationException` → 400 `validation-failed` + `errors`; `NotFoundException` / `RequiredDataMissingException` → 404; konflikt współbieżności Martena → 409 `concurrency-conflict`; `DuplicateException` → 409 z kodem wyjątku; `BusinessRuleViolationException` → 422 z kodem wyjątku; pozostałe (w tym `NotImplementedException`) → 500.
- **AC-8** `GET /health` zwraca 200.
- **AC-9** Wolverine w Host ma ustawione: schemat magazynu wiadomości `wolverine`, `MessageIdentity.IdAndDestination`, `MultipleHandlerBehavior.Separated`, trwałe kolejki lokalne, `AutoApplyTransactions`, `AutoCreate.None` dla magazynu wiadomości i stored Martena, `OnMissing.ThrowException`, `TypeLoadMode.Dynamic`.

**AppHost, Keycloak, skrypty**
- **AC-10** `dotnet run --project src/Host/Shop.AppHost` uruchamia Postgres (port hosta `55432`, hasło dev `shop-dev-password`, wolumen danych, baza `shop`), po jednym zasobie Flyway na moduł z `db/flyway-modules.json` w kolejności z pliku, Keycloak (port `8080`) i Host; Host startuje dopiero po zakończeniu wszystkich migracji i gotowości Keycloaka; wszystkie zasoby są zdrowe, a każdy Flyway kończy się sukcesem.
- **AC-11** `shop-realm.json` definiuje realm `shop`; role realmu `sales`, `accounting`, `gateway`; klientów `shop-api`, `shop-gui` (confidential, authorization code) i `shop-dev-cli` (public, direct access grants); mapper audience `shop-api` oraz mapper ról realmu do płaskiego claimu `roles` w access tokenie; użytkowników `anna` (sales), `jan` (accounting), `demopay` (gateway).
- **AC-12** `scripts/dev/get-token.sh <użytkownik>` wypisuje access token z `shop-dev-cli`; token `anna` ma `aud` = `shop-api` i `roles` = `["sales"]`.
- **AC-13** Skrypty `scripts/db/migrate-local.sh`, `reset-local.sh`, `new-migration.sh` i `scripts/dev/get-token.sh` mają `set -euo pipefail`, komunikaty po polsku i `--help` kończący się kodem 0.
- **AC-14** `migrate-local.sh` stosuje migracje wszystkich modułów z `db/flyway-modules.json` na lokalnej bazie z AppHost; `reset-local.sh` przed usunięciem schematów modułów prosi o potwierdzenie i bez potwierdzenia niczego nie usuwa.

**Baza i testy**
- **AC-15** Baza zbudowana wyłącznie przez Flyway z `db/flyway-modules.json` nie ma różnic względem konfiguracji Martena (Orders, Settlements) i Wolverine (magazyn wiadomości); `dotnet run --project src/Host/Shop.Host -- db-assert` oraz `SchemaConsistencyTests` to potwierdzają.
- **AC-16** `Shop.Messaging.Migrations/Sql/V001__wolverine_message_storage.sql` powstaje z `db-patch`; migracje Orders i Settlements powstają tylko wtedy, gdy `db-patch` zwraca różnice (puste migracje nie istnieją); żadna migracja nie zawiera niezamierzonego `DROP`.
- **AC-17** Pliki migracji każdego modułu z `db/flyway-modules.json` mają nazwę `V<NNN>__<snake_case>.sql`, numerację ciągłą od `V001` i bez duplikatów.
- **AC-18** Każde zdarzenie integracyjne w projekcie `*.Contracts` ma nazwę z sufiksem `IntegrationEvent`.
- **AC-19** `Shop.IntegrationTests` startują Postgres z Testcontainers (obraz z `postgresImage`), uruchamiają Flyway w kontenerach wg `db/flyway-modules.json` i hostują aplikację przez `AlbaHost.For<Program>` ze stubem uwierzytelniania z rolami.
- **AC-20** `dotnet test Shop.slnx` kończy się zielono, w tym `Shop.ArchitectureTests` i `SchemaConsistencyTests`.

**Dokumentacja**
- **AC-21** `README.md` zawiera wymagania, uruchomienie, użytkowników testowych i najważniejsze polecenia; `CLAUDE.md` jest zaktualizowany, jeśli któreś polecenie się zmieniło.

## 5. Przykłady

Slice techniczny – przykłady to scenariusze weryfikacji. Każdy jest też krokiem weryfikacji końcowej promptu.

### P-1 (AC-1, AC-20) Czysty build i testy

| | |
|---|---|
| **Given** | czyste repozytorium po realizacji A–I, Docker działa |
| **When** | `dotnet build Shop.slnx`, potem `dotnet test Shop.slnx` |
| **Then** | `0 Warning(s)`, `0 Error(s)`; wszystkie testy zielone, w tym architektury i zgodności schematu |

### P-2 (AC-6, AC-7, AC-12) Uwierzytelnianie i routing na zaślepce

| | |
|---|---|
| **Given** | AppHost działa, Host na porcie `5080` |
| **When** | `POST /api/orders` (operacja `PlaceOrder`, rola `sales`) trzy razy: bez tokenu; z tokenem `jan`; z tokenem `anna` (`scripts/dev/get-token.sh anna`) |
| **Then** | bez tokenu **401**; `jan` (accounting) **403**; `anna` **500** `application/problem+json` z komunikatem o `NotImplementedException` („Etap 3”) |

### P-3 (AC-6) Rola alternatywna `SalesOrAccounting`

| | |
|---|---|
| **Given** | endpoint `GET /api/datahub/order-payments` (kontrakt: „Role: sales, accounting”) |
| **When** | wywołanie z tokenem `anna`, potem `jan`, potem `demopay` |
| **Then** | `anna` i `jan` docierają do zaślepki (500), `demopay` (gateway) dostaje 403 |

### P-4 (AC-7) Mapowanie wyjątków

| | |
|---|---|
| **Given** | mapper wyjątków w Host |
| **When** | `new BusinessRuleViolationException("orders.refund-not-allowed", "…")` |
| **Then** | status 422, `code` = `orders.refund-not-allowed`; analogicznie `NotFoundException("orders.not-found", …)` → 404, `DuplicateException("settlements.report-already-imported", …)` → 409 z tym kodem |

### P-5 (AC-15, AC-16) Zgodność schematu

| | |
|---|---|
| **Given** | pusta baza, Flyway zastosował `V001` (wolverine) i ewentualne migracje Orders/Settlements |
| **When** | `db-assert` na Host z `appsettings.Development.json` (port `55432`) |
| **Then** | „No database differences detected”, kod wyjścia 0; po usunięciu `V001` test `SchemaConsistencyTests` jest czerwony (dowód, że test coś pilnuje) |

### P-6 (AC-17) Numeracja migracji

| | |
|---|---|
| **Given** | `Sql/` modułu zawiera `V001__a.sql`, `V003__b.sql` |
| **When** | test numeracji |
| **Then** | czerwony: brak `V002`; analogicznie czerwony dla `V1__a.sql` (zła nazwa) i dla dwóch plików `V002__…` |

### P-7 (AC-2) Złamanie reguły zależności

| | |
|---|---|
| **Given** | `Shop.Orders.Domain` referencjonuje pakiet `WolverineFx` |
| **When** | testy architektury |
| **Then** | test zależności Domain jest czerwony i wskazuje projekt oraz pakiet |

## 6. Model domeny

Brak logiki domenowej. Jedyne typy: wyjątki w `Shop.SharedKernel` (AC-3).

### Komendy / Zdarzenia domenowe

Nie dotyczy.

### Reguły biznesowe (reguły infrastrukturalne)

| Reguła | Kod błędu | HTTP |
|---|---|---|
| walidacja wejścia (FluentValidation) | `validation-failed` | 400 |
| brak zasobu / agregatu | `<moduł>.not-found` / `not-found` | 404 |
| konflikt współbieżności Martena | `concurrency-conflict` | 409 |
| duplikat | kod z `DuplicateException` | 409 |
| naruszenie reguły biznesowej | kod z `BusinessRuleViolationException` | 422 |
| niezaimplementowane (zaślepki etapów 3–5) | brak | 500 |

### Zmiany agregatów / usług domenowych

Nie dotyczy.

## 7. Zdarzenia integracyjne

Nie dotyczy (brak zdarzeń; w tym etapie powstaje tylko konwencja nazw i jej test – AC-18).

## 8. Kontrakt API

Bez zmian w `api/openapi/shop-api.yaml`. Etap zmienia wyłącznie sposób generowania kodu (AC-4) i dodaje zaślepki kontrolerów.

| Operacja | Rola (polityka) | Odpowiedzi w etapie 2 |
|---|---|---|
| `PlaceOrder`, `CancelOrder`, `RefundOrder` | `sales` (`Sales`) | 401 / 403 / 500 (zaślepka) |
| `ConfirmPayment` | `gateway` (`Gateway`) | 401 / 403 / 500 |
| `ImportSettlementReport` | `accounting` (`Accounting`) | 401 / 403 / 500 |
| `ListOrderPayments`, `GetOrderPayment` | `sales` lub `accounting` (`SalesOrAccounting`) | 401 / 403 / 500 |
| `ListSettlementReports`, `ListSettlementDiscrepancies` | `accounting` (`Accounting`) | 401 / 403 / 500 |

## 9. Baza danych

| Schemat | Zmiana | Źródło migracji | Czy usuwa dane? |
|---|---|---|---|
| `wolverine` | tabele magazynu wiadomości (inbox/outbox, kolejki, dead letters, węzły) | `db-patch` (`-d wolverine://messages/main`) → `V001__wolverine_message_storage.sql` | nie |
| `orders` | `mt_doc_envelope`, `mt_doc_deadletterevent` – **tylko jeśli** `db-patch` pokaże różnice | `db-patch` store'a Orders | nie |
| `settlements` | jak wyżej | `db-patch` store'a Settlements | nie |
| `datahub` | brak (klasy EF dopiero w etapie 5; katalog `Sql/` z `.gitkeep`) | – | nie |

Przy zerowej konfiguracji Martena (brak agregatów) tabele zdarzeń Martena (`mt_events`, `mt_streams`) też mogą pojawić się
w patchu – przeglądam SQL pod kątem niezamierzonych `DROP` zgodnie z `migrations.md`. W etapach 3–4 dojdą kolejne migracje
(`V002…`), nie edycja `V001`.

## 10. DataHub

Nie dotyczy (widoki w etapie 5). Wpływ etapu: istnieje `DataHubWolverineExtension` z polityką ponawiania
(`DbUpdateConcurrencyException`, `23505`) wg D-012.

## 11. Plan testów

| AC | Poziom | Test (opis zachowania) |
|---|---|---|
| AC-1 | build | `dotnet build Shop.slnx` – 0 ostrzeżeń (wynik pokazany w raporcie) |
| AC-2 | architektura | reguły na referencjach z `*.csproj` (pakiety zakazane w Domain/Api, projekty zakazane między warstwami i modułami) + ArchUnitNET z `WithoutRequiringPositiveResults()` na typach |
| AC-3 | integracja (projekt `Shop.IntegrationTests`, klasa `SharedKernelTests`) | każdy wyjątek niesie `Code` i `Message` |
| AC-4 | skrypt (ręcznie, raport) | dwa uruchomienia skryptu; `git status` czysty po drugim |
| AC-5 | integracja | refleksyjny test pokrycia: każda abstrakcyjna metoda `*ApiController` ma nadpisanie w module (kompilacja to wymusza) + atrybut `[Authorize(Policy=…)]` zgodny z tabelą z sekcji 8 |
| AC-6 | integracja (Alba) | `POST /api/orders` – 401 / 403 / 500; `GET /api/datahub/order-payments` dla `sales` i `accounting` → 500, dla `gateway` → 403 |
| AC-7 | integracja | mapper wywołany dla każdego wyjątku z tabeli; asercja statusu i `code` w ProblemDetails; zaślepka → 500 |
| AC-8 | integracja | `GET /health` → 200 |
| AC-9 | integracja | `IWolverineRuntime.Options` zawiera wartości z AC-9 (`MessageStorageSchemaName`, `MessageIdentity`, `MultipleHandlerBehavior`, `TypeLoadMode`, `EntityDefaults.OnMissing`) |
| AC-10 | ręcznie (raport) | `mcp__aspire__list_resources` po starcie AppHost: zasoby zdrowe, Flyway `Exited(0)`; automatyzacja – Q-2 |
| AC-11 | plik | test parsuje `shop-realm.json` (System.Text.Json) i sprawdza realm, role, klientów, mappery, użytkowników |
| AC-12 | skrypt (ręcznie, raport) | `get-token.sh anna` → dekodowany payload ma `aud` i `roles`; ten sam token użyty w P-2 |
| AC-13 | plik + proces | test uruchamia każdy skrypt z `--help` (exit 0) i sprawdza obecność `set -euo pipefail` |
| AC-14 | skrypt (ręcznie, raport) | `migrate-local.sh` na działającym AppHost; `reset-local.sh` z odpowiedzią „nie” niczego nie usuwa |
| AC-15 | integracja | `SchemaConsistencyTests`: `AssertDatabaseMatchesConfigurationAsync()` dla store'ów Orders i Settlements oraz magazynu wiadomości; + ręcznie `db-assert` (część EF dołączona w etapie 5 – brak modelu) |
| AC-16 | architektura + przegląd | test numeracji (AC-17) + przegląd SQL pod kątem `DROP` pokazany w raporcie |
| AC-17 | architektura | dla każdego modułu z `db/flyway-modules.json`: wzorzec nazwy, ciągłość, brak duplikatów (`[Theory]`, dane z JSON) |
| AC-18 | architektura | ArchUnitNET: typy w `*.Contracts` implementujące konwencję zdarzenia kończą się na `IntegrationEvent` (`WithoutRequiringPositiveResults`, bo Contracts jest puste) |
| AC-19 | integracja | fixture zestawu: kontener Postgres + Flyway + Alba; test dymny `/health` |
| AC-20 | wszystkie | `dotnet test Shop.slnx` zielone |
| AC-21 | przegląd | lista kontrolna z `CLAUDE.md` „Zanim powiesz gotowe” |

Wszystkie testy automatyczne dostają `[Trait("AC", "AC-n")]` (reguła `testing.md`). AC weryfikowane ręcznie (AC-1, 4, 10, 12, 14, 21)
są raportowane z wynikiem poleceń, nie testami.

## 12. GUI

Nie dotyczy (etap 6). Klient `shop-gui` w Keycloak jest przygotowany: redirect `https://localhost:7180/*`, `http://localhost:5180/*`, sekret tylko dev.

## 13. Ryzyka i pytania otwarte

**Założenia przyjęte (odpowiedź domyślna – do potwierdzenia przy akceptacji):**
- Numer specyfikacji `001`, bo to pierwszy slice; `010` jest zarezerwowany dla funkcji demo (etap 7).
- Spec obejmuje cały etap 2, ale zakres prac do wykonania to C (dokończenie) → I; A i B mają AC do zweryfikowania, nie do ponownej realizacji.
- Polityka `SalesOrAccounting` nie występuje w opisie ról z `architecture.md` (§8) jako osobna – wynika z kontraktu (`order-payments`: „sales, accounting”). **Odstępstwo do zapisania w „Notatkach z implementacji”.**
- `NotFoundException` ma kod (`NotFoundException(code, message)`), choć prompt podaje sygnaturę bez kodu – bo tabela błędów w `api.md` wymaga kodu `<moduł>.not-found`. **Odstępstwo od promptu.**
- Hasło Postgresa dev `shop-dev-password` wpisane w `appsettings.Development.json`; parametr AppHost ma tę samą wartość domyślną. Tylko dev.
- Część EF testu zgodności schematu (`HasPendingModelChanges`) jest dopisana w etapie 5, gdy powstanie `DataHubDbContext` z encjami.

**Ryzyka:**
- [ ] PG 18 – zmieniona ścieżka danych w obrazie; montowanie wolumenu Aspire wymaga sprawdzenia (`/var/lib/postgresql` vs `/var/lib/postgresql/data`).
- [ ] `CommunityToolkit.Aspire.Hosting.Flyway` może nie pozwolić przekazać `-schemas`/`-locations`/`flywayArguments` – wtedy kontener `flyway/flyway` z `flywayImage` (zgodnie z promptem D).
- [ ] Docker = Rancher Desktop: kontenery nie widzą `/tmp` hosta – montować tylko ścieżki z repo; `migrate-local.sh` używa `--network container:<postgres z Aspire>` (port `55432` to proxy DCP po stronie hosta).
- [ ] `Aspire.Keycloak.Authentication` 13.6.4-preview.1 jest wersją preview (D-014) – ryzyko zmian API.
- [ ] Wolverine 6 `UseFluentValidation()` i `options.Include<T>()` nie były jeszcze kompilowane w Host (nie sprawdzone w pliku postępu).
- [ ] `JasperFx.ConcurrencyException` dla nieistniejącego strumienia z `Version` daje 409 zamiast 404 (D-012) – zaakceptowane świadomie w mapperze (AC-7), do przypomnienia w etapie 3.
- [ ] Pozostałości próbnej generacji poza repo: `~/.cache/shop-openapi-trial`, `~/.cache/shop-flyway-pkg` – do usunięcia.

**Pytania otwarte** (28.10.2026: Marcin zaakceptował specyfikację z odpowiedziami domyślnymi na Q-1, Q-2, Q-3):
- [x] **Q-1** Czy etap 1 (`docs/decisions.md`, `spikes/`, `.claude/`) jest już skommitowany? Wg `git log` tak (`ee200f3`); jeśli tak – można usunąć punkt „Uwagi” z `02-postep.md`. *Domyślnie: tak, usuwam wzmiankę.*
- [x] **Q-2** Czy AC-10 (AppHost zdrowy) ma mieć test automatyczny? Wymagałby pakietu `Aspire.Hosting.Testing` (spoza D-014, zasada 9) i uruchamiania całego AppHost w testach (wolne, Keycloak ~1 min). *Domyślnie: nie, weryfikacja ręczna przez Aspire MCP i raport.*
- [x] **Q-3** Czy `scripts/db/new-migration.sh` dla DataHub ma w etapie 2 kończyć się komunikatem „etap 5” (jak w prompcie)? *Domyślnie: tak, kod wyjścia 2 i komunikat po polsku.*

## 14. Notatki z implementacji

**Odstępstwa od specyfikacji i promptu**

* **Polityka `SalesOrAccounting`** – dodatkowa względem `architecture.md` §8, bo kontrakt wymaga „sales, accounting” dla `order-payments` (założenie z sekcji 13).
* **`NotFoundException(code, message)`** – z kodem, bo tabela błędów w `api.md` wymaga `<moduł>.not-found` (prompt podawał sygnaturę bez kodu).
* **Polecenie testów: `dotnet test --solution Shop.slnx` / `--project <ścieżka>`.** Przy runnerze Microsoft.Testing.Platform (`global.json`) samo
  `dotnet test Shop.slnx` kończy się błędem „Specifying a solution for 'dotnet test' should be via '--solution'”. Poprawiono `CLAUDE.md` i `README.md`.
* **Flyway jako zwykły kontener**, nie pakiet `CommunityToolkit.Aspire.Hosting.Flyway` (nie przyjmuje `-schemas`/`-locations` ani przypiętego obrazu) – D-016.
* **Keycloak po HTTP** (`WithoutHttpsCertificate`, API eksperymentalne, pragma `ASPIRECERTIFICATES001` tylko przy tym wywołaniu) – D-016.
* **`ASPIRE010` wyciszone w `Shop.AppHost.csproj`** (informacja SDK o trybie bez Aspire CLI) – D-016. **Do decyzji programisty**, czy wolimy `AspireUseCliBundle=true`.
* **`generate-contracts.sh` zmieniony w etapie 2:** czyszczenie katalogów w kontenerze, `*.csproj` zostają – D-016. Bez tego co drugie uruchomienie
  kończyło się `NoSuchFileException`, a C# Dev Kit usuwał referencje do wygenerowanego projektu z `*.Api.csproj`.
* **Migracje Orders i Settlements powstały** (`V001__marten_store_baseline.sql`): `db-patch` zwrócił funkcje pomocnicze `mt_*` i tabelę
  `mt_doc_envelope`. Wbrew oczekiwaniu nie ma `mt_doc_deadletterevent` ani tabel zdarzeń (`mt_events`, `mt_streams`) – pojawią się w migracjach etapów 3–4,
  gdy dojdą typy zdarzeń i agregaty. Żadna z trzech migracji nie zawiera `DROP`/`TRUNCATE`.
* **Magazyn wiadomości:** `V001__wolverine_message_storage.sql` w `Shop.Messaging.Migrations`; pliki rollbacku (`.drop`) w `Rollback/` obok `Sql/`.
* **AC-9, trwałe kolejki lokalne i `AutoApplyTransactions`** sprawdzane pośrednio: kolejka `local://default` ma `EndpointMode.Durable`, a lista polityk zawiera
  `AutoApplyTransactions` (po nazwie typu).
* **`SharedKernelTests` w `Shop.IntegrationTests`** (zgodnie z sekcją 11) – to testy bez bazy, ale mieszkają w tym projekcie, bo projekt jednostkowy modułu jeszcze nie istnieje.
* **Test `ApiControllersTests`** sprawdza atrybuty tylko na nadpisaniu (`inherit: false`): bazowa metoda z kontraktu ma gołe `[Authorize]`.
* **Q-1:** usunięto wzmiankę o niezacommitowanym etapie 1 z `prompts/02-postep.md`. **Q-2:** AC-10 weryfikowane ręcznie. **Q-3:** `new-migration.sh datahub` → kod 2 i komunikat „etap 5”.

**Do zrobienia później (poza zakresem etapu 2)**

* Etap 3: `JasperFx.ConcurrencyException` dla nieistniejącego strumienia z `Version` daje 409, nie 404 (D-012) – świadomie zaakceptowane w mapperze.
* Etap 3: błędy walidacji modelu MVC (atrybuty `[Required]` z wygenerowanych modeli) zwracają standardowy `ValidationProblemDetails` **bez pola `code`**
  (np. 400 dla `PlaceOrder` bez `sku`). Mapper z AC-7 obsługuje tylko `FluentValidation.ValidationException`. Do rozstrzygnięcia przy pierwszym slice'ie z komendą.
* Etap 5: dopisać do `SchemaConsistencyTests` część EF (`GetPendingMigrationsAsync`, `HasPendingModelChanges`), gdy powstanie `DataHubDbContext`.
* Wolverine zgłasza „found no handlers” przy starcie – oczekiwane do etapu 3.
* Poza repozytorium zostały katalogi próbnej generacji: `~/.cache/shop-openapi-trial`, `~/.cache/shop-flyway-pkg` – do usunięcia ręcznie.
