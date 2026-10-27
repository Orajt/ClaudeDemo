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
**Decyzja.** Do potwierdzenia w spike'u (etap 1): preferowana minimalna referencja analizatora w projekcie Domain,
bez używania typów Martena w kodzie. Testy architektury sprawdzają, że typy Domain nie zależą od Martena/Wolverine.
**Konsekwencje.** Do uzupełnienia po spike'u.

## D-010 Walidacja FluentValidation jako middleware Wolverine

**Decyzja.** Kształt danych waliduje FluentValidation (middleware Wolverine, błąd → 400). Reguły zależne od stanu – Domain (422).
**Konsekwencje.** W firmie walidacja jest dziś ręczna – po POC decyzja do potwierdzenia przez zespół.

## D-011 Keycloak lokalnie w Aspire

**Decyzja.** Kontener Keycloak z importem realmu `shop` (role `sales`, `accounting`, `gateway`). API: JWT Bearer, GUI: OIDC.
**Konsekwencje.** Blisko docelowego rozwiązania (Keycloak + AD). Testy integracyjne używają stubu Alba.
