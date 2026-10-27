---
name: api-contract
description: Przenosi zmianę kontraktu API z zaakceptowanej specyfikacji do api/openapi/shop-api.yaml, waliduje go i regeneruje kod kontraktu (symulacja pipeline'u). Używaj tylko na wyraźne polecenie.
argument-hint: "<ścieżka do specyfikacji>"
disable-model-invocation: true
---

# Zmiana kontraktu API (contract-first)

Specyfikacja: **$ARGUMENTS**

Kontrakt jest własnością ludzi. W firmie po zmianie YAML pipeline generuje kod i publikuje paczkę do Artifactory.
W POC tę rolę pełni `scripts/api/generate-contracts.sh`. Ten skill przygotowuje zmianę i pokazuje ją do akceptacji.

## Kroki

1. Przeczytaj sekcję „Kontrakt API” specyfikacji. Jeśli specyfikacja nie ma statusu `Zaakceptowana` – zatrzymaj się.
2. Przeczytaj `api/openapi/shop-api.yaml` i trzymaj się jego konwencji:
   - `operationId` w PascalCase, tag = moduł (`Orders`, `Settlements`, `DataHub`),
   - kwoty `type: number` **bez** `format` (generator mapuje na `decimal`), czasy `format: date-time`, daty `format: date`,
   - każda operacja ma odpowiedzi błędów przez `components/responses` (schemat `ApiProblem`, `application/problem+json`)
     i opis wymaganej roli,
   - nowe schematy w `components/schemas`, nazwy z sufiksem `Request` / `Response` / `View`.
3. Wprowadź **tylko** zmiany ze specyfikacji. Bez „przy okazji”.
4. Pokaż diff YAML i **poczekaj na akceptację** człowieka.
5. Po akceptacji uruchom `scripts/api/generate-contracts.sh` (walidacja + generowanie).
   Błąd walidacji → popraw YAML, pokaż ponownie.
6. Zbuduj rozwiązanie. Nowe metody abstrakcyjne w wygenerowanych kontrolerach dadzą błędy kompilacji w projektach `*.Api` –
   dodaj w kontrolerach modułów **zaślepki** rzucające `NotImplementedException("Slice NNN")`, żeby build przechodził.
   Implementacja należy do `/implement-slice`.
7. Podsumuj: zmienione operacje i schematy, wygenerowane typy, gdzie są zaślepki.
   Przypomnij, że w prawdziwym projekcie zmianę YAML wysyła się przez PR do repozytorium kontraktu.
