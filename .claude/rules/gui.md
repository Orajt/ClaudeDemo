---
paths:
  - "src/Gui/**"
---

# GUI (Blazor Server + MudBlazor)

GUI to **prosty klient API** do demonstracji. Zero logiki biznesowej, zero dostępu do bazy.

* Komunikacja wyłącznie przez wygenerowanego klienta `Shop.Api.Client.Generated` (nie edytuj go).
* Token użytkownika (OIDC, Keycloak) dołączany do żądań przez `DelegatingHandler` – wzorzec z dokumentacji
  Microsoft „Blazor Web App with OIDC” (wariant server).
* Ekran desktopowy (≥ 1280 px). Bez wersji mobilnej, bez dopracowanej grafiki – standardowe komponenty MudBlazor
  (`MudDataGrid`, `MudFileUpload`, `MudChip` dla statusów).
* Strona = komponent `.razor` + osobna klasa z logiką ładowania (`…Page.razor.cs`), metody krótkie i nazwane intencją.
* Błędy API (ProblemDetails) pokazywane jako `Snackbar` z polskim komunikatem z pola `detail`.
* Statusy po polsku w jednym miejscu (`StatusLabels`), kolory chipów spójne na wszystkich ekranach.
