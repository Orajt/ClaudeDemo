# Shop POC – instrukcje projektu

Backend .NET 10: sklep (moduł **Orders**), rozliczenia z bramką płatności DemoPay (moduł **Settlements**)
i moduł odczytowy **DataHub**, który skleja dane obu modułów w płaskie widoki.
Modularny monolit, event sourcing (Marten), asynchroniczna komunikacja (Wolverine: trwałe lokalne kolejki
+ inbox/outbox w PostgreSQL), architektura heksagonalna, lokalnie Aspire. GUI (Blazor Server + MudBlazor)
jest tylko klientem API.

Architektura: `docs/architecture.md`. Decyzje i ich powody: `docs/decisions.md`.
Język domeny każdego modułu: `src/Modules/<Moduł>/CLAUDE.md`. Standardy warstw: `.claude/rules/` (ładują się same).

## Język

* Kod, identyfikatory, komunikaty commitów: **angielski**.
* Specyfikacje, dokumentacja, komentarze w kodzie, rozmowa z programistą: **polski**.

## Mapa repozytorium

```
api/openapi/shop-api.yaml          kontrakt API – własność ludzi (contract-first)
db/flyway-modules.json             lista modułów migracji (AppHost, testy, CI)
docs/specs/                        specyfikacje slice'ów (szablon: _TEMPLATE.md)
scripts/                           api/, db/, dev/, demo/ – skrypty pomocnicze
src/Host/                          Shop.Host (kompozycja), Shop.AppHost (Aspire), Shop.ServiceDefaults
src/Modules/<Moduł>/               Contracts, Domain, Application, Infrastructure, Migrations, Api
src/Shared/                        Shop.SharedKernel, Shop.Messaging.Migrations
src/Api/Shop.Api.Contracts.Generated   KOD GENEROWANY – nie edytuj
src/Gui/                           Shop.Gui, Shop.Api.Client.Generated (KOD GENEROWANY)
tests/                             testy jednostkowe modułów, Shop.IntegrationTests, Shop.ArchitectureTests
```

## Polecenia

| Cel | Polecenie |
|---|---|
| Build (ostrzeżenia = błędy) | `dotnet build Shop.slnx` |
| Wszystkie testy (wymaga Dockera) | `dotnet test Shop.slnx` |
| Testy jednego modułu | `dotnet test tests/Shop.Orders.Tests` |
| Uruchomienie lokalne | `dotnet run --project src/Host/Shop.AppHost` |
| Regeneracja kontraktu API (symulacja pipeline'u) | `scripts/api/generate-contracts.sh` |
| Nowa migracja | skill `db-migration` |
| Zgodność bazy z konfiguracją | `dotnet run --project src/Host/Shop.Host -- db-assert` |
| Token deweloperski | `scripts/dev/get-token.sh anna` (role: anna=sales, jan=accounting, demopay=gateway) |

## Zasady nienegocjowalne

1. **Pracujesz według specyfikacji** z `docs/specs/`. Niejasność albo luka w specyfikacji → pytasz, nie zgadujesz.
   Odstępstwo od specyfikacji zapisujesz w jej sekcji „Notatki z implementacji”.
2. **Kontrakt API i kod generowany należą do ludzi.** Nie edytujesz `src/**/*.Generated/**`.
   Zmianę kontraktu proponujesz w specyfikacji lub skillem `/api-contract`.
3. **Logika tylko w Domain i Application.** Api i Infrastructure są cienkie (patrz reguły warstw).
4. **Moduły komunikują się wyłącznie asynchronicznie przez zdarzenia z `*.Contracts`.** Nigdy przez referencję
   do Domain/Application/Infrastructure innego modułu, nigdy przez `InvokeAsync` do innego modułu.
5. **DataHub tylko czyta.** Komendy trafiają do modułów zapisu.
6. **Schemat bazy zmienia tylko Flyway.** Aplikacja działa z `AutoCreate.None`. Migracja, która jest na `main`,
   jest nienaruszalna – zmiana = nowa migracja. (Pilnuje tego hook.)
7. **Każde kryterium akceptacji (AC) ma co najmniej jeden test.** Nie kończysz z czerwonym buildem lub testem.
8. **Czytelność ponad spryt.** Programista czytający kod ma od razu rozumieć, co się dzieje (reguła `code-style`).
9. Nie dodajesz pakietów NuGet ani nie zmieniasz ich wersji bez uzasadnienia i zgody.
10. Nie wyłączasz testów, analizatorów ani ostrzeżeń, żeby „przejść dalej”.

## Przepływ pracy

```
opis → /spec-slice → akceptacja człowieka → (/api-contract → regeneracja) → /implement-slice → /review-slice → PR
```

* `/spec-slice <opis>` – szkic specyfikacji z szablonu, z pytaniami o niejasności. Bez kodu.
* `/api-contract <spec>` – propozycja zmiany `shop-api.yaml` na podstawie specyfikacji + regeneracja (POC).
* `/implement-slice <spec>` – plan → testy → implementacja warstwa po warstwie → weryfikacja.
* `/review-slice <spec>` – niezależny przegląd zmian przez agenta `slice-reviewer`.
* Skille `db-migration` i `datahub-view` uruchamiasz sam, gdy zadanie dotyczy migracji lub widoku DataHub.

## Biblioteki – na co uważać

Wersje są w `Directory.Packages.props`. Marten 9 / Wolverine 6 zmieniają się szybko – **przy wątpliwości
sprawdź dokumentację** (wolverinefx.net, martendb.io, weasel.jasperfx.net) zamiast polegać na pamięci.
Najważniejsze pułapki są w `.claude/rules/application.md` i `.claude/rules/domain.md`.

## Zanim powiesz „gotowe”

- [ ] `dotnet build Shop.slnx` bez ostrzeżeń
- [ ] `dotnet test Shop.slnx` zielone, w tym `Shop.ArchitectureTests` i test zgodności schematu
- [ ] każde AC ze specyfikacji ma test (pokaż mapowanie AC → test)
- [ ] migracje wygenerowane i przejrzane (brak niezamierzonych `DROP`)
- [ ] `CLAUDE.md` modułu zaktualizowany, jeśli zmienił się model
- [ ] status specyfikacji i „Notatki z implementacji” uzupełnione
- [ ] nie commitujesz i nie pushujesz bez prośby; proponujesz komunikat commita
