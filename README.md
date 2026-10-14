# Miettis Park

.NET-, PostgreSQL- ja Vue/Nuxt-pohjainen pysäköintihallisimulaatio rekrytoinnin ennakkotehtävään.

- [SPEC.md](SPEC.md): toiminnallinen ja tekninen sopimus.
- [STARTING-POINT.md](STARTING-POINT.md): pohjan sisältö ja valmiustarkistus.
- [BACKLOG.md](BACKLOG.md): toiminnalliset työpaketit.
- [AI-USAGE.md](AI-USAGE.md): AI:n käyttö.
- [VALIDATION.md](VALIDATION.md): suoritetut tarkistukset ja avoimet validoinnit.
- [Käyttöliittymäluonnokset](ui-mocks/index.html): erillinen prototyyppi esimerkkidatalla.

## Toteutettu PARK-01 ja pohja

`src/MiettisPark.Api` sisältää .NET 10 -hostin, EF Core/Npgsql-mallin, alkumigraation sekä siemendatan: yksi halli, viisi paikkaa ja kaksi hintaporrasta. Hinnasto tallennetaan jsonb-taulukkona, kestot ovat C#:ssa TimeSpan-arvoja ja aikaleimat UTC DateTimeOffset -arvoja. Paikan valinnainen VisitId mahdollistaa tunnistamattomat paikkahavainnot.

`src/client` on staattinen Nuxt/Vue SPA. Reitti `/` sisältää porttisimulaation, paikkahavainnot ja palvelimen kuitin. PARK-01 lisää kahdeksan pysäköinnin REST-reittiä, tuotannon palvelut sekä paikallisen xUnit- ja DOM-testiharnessin. `/api/health` on edelleen pelkkä liveness, ei tietokannan valmiustarkistus.

SDK-versio on global.json-tiedostossa, Node-versio .node-version-tiedostossa. NuGet- ja npm-riippuvuudet on lukittu. Sovelluksen ajo tarvitsee .NET SDK:n, Node 24:n ja PostgreSQL 17:n tai Dockerin. Paikalliset buildit ja testit tarvitsevat vain .NETin, Noden ja tavallisen riippuvuuksien restoren; ne eivät tarvitse tietokantapalvelua, konttia, selainajuria tai salaisia asetuksia.

## Kehitys

Repojuuressa PowerShellillä:

```powershell
Copy-Item .env.example .env
# Vaihda .env-tiedoston POSTGRES_PASSWORD paikalliseksi salasanaksi.
docker compose up -d database
dotnet tool restore
$env:ConnectionStrings__Parking = 'Host=localhost;Database=miettis_park;Username=miettis_park;Password=OMA_PAIKALLINEN_SALASANA'
dotnet run --project src/MiettisPark.Api
```

Backend kuuntelee kehityksessä osoitteessa http://localhost:5080. Sovella .env-tiedostossa muuttamasi tietokannan nimi, käyttäjä ja portti myös yhteysmerkkijonoon. .NET ei lue .env-tiedostoa; Compose lukee. Vaihtoehtoisesti käytä omaa PostgreSQL-instanssia. Älä tallenna oikeaa salasanaa versionhallintaan. Compose-esimerkin salasanaan ei tule käyttää yhteysmerkkijonon erottimia, kuten puolipistettä.

Toisessa terminaalissa:

```powershell
cd src/client
npm ci
npm run dev
```

Avaa http://localhost:3000. Frontend käyttää suhteellista `/api`-polkua, jonka Nuxt proxy välittää kehityksessä backendille. `API_PROXY_TARGET` voi vaihtaa backend-originin; älä lisää siihen `/api`-päätettä. Tuotannossa proxyä tai Node-palvelinta ei ole.

`Database__InitializeOnStartup=true` (oletus) soveltaa migraatiot ennen HTTP-palvelimen käynnistymistä. Siemendata on migraatiossa; uudelleenkäynnistys ei nollaa dataa. Yhden instanssin simulaatiossa tämä on tarkoituksellinen yksinkertaistus. Tuotannon migraatioajoon voidaan käyttää erillistä käyttöoikeutta ja ajaa migraatiot ennen deploymentia.

## OpenAPI ilman käynnissä olevaa sovellusta

```powershell
node scripts/openapi.mjs
```

Tulos: `src/MiettisPark.Api/OpenApi/miettis-park.json`. Sama generointi tapahtuu tavallisen backend-buildin yhteydessä. Käytössä ovat **Microsoft.AspNetCore.OpenApi** ja **Microsoft.Extensions.ApiDescription.Server**, ei Swashbuckle/NSwag.

Microsoftin build-työkalu kutsuu entry pointia endpoint-metadatan löytämiseksi, mutta ei käynnistä kuuntelevaa HTTP-palvelinta. Program tunnistaa tämän ajon ja ohittaa migraatiot sekä startupin tietokantayhteyden. Generointi onnistuu ilman tietokantaa, yhteysmerkkijonoa tai vapaata palvelinporttia. Pidä mahdolliset tulevat ulkoiset startup-integraatiot saman suojauksen takana. Ajonaikainen kuvaus on `/openapi/v1.json`.

## Build ja EF

```powershell
dotnet restore MiettisPark.slnx --locked-mode
dotnet build MiettisPark.slnx -c Release
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project src/MiettisPark.Api --configuration Release
dotnet ef migrations script --idempotent --project src/MiettisPark.Api --configuration Release --output artifacts/migrations.sql
cd src/client
npm ci
npm run lint
npm run typecheck
npm run build
```

Migraation luonti ja SQL-generointi käyttävät design-time DbContextFactorya, eivät avaa tietokantayhteyttä. Uusi migraatio: `dotnet ef migrations add Nimi --project src/MiettisPark.Api`. Tietokantaan soveltaminen: `dotnet ef database update --project src/MiettisPark.Api`, kun `ConnectionStrings__Parking` on asetettu. Älä käytä `EnsureCreated`-kutsua migraatioiden rinnalla.

## Kontti ja julkaisu

```powershell
docker compose up --build -d
```

Sovellus: http://localhost:8080. Dockerfile rakentaa frontendin ja backendin yhteen ei-root-runtime-imageen; PostgreSQL on erillinen Compose-palvelu ja data nimetyssä volumessa. `docker compose down` säilyttää datan. `APP_PORT` ja `POSTGRES_PORT` ovat muutettavissa .env-tiedostossa.

GitHub Actions julkaisee imagen `ghcr.io/<owner>/<repository>` semanttisesta tagista, esimerkiksi `v1.2.3` tai `v1.2.3-rc.1`. Virheellinen versiotagi pysäyttää työn ennen julkaisua. Docker-tagit ovat versio ilman v-etuliitettä, `sha-<12 merkkiä>` ja vain vakaalle versiolle `latest`. SemVerin mahdollinen `+build` tallennetaan Docker-tagissa `_build`-muodossa; build-metadatatagi ei muuta latestia. Julkaisu käyttää GitHubin GITHUB_TOKENia ja packages:write-oikeutta. Imagea ei ole vielä julkaistu eikä workflowta ajettu GitHubissa.

## PARK-01: sopimukset ja luovutus

Kaikki reitit käyttävät suhteellista `/api/garages`-alkua ja pohjan `int`-tunnisteita:

| Metodi ja loppuosa | Pyyntö / vastaus |
| --- | --- |
| GET `/api/garages` | Hallit: `id`, `name`. |
| GET `/{garageId}/availability` | `capacity`, `activeVisits`, `freeLots`, `occupiedLots`, `unknownLots`, `isFull`, `lots(id, number, currentStatus)`. |
| GET `/{garageId}/visits/active` | `visitId`, `licenseNumber`, `startAt`, nullable `lotId`/`lotNumber`. |
| GET `/{garageId}/parkings/active` | `parkingId`, `lotId`, `lotNumber`, nullable `visitId`/`licenseNumber`, `startAt`. |
| POST `/{garageId}/entries` | `{licenseNumber}` → `visitId`, normalisoitu rekisteri, `startAt`, `outcome`, `gateOpened`. |
| POST `/{garageId}/lots/{lotId}/entries` | `{visitId?}`, myös `{}` tai `{visitId:null}` → `parkingId`, `lotId`, nullable `visitId`, `startAt`. |
| POST `/{garageId}/lots/{lotId}/exit` | Ei vaadi rekisteriä/käyntiä → `lotId`, nullable `parkingId`, `missingParkingObservation`. |
| POST `/{garageId}/exits` | `{licenseNumber}` → tallennettu `visitId`, `licenseNumber`, `startAt`, `endAt`, `closedAt`, `duration`, numeerinen `cost`, `currency=EUR`, `gateOpened=true`. |

Enumit ovat JSON-merkkijonoja. Kestot ovat .NETin TimeSpan-merkkijonoja, ajat UTC-aikaleimoja. `LicenseNumberNormalizer` tekee palvelimella Trim + invariantti uppercase; selain ei muuta tunnuksen sisältöä. Virheet ovat ProblemDetails: `code`, `gateOpened=false` ja tarvittaessa `requiresCustomerService=true`. Virheellinen syöte on 400, puuttuva/toisen hallin tunniste 404 ja tilakonflikti 409. JSON/mallisidonnan virhe on `INVALID_REQUEST`; tuntematon sisäinen vika palauttaa geneerisen `INTERNAL_ERROR`-vastauksen, ei sisäistä poikkeamaa.

`Contracts/GarageContracts.cs` määrittää DTO:t. Ohut `GaragesController` välittää cancellation-tokenin scoped `ParkingService`-palvelulle. Sen `GetGaragesAsync`, `GetAvailabilityAsync`, `GetActiveVisitsAsync`, `GetActiveParkingsAsync`, `EnterGarageAsync`, `EnterLotAsync`, `LeaveLotAsync` ja `LeaveGarageAsync` käyttävät samaa EF/PostgreSQL-tietolähdettä. Erillistä tuotannon muistivarastoa ei ole.

`Services/ParkingFeeCalculator.cs` validoi hinnaston ja summaa decimal-eurot. Alkavat yksiköt lasketaan tick-jaolla ja jakojäännöksellä, ei kokonaisminuuteilla tai liukuluvuilla. `TimeProvider` määrää laskutusajat. Ulosajo tallentaa hinnan/ajat kerran; toisto palauttaa `ACTIVE_VISIT_NOT_FOUND` eikä hae satunnaista vanhaa kuittia.

`Services/ParkingWriteTransaction.cs` aloittaa PostgreSQL ReadCommitted -transaktion. Kaikki kirjoitukset lukitsevat parametrisoidusti hallirivin `FOR UPDATE` **ennen** tilatarkistuksia ja tarvittaessa paikan. Tässä paketissa kukin komento muuttaa korkeintaan yhtä paikkaa; tulevien monipaikkakomentojen on lukittava paikat id-järjestyksessä. Hallilukko serialisoi saman hallin komennot myös eri sovellusinstansseissa. Tallennus ja commit kuuluvat samaan transaktioon, epäonnistumisen dispose rollbackaa tuotannossa. Tunnetut osittaisten unique-indeksien konfliktit muunnetaan turvallisiksi 409-vastauksiksi transaktion purkamisen jälkeen. Muut DB-virheet eivät muutu näennäisiksi liiketoimintakonflikteiksi. Pohjan indeksit, FK:t ja lifecycle-rajoitteet säilyvät `Data/Configurations`-alueella viimeisenä suojana.

Visit ja Parking ovat itsenäiset. Täysi halli ei estä sisäänajohavaintoa; saatavuus tulee paikkojen tiloista. Paikan käyttö voi olla tunnistamaton. Annettu VisitId validoidaan saman hallin aktiiviseksi käynniksi ilman toista aktiivista Parkingia. Vapautushavainto sulkee vain paikan Parkingin ja asettaa paikan Free-tilaan. Ulosajo ei muuta Parkingia tai paikkaa. Paikanvaihto on vapautus ja uusi valinta; alkuperäinen Visit.StartAt säilyy. UI:n aktiivinen tieto palautuu API:sta reloadissa, ei localStoragesta.

### Palveluriippumattomat tarkistukset repojuuresta

```powershell
dotnet build MiettisPark.slnx -c Release
dotnet test MiettisPark.slnx -c Release
npm run check
node scripts/openapi.mjs
```

`npm run check` ajaa clientissä puhtaan `npm ci` → lint → typecheck → API-stubatut DOM-testit → staattinen build. Runner ratkaisee cwd:n tiedostostaan ja käynnistää aktiivisen npm CLI:n Nodella Windows/Linux-yhteensopivasti; jokainen virhe pysäyttää ajon nonzero-koodiin. Nykyiset Nuxt/Vue-versiot säilyvät lukittuina. xUnit kutsuu tuotannon palveluja testikellolla ja testiin rajatulla EF InMemory -providerilla sekä lukitus/transaktiorajapinnan testituplalla. HTTP/JSON/mallisidonta testataan prosessin sisäisellä TestServerillä, ei kuuntelevalla palvelimella. Npgsql-malli ja idempotentti migraatio-SQL tarkistetaan offline samassa testiajossa.

**Näytön raja:** InMemory/testitupla ei todista PostgreSQL-transaktioita, rollbackia, lukituksia, kilpailuja, SQL-rajoitteiden ajonaikaista toimintaa tai restart-pysyvyyttä. Oikean PostgreSQL:n, Dockerin ja selaimen/visuaalisen hyväksynnän tarkistukset tehdään myöhemmin ihmisen toimesta; ne eivät ole tämän agenttityön submission-portteja. Tarkat suoritetut/epäonnistuneet/ajamattomat tarkistukset: [VALIDATION.md](VALIDATION.md).

**PARK-02:** aktiivisen rekisterin uusintasisäänajo palauttaa tässä paketissa saman Visitin `Duplicate`-tuloksena iästä riippumatta. Konfiguroitava kahden minuutin raja, `RecoveredMissingExit`, vanhan käynnin `Interrupted`/`MissingExitBeforeNewEntry` ja laajempi kilpailu-/poikkeamatestimatriisi jäävät PARK-02:lle. Keskeytys ei saa vapauttaa paikkaa tai siirtää vanhaa Parking-yhteyttä. Nykyinen virhe-/tulossopimus ja testisauma ovat jatkokäyttöön valmiit. MVP-järjestys on PARK-01 → PARK-02 → PARK-04 → PARK-03; hallinta ei kuulu tähän itemiin. Ei authia, maksamista, varauksia tai deploymentin uudelleenrakentamista.