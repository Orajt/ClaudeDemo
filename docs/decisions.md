# Decyzje architektoniczne (ADR w skrócie)

Format: kontekst → decyzja → konsekwencje. Nowa decyzja = nowy numer. Zmiana decyzji = nowy wpis, który zastępuje stary
(stary oznacz „Zastąpiona przez D-0xx”). Claude dopisuje tu decyzje podjęte w trakcie implementacji, a ludzie je zatwierdzają w PR.

---

## D-001 DataHub aktualizowany asynchronicznie

**Kontekst.** Widoki do odczytu skleją dane z kilku modułów. Aktualizacja w transakcji zapisu wiązałaby moduły
ze sobą i z DataHub.
**Decyzja.** Moduły publikują zdarzenia integracyjne przez outbox Wolverine; DataHub przetwarza je z trwałych
kolejek lokalnych.
**Konsekwencje.** Widok może przez chwilę być za zapisem (eventual consistency). GUI po komendzie odświeża dane.
Testy integracyjne czekają na przetworzenie przez tracked session.

## D-002 Trwałe kolejki lokalne Wolverine zamiast brokera

**Decyzja.** POC bez RabbitMQ: `UseDurableLocalQueues`, inbox/outbox w PostgreSQL (schemat `wolverine`),
`MultipleHandlerBehavior.Separated`, `MessageIdentity.IdAndDestination`.
**Konsekwencje.** Brak dodatkowej infrastruktury. Przejście na brokera później = zmiana routingu w Host, bez zmian w handlerach.

## D-003 Każdy moduł zapisu ma własny store Martena i schemat

**Decyzja.** `AddMartenStore<IOrdersStore>()` (schemat `orders`), `AddMartenStore<ISettlementsStore>()` (schemat `settlements`),
`IntegrateWithWolverine()`. Handlery modułu przypisane do store'a polityką
`UseAncillaryStorageFromAssemblyContaining<…>()`. Wspólny `MessageStorageSchemaName = "wolverine"`.
**Konsekwencje.** Zdarzenia modułów są fizycznie rozdzielone – moduł da się wydzielić. Handler nie może zapisywać do
dwóch store'ów naraz (i nie powinien).

## D-004 Flyway jest jedynym wykonawcą migracji

**Decyzja.** Aplikacja działa z `AutoCreate.None` (Marten, Wolverine), EF Core bez `Migrate()` i bez migracji
zarządzanych przez Wolverine. SQL generujemy: Weasel `db-patch` (Marten, Wolverine), `dotnet ef migrations script` (DataHub).
Osobna historia Flyway w schemacie każdego modułu. Lista modułów w `db/flyway-modules.json`.
**Konsekwencje.** Ten sam SQL w dev, testach i na produkcji. Zapomniana migracja jest wykrywana testem zgodności schematu.

## D-005 Outbox przez wartość zwracaną z handlera

**Kontekst.** Alternatywą jest przekazywanie zdarzeń Martena do Wolverine przez subskrypcje (async daemon).
**Decyzja.** Handler zwraca `(Events, OutgoingMessages)`; zdarzenia integracyjne powstają w tym samym handlerze
i zapisują się w tej samej transakcji.
**Konsekwencje.** Prosty, jawny przepływ, łatwy do przeczytania i przetestowania; brak zależności od async daemona.
Wersję strumienia dla zdarzenia stanu liczymy jako `wersja przed + liczba dopisanych zdarzeń`.

## D-006 Zdarzenia stanu dla DataHub + „nowsza wersja wygrywa”

**Kontekst.** Zdarzenia z różnych modułów i kolejek mogą przyjść w innej kolejności i więcej niż raz.
**Decyzja.** Dla widoków stanu moduły publikują `…StateChangedIntegrationEvent` z pełnym stanem potrzebnym do odczytu
i `Version`. DataHub nadpisuje kolumny źródła tylko, gdy wersja jest nowsza; wiersz ma token współbieżności `xmin`.
Fakty biznesowe służą reakcjom między modułami i widokom dziennika.
**Konsekwencje.** Kolejność i duplikaty nie psują widoków. Koszt: dwa rodzaje zdarzeń integracyjnych.

## D-007 Contract-first z openapi-generator

**Decyzja.** `api/openapi/shop-api.yaml` jest źródłem prawdy; generator `aspnetcore` (`buildTarget=library`,
abstrakcyjne kontrolery). W firmie generuje pipeline i publikuje paczkę do Artifactory; w POC robi to skrypt.
**Konsekwencje.** Claude nie zmienia kontraktu samodzielnie – proponuje zmianę, człowiek zatwierdza.

## D-008 DataHub bez Domain i Contracts; DbContext w Application

**Kontekst.** Transakcyjny middleware Wolverine dla EF Core rozpoznaje DbContext po konkretnym typie parametru handlera.
**Decyzja.** DataHub ma Application (widoki, `DataHubDbContext`, konfiguracje, handlery, zapytania), Infrastructure
(rejestracja), Migrations, Api.
**Konsekwencje.** Application DataHub zależy od EF Core – świadomie, bo to moduł odczytowy.

## D-009 Source generator Martena 9 a czystość Domain

**Kontekst.** Marten 9 wymaga, by generator `JasperFx.Events.SourceGenerator` działał w assembly, które deklaruje agregat
(publiczne `Apply`/`Create`).
**Decyzja (potwierdzona w spike'u, etap 1 – D-012).** Projekt Domain ma dokładnie dwie referencje:

```xml
<PackageReference Include="JasperFx.Events" />
<PackageReference Include="JasperFx.Events.SourceGenerator" PrivateAssets="all" />
```

Bez Martena, Wolverine, Npgsql i EF. Kod Domain nie używa żadnych typów JasperFx – używa ich tylko wygenerowany
dispatcher (`[assembly: GeneratedEvolver]`, `IGeneratedSyncEvolver<,>`, `IEvent`).

Co sprawdzono:
* sam analizator → błąd kompilacji `CS0400: Nie można odnaleźć ... „JasperFx”` w wygenerowanym pliku `*Evolver.g.cs`;
* analizator + `JasperFx.Events` → działa: `Create`/`Apply` (publiczne, record niemutowalny) są wołane;
* brak obu referencji (generator tylko w Application, które ma Martena) → w czasie działania
  `No source-generated dispatcher found for ...SingleStreamProjection<Order, Guid> ... there is no runtime fallback`.

**Konsekwencje.** Testy architektury: Domain **może** zależeć od `JasperFx.Events` (i przechodnio `JasperFx`),
**nie może** od `Marten*`, `Wolverine*`, `Npgsql*`, `Microsoft.EntityFrameworkCore*`. `JasperFx.Events` ciągnie przechodnio
m.in. `Microsoft.Extensions.Hosting` i `Spectre.Console` – akceptowalne, bo kod Domain ich nie używa. Generator musi
działać w każdym assembly, które deklaruje agregat; nie ma fallbacku w czasie działania.

## D-010 Walidacja FluentValidation jako middleware Wolverine

**Decyzja.** Kształt danych waliduje FluentValidation (middleware Wolverine, błąd → 400). Reguły zależne od stanu – Domain (422).
**Konsekwencje.** W firmie walidacja jest dziś ręczna – po POC decyzja do potwierdzenia przez zespół.

## D-011 Keycloak lokalnie w Aspire

**Decyzja.** Kontener Keycloak z importem realmu `shop` (role `sales`, `accounting`, `gateway`). API: JWT Bearer, GUI: OIDC.
**Konsekwencje.** Blisko docelowego rozwiązania (Keycloak + AD). Testy integracyjne używają stubu Alba.

## D-012 Potwierdzona konfiguracja Critter Stack

**Kontekst.** Marten 9 / Wolverine 6 zmieniają się szybko, a przykłady z internetu są nieaktualne. Spike
`spikes/CritterStackSpike/` (13 testów xUnit na Testcontainers: Postgres 18.6 + Flyway 13.11.1 w kontenerze) sprawdził
hipotezy H1–H9 z etapu 1 na zainstalowanych wersjach. Opis uruchomienia: `spikes/CritterStackSpike/README.md`.

**Decyzja.** Etap 2 buduje rozwiązanie na poniższej konfiguracji. Jedna sprawa jest otwarta – sposób generowania
kodu Wolverine (punkt „Do decyzji”).

### Wersje pakietów (październik 2026)

| Pakiet | Wersja | Uwagi |
|---|---|---|
| Marten | 9.54.2 | `WolverineFx.Marten` 6.56.0 ciągnie 9.54.1 – potrzebne przypięcie (niżej) |
| WolverineFx, .Marten, .EntityFrameworkCore, .Postgresql | 6.56.0 | |
| Weasel.Postgresql / Weasel.EntityFrameworkCore | 9.47.0 | przechodnio |
| JasperFx, JasperFx.Events, JasperFx.Events.SourceGenerator | 2.96.0 | |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | wymusza Npgsql 10.0.3 w całym hoście (Marten deklaruje ≥ 9.0.4) – działa |
| Microsoft.EntityFrameworkCore (+ .Relational, .Design) | 10.0.14 | przypięte; narzędzie `dotnet-ef` 10.0.14 w lokalnym manifeście |
| xunit.v3 / xunit.runner.visualstudio / Microsoft.NET.Test.Sdk | 4.0.2 / 4.0.1 / 18.11.0 | |
| Shouldly | 4.3.0 | |
| Testcontainers.PostgreSql | 4.16.0 | |
| Obrazy Dockera | `postgres:18.6-alpine`, `flyway/flyway:13.11.1-alpine` | |

`Directory.Packages.props`: `ManagePackageVersionsCentrally` **i** `CentralPackageTransitivePinningEnabled` = `true`,
żeby wersje z pliku obowiązywały też dla zależności przechodnich (Marten, EF Core).

### Host – Wolverine

```csharp
builder.UseWolverine(options =>
{
    options.ApplicationAssembly = typeof(Program).Assembly;      // jawnie – patrz pułapki
    options.CodeGeneration.TypeLoadMode = TypeLoadMode.Static;   // patrz „Do decyzji”

    options.Discovery.IncludeAssembly(typeof(SomeOrdersHandler).Assembly);   // każdy moduł
    // ...

    // Główny magazyn wiadomości – główny AddMarten() NIE jest potrzebny.
    options.PersistMessagesWithPostgresql(connectionString, "wolverine");
    // Wymagane: bez tego każdy store Martena dostaje własne tabele wolverine_* w schemacie modułu.
    options.Durability.MessageStorageSchemaName = "wolverine";
    options.AutoBuildMessageStorageOnStartup = AutoCreate.None;

    options.UseEntityFrameworkCoreTransactions();
    options.Policies.AutoApplyTransactions();
    options.Policies.UseDurableLocalQueues();
    options.MultipleHandlerBehavior = MultipleHandlerBehavior.Separated;
    options.Durability.MessageIdentity = MessageIdentity.IdAndDestination;
    options.EntityDefaults.OnMissing = OnMissing.ThrowException;

    options.UseOrdersModule();        // polityki modułów (niżej)
    options.UseSettlementsModule();
    options.UseDataHubModule();
});
```

### Store modułu zapisu (Infrastructure)

```csharp
public interface IOrdersStore : IDocumentStore;

services.AddMartenStore<IOrdersStore>(options =>
    {
        options.Connection(connectionString);
        options.DatabaseSchemaName = "orders";
        options.AutoCreateSchemaObjects = AutoCreate.None;
        options.Projections.Snapshot<Order>(SnapshotLifecycle.Inline);
    })
    .IntegrateWithWolverine();

// Wszystkie handlery z assembly modułu używają store'a modułu (sesja, outbox, aggregate workflow).
// Parametr generyczny: dowolny typ z assembly modułu; argument: typ store'a.
options.Policies.UseAncillaryStorageFromAssemblyContaining<OrdersApplicationMarker>(typeof(IOrdersStore));
```

### Handler z agregatem (Application)

```csharp
// [WriteModel] na parametrze – aktualny, niezależny od magazynu atrybut Wolverine 6.
// Wersja oczekiwana pochodzi z właściwości "Version" komendy (FetchForWriting z expected version).
public static (Events, OutgoingMessages) Handle(ConfirmPayment command, [WriteModel] Order order)
{
    var confirmed = order.ConfirmPayment(command.TransactionId, DateTimeOffset.UtcNow);
    var events = new Events { confirmed };
    var versionAfterAppend = order.Version + events.Count;   // D-005
    return (events, new OutgoingMessages { /* zdarzenia integracyjne */ });
}
```

Agregat: niemutowalny `record` z `public long Version { get; init; }`, publiczne `static Create(...)` i `Apply(...)`
zwracające nową instancję. Marten ustawia `Version` przy `FetchForWriting`.

Handler, który pisze do **wielu** strumieni (liczba znana w czasie działania, np. import raportu): wstrzyknięty
`IDocumentSession` (polityka assembly wybiera store modułu), `session.Events.FetchForWriting<T>(id)` w pętli,
`stream.AppendOne(...)`, `session.Events.StartStream<T>(...)`, zwrot `OutgoingMessages`. `AutoApplyTransactions`
robi jeden `SaveChangesAsync`. Wielostrumieniowe `[WriteModel]` nadaje się tylko do liczby strumieni znanej
przy kompilacji.

### DataHub

```csharp
services.AddDbContextWithWolverineIntegration<DataHubDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "datahub")));

// Konflikt xmin albo równoległe utworzenie tego samego wiersza przez dwa handlery → ponów (handler wczyta wiersz od nowa).
TimeSpan[] cooldowns = [TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(250)];
options.OnException<DbUpdateConcurrencyException>().RetryWithCooldown(cooldowns);
options.OnException<DbUpdateException>(e => e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
    .RetryWithCooldown(cooldowns);
```

Handler przyjmuje konkretny `DataHubDbContext` i nie woła `SaveChangesAsync` – robi to middleware (wygenerowany kod:
nowy scope i `DbContext` przy każdej próbie, transakcja EF, `SaveChangesAsync`, commit). `AddDbContextWithWolverineIntegration`
**nie** dodaje tabel kopert do modelu EF – migracja zawiera tylko tabele DataHub. `xmin`: `Property(x => x.RowVersion).IsRowVersion()`
na `uint` (migracja nie tworzy kolumny).

### Identyfikatory `db-list` i kolejność generowania SQL

| DatabaseUri | SubjectUri | Co to jest |
|---|---|---|
| `postgresql://<host>/<db>/<schemat_modułu>` | `marten://store/` | store Martena modułu (po jednym na moduł) |
| `postgresql://<host>/<db>` | `wolverine://messages/main` | magazyn wiadomości (na liście dwa razy, ten sam) |

W spike'u: `postgresql://localhost/spike/spike_orders`, `postgresql://localhost/spike/spike_settlements`,
`postgresql://localhost/spike` + `wolverine://messages/main`. DataHub (EF) nie występuje w `db-list`.

Kolejność:
1. Pusta baza deweloperska.
2. `db-patch <plik> -d <id>` dla każdego store'a Martena (pełny DatabaseUri) i magazynu wiadomości (`-d wolverine://messages/main`).
   Plik `*.drop.sql` pomijamy.
3. `dotnet ef migrations add` + `dotnet ef migrations script <od> <do> --no-transactions` (startup project = Host).
4. Pliki jako `<moduł>/Sql/V<n>__<opis>.sql`; Flyway osobno dla każdego modułu z `-schemas=<schemat>` (kolejność
   z `db/flyway-modules.json`: messaging → orders → settlements → datahub).
5. `db-assert` → „No database differences detected” dla wszystkich; test integracyjny robi to samo
   (`Storage.Database.AssertDatabaseMatchesConfigurationAsync()` dla store'ów, rzutowanie `IWolverineRuntime.Storage`
   na `Weasel.Core.Migrations.IDatabase` dla wiadomości, `GetPendingMigrationsAsync()` + `HasPendingModelChanges()` dla EF).

### Pułapki znalezione w spike'u

| Obszar | Pułapka | Co robić |
|---|---|---|
| Codegen | Rdzeń Wolverine 6 nie ma kompilatora Roslyn – host nie startuje (`TypeLoadMode.Dynamic ... no IAssemblyGenerator`) | patrz „Do decyzji” |
| Codegen | `codegen write` zapisuje do `<content root>/Internal/Generated`; `--contentroot` z względną ścieżką nie działa | uruchamiać z katalogu projektu Host |
| Codegen | Bez `ApplicationAssembly` Wolverine w testach przyjmuje assembly testów (ostrzeżenie GH-3521) | ustawić jawnie |
| Codegen | W trybie Static ustawienia wpływające na kod (np. `OnMissing`) są „zamrożone” w chwili `codegen write` | regenerować po każdej zmianie handlerów/konfiguracji; w CI `codegen write` + `git diff --exit-code` |
| Brak agregatu | `[DeciderFunction]` / `[AggregateHandler]` na metodzie **nie** generuje sprawdzenia `null` – handler dostaje `null` | używać `[WriteModel]` na parametrze |
| Brak agregatu | `[WriteModel]` + `OnMissing.ThrowException` → `Wolverine.Persistence.RequiredDataMissingException`; bez ustawienia (domyślnie `Simple404`) → `EntityIsNotNullGuard` loguje i kończy handler po cichu | ustawienie globalne w Host (już jest) |
| Brak agregatu | Komenda z `Version` dla nieistniejącego strumienia → `JasperFx.ConcurrencyException` („expected 1, but was 0”) **przed** sprawdzeniem `OnMissing` | mapowanie błędów w Api: uwzględnić (409 zamiast 404) albo świadomie zaakceptować |
| `db-patch` | `-d` dopasowuje po początku URI: `spike_orders` nic nie znajduje, a `postgresql://localhost/<db>` trafia w pierwszy store Martena | pełny DatabaseUri dla store'ów, SubjectUri dla wiadomości |
| Marten | każdy store dostaje też tabele dokumentów `mt_doc_envelope` i `mt_doc_deadletterevent` w swoim schemacie | normalne, są w SQL z `db-patch` |
| EF | `migrations script` domyślnie dokłada `START TRANSACTION`/`COMMIT`, co gryzie się z transakcją Flyway | `--no-transactions` |
| DataHub | konflikt `xmin` i równoległy `INSERT` tego samego wiersza bez polityki ponawiania → wiadomość od razu w error queue (test bez polityki: 80× `23505 duplicate key`) | polityka ponawiania (wyżej) |
| Wyjątki | `Marten.Exceptions.ExistingStreamIdCollisionException` (nie `JasperFx.Events.…`), `JasperFx.Events.EventStreamUnexpectedMaxEventIdException` (dziedziczy po `JasperFx.ConcurrencyException`) | w testach i mapowaniu błędów używać tych typów |
| Tracked session | wiadomość bez odbiorcy jest w `session.NoRoutes`, nie w `session.Sent` (i jest porzucana) | asercje odpowiednio |
| xUnit v3 4.x | tylko Microsoft Testing Platform: `dotnet test` wymaga `global.json` z `"test": { "runner": "Microsoft.Testing.Platform" }` i składni `dotnet test --project ...`; brak niejawnego `using Xunit`; `CollectionBehavior.DisableTestParallelization` przestarzałe (błąd przy ostrzeżeniach jako błędach); analizator `xUnit1051` wymaga `TestContext.Current.CancellationToken` | `global.json`, `<Using Include="Xunit" />`, `xunit.runner.json` z `parallelizeTestCollections: false` |
| Testy | `DurabilityMode.Solo` w hoście testowym (bez wyboru lidera) | tylko w testach |

### Rozstrzygnięte przed etapem 2: generowanie kodu Wolverine (wybrano A – D-013)

| Opcja | Plusy | Minusy |
|---|---|---|
| A. `WolverineFx.RuntimeCompilation` 6.56.0 (nowy pakiet), `TypeLoadMode.Dynamic`/`Auto` w dev i testach | brak kroku `codegen write` przy każdej zmianie handlera; wszystkie warianty konfiguracji testowalne na żywym hoście | nowy pakiet (Roslyn) w hoście; wolniejszy start; produkcyjnie i tak warto Static |
| B. Tylko `TypeLoadMode.Static` + `codegen write` (wariant spike'u) | brak nowych pakietów; szybki start; to samo w dev i na produkcji | po każdej zmianie handlera trzeba regenerować kod (nieaktualny kod = błąd w czasie działania); potrzebny check w CI |

**Konsekwencje.** Architektura z `docs/architecture.md` jest potwierdzona bez zmian kierunku zależności. Do
uwzględnienia w etapie 2: jawna polityka ponawiania w DataHub (§6 zakładał, że Wolverine ponawia sam), `[WriteModel]`
jako standard handlera agregatu, `global.json` dla testów, decyzja A/B o generowaniu kodu.

## D-013 Kod Wolverine generowany w czasie działania (opcja A z D-012)

**Kontekst.** Rdzeń Wolverine 6 nie ma kompilatora. Opcja B (Static + `codegen write`) wymaga regeneracji kodu po każdej
zmianie handlera – łatwo o to zapomnieć w każdym kolejnym slice'ie, a nieaktualny kod wywraca się dopiero w czasie działania.
**Decyzja (zatwierdzona przez programistę, etap 2).** Host ma pakiet `WolverineFx.RuntimeCompilation` i działa
z `TypeLoadMode.Dynamic` – w dev i w testach. Katalogu `Internal/Generated` nie ma w repozytorium, kroku `codegen write` nie ma
w procesie pracy.
**Konsekwencje.** Start hosta i testów dłuższy o kilka sekund. Przed ewentualnym wdrożeniem produkcyjnym do rozważenia
`TypeLoadMode.Static` + `codegen write` w pipeline (nowa decyzja). Tej kwestii nie podnosimy ponownie w etapach POC.

## D-014 Pakiety zatwierdzone w etapie 2

**Decyzja (zatwierdzona przez programistę).** Oprócz wersji z D-012 w `Directory.Packages.props` są: Aspire 13.6.4
(`Aspire.AppHost.Sdk`, `Aspire.Hosting.PostgreSQL`), `Aspire.Hosting.Keycloak` i `Aspire.Keycloak.Authentication`
13.6.4-preview.1.26523.11 (brak wersji stabilnej), `CommunityToolkit.Aspire.Hosting.Flyway` i
`CommunityToolkit.Aspire.Hosting.PostgreSQL.Extensions` 13.6.1, `WolverineFx.FluentValidation` / `WolverineFx.RuntimeCompilation` 6.56.0,
`FluentValidation` 12.2.0, pakiety ServiceDefaults (OpenTelemetry 1.20.1, `Microsoft.Extensions.Http.Resilience` /
`ServiceDiscovery` 10.11.0), zależności wygenerowanego klienta GUI (`Microsoft.Extensions.Http`, `.Hosting`, `.Http.Polly`,
`Microsoft.Net.Http.Headers` 10.0.14), `Alba` 8.5.3, `TngTech.ArchUnitNET.xUnitV3` 0.13.5.
**Konsekwencje.** Użycie tych pakietów w ramach ich roli nie wymaga ponownej zgody. Nowy pakiet albo zmiana wersji – nadal
zgodnie z zasadą 9 z `CLAUDE.md`.

## D-015 Obrazy Dockera w `db/flyway-modules.json`

**Kontekst.** Postgres i Flyway muszą mieć te same obrazy w AppHost, testach integracyjnych i skryptach `scripts/db/`.
**Decyzja (zatwierdzona przez programistę).** Pola `postgresImage` i `flywayImage` w `db/flyway-modules.json` – czytają je
AppHost, `Shop.IntegrationTests` i skrypty. Obraz Keycloak – domyślny dla wersji pakietu Aspire.
**Konsekwencje.** Zmiana wersji obrazu = jedna linia w jednym pliku.
