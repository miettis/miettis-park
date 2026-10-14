# Miettis Park

.NET-, PostgreSQL- ja Vue/Nuxt-pohjainen pysäköintihallisimulaatio rekrytoinnin ennakkotehtävään. Koko MVP sisältää porttisimulaation `/` ja lukevan hallintanäkymän `/admin`.

- [SPEC.md](SPEC.md): alkuperäinen toiminnallinen ja tekninen sopimus.
- [STARTING-POINT.md](STARTING-POINT.md) ja [BACKLOG.md](BACKLOG.md): lähtöpohjan ja työpakettien historiallinen suunnitelma.
- [AI-USAGE.md](AI-USAGE.md): toteutunut AI-/ihmistyö.
- [VALIDATION.md](VALIDATION.md): paikallinen näyttö, testimatriisi ja myöhemmin ajettava ihmisen ympäristötarkistus.
- [Käyttöliittymäluonnokset](ui-mocks/index.html): erillinen prototyyppi esimerkkidatalla, ei sovelluksen tietolähde.

Projektin 34 nykyinen toimeksianto kattaa PARK-01 → PARK-02 → PARK-04 → PARK-03, myös adminin. Se korvaa BACKLOGin vanhan adminin myöhempää milestonea koskevan rajauksen. Käyttäjän 14.10.2026 hyväksymä palveluriippumaton agenttihyväksyntä korvaa dokumenttien vanhat ulkoiset valmistumisportit; ei SPEC:n liiketoimintasääntöjä. Oikea selain, Docker ja PostgreSQL tarkistetaan myöhemmin ihmisen toimesta. Paikallinen hyväksyntä ei tarkoita ympäristöhyväksyntää tai tuotantovalmiutta.

## Toteutus ja arkkitehtuuri

`src\MiettisPark.Api`: .NET 10, ohut `GaragesController`, scoped `ParkingService`, EF Core/Npgsql ja PostgreSQL 17. Pohjassa `a82ca21` ovat malli, InitialCreate-migraatio ja seed: yksi halli, paikat 1–5 ja kaksi hintaporrasta. Hinnasto on tyypitetty jsonb-taulukko, kestot TimeSpan-arvoja, ajat UTC DateTimeOffset -arvoja ja raha decimal-euroja. Tuotannossa ei ole rinnakkaista muistivarastoa.

`src\client`: staattinen Nuxt/Vue SPA. `/` simuloi porttia ja paikkahavaintoja sekä näyttää palvelimen kuitin. `/admin` näyttää saatavuuden, kaikki aktiiviset pysäköinnit ja erilliset hallikäynnit vain GET-kutsuilla. Suhteellinen `/api` kulkee kehityksessä Nuxt-proxyn kautta; tuotannossa .NET palvelee API:n ja staattisen frontendin samasta originista. Runtime ei tarvitse Node-palvelinta. `/api/health` on liveness, ei DB-readiness.

SDK/Node-versiot ovat `global.json`- ja `.node-version`-tiedostoissa; NuGet/npm-riippuvuudet lukittuina. Sovelluksen ajo tarvitsee .NET SDK:n, Node 24:n ja PostgreSQL 17:n tai Dockerin. Paikalliset buildit/testit tarvitsevat vain .NETin, Noden ja riippuvuuksien restoren, eivät palvelinprosessia, tietokantapalvelua, konttia, selainajuria tai salaisia asetuksia.

## Kehityskäynnistys

Repojuuressa PowerShellillä:

```powershell
Copy-Item .env.example .env
# Aseta .env-tiedostoon oma paikallinen POSTGRES_PASSWORD.
docker compose up -d database
dotnet tool restore
$env:ConnectionStrings__Parking = 'Host=localhost;Database=miettis_park;Username=miettis_park;Password=<paikallinen salasana>'
dotnet run --project src\MiettisPark.Api
```

Backend: http://localhost:5080. Käytä yhteysmerkkijonossa samoja DB-/käyttäjä-/porttiarvoja kuin `.env`:ssä. .NET ei lue `.env`:ää, Compose lukee. Vaihtoehtona oma PostgreSQL. Älä tallenna salasanaa versionhallintaan; Compose-esimerkin salasanassa ei pidä käyttää yhteysmerkkijonon erottimia kuten puolipistettä.

Toisessa terminaalissa:

```powershell
Set-Location src\client
npm ci
npm run dev
```

Avaa http://localhost:3000 tai http://localhost:3000/admin. `API_PROXY_TARGET` voi vaihtaa backend-originin; älä lisää `/api`-päätettä. `Database__InitializeOnStartup=true` (oletus) soveltaa migraatiot ennen HTTP-käynnistystä. Seed ei nollaa dataa. Tämä on simulaation yksinkertaistus; oikean tuotannon migraatio-oikeudet ja deployment suunnitellaan erikseen.

## Palveluriippumattomat tarkistukset

Repojuuresta, Windowsissa ja Linuxissa (Linuxissa käytä poluissa `/`):

```powershell
node scripts\openapi.mjs
dotnet build MiettisPark.slnx -c Release
dotnet test MiettisPark.slnx -c Release
npm run check
```

`npm run check` tekee clientissä puhtaan `npm ci` → lint → typecheck → Vitest/jsdom-DOM-testit → staattisen tuotantobuildin. Windows/Linux-runner ratkaisee cwd:n omasta tiedostostaan ja käyttää aktiivista npm CLI:tä; ensimmäinen virhe pysäyttää ajon. Ei valmiisiin node_modules-tiedostoihin luottamista.

xUnit testaa tuotannon palveluja testikellolla ja testiin rajatulla EF InMemory -providerilla/transaktiotuplalla. API-putki ajetaan prosessin sisäisessä TestServerissä; DOM-testit käyttävät API-stubeja. Koordinoidut erillisten DbContextien kilpailut, tilannekuvarollback ja injektoidut DB-poikkeamat todistavat palvelun käyttäytymisen **vain testimallissa**, eivät PostgreSQL:n lukkoja, rollbackia tai SQL-rajoitteiden runtimea. Samat backend-testit tarkistavat Npgsql-mallin, idempotentin PostgreSQL-migraatio-SQL:n ja mallin/migraation yhtäpitävyyden offline.

### OpenAPI ja EF offline

`node scripts\openapi.mjs` tuottaa `src\MiettisPark.Api\OpenApi\miettis-park.json`; sama generointi tapahtuu backend-buildissa. Käytössä ovat Microsoft.AspNetCore.OpenApi ja Microsoft.Extensions.ApiDescription.Server, ei ulkoista generaattoria. Entry pointin metadata-ajossa ohitetaan DB/startup-migraatiot eikä avata kuuntelevaa palvelinta. Ajonaikainen kuvaus: `/openapi/v1.json`.

```powershell
dotnet restore MiettisPark.slnx --locked-mode
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project src\MiettisPark.Api --configuration Release
dotnet ef migrations script --idempotent --project src\MiettisPark.Api --configuration Release --output artifacts\migrations.sql
```

Design-time DbContextFactory ei avaa yhteyttä näissä offline-komennoissa. Tietokantaan soveltaminen: `dotnet ef database update --project src\MiettisPark.Api`, kun yhteys on asetettu. Älä käytä EnsureCreatedia migraatioiden rinnalla. Tavallinen `dotnet test` tarkistaa mallin ja SQL:n ilman erillistä EF CLI -ajoa.

## Konttikäynnistys ja erillinen julkaisu

Kun `.env` on asetettu:

```powershell
docker compose up --build -d
```

Sovellus: http://localhost:8080 ja http://localhost:8080/admin. Olemassa oleva Dockerfile rakentaa frontendin/backendin yhteen ei-root-imageen; PostgreSQL on erillinen palvelu ja data nimetyssä volumessa. `docker compose down` säilyttää datan; **älä käytä `down -v`:tä pysyvyystestissä**. APP_PORT/POSTGRES_PORT ovat muutettavissa `.env`:ssä. Tämä käynnistys ja restart-pysyvyys ovat vielä ajamatta; toistettava tarkistus on VALIDATION.md:ssä.

Pohjan GitHub Actions julkaisee GHCR-imagen semanttisesta tagista. Julkaisuworkflowta/GHCR-oikeuksia ei ole validoitu oikealla tagiajolla. PullPotential omistaa commitit, PR:t ja milestone-integraation. Ihmisen release/tagi on erillinen; agenttityö ei tee manuaalista commitia/pushia/tagia, image-pushia tai deployta.

## API ja pysyvät sopimukset

Tunnisteet ovat `int`; Contracts\GarageContracts.cs on DTO-sopimus. Kahdeksan reittiä:

| Metodi/reitti | Sopimus |
| --- | --- |
| GET `/api/garages` | `id`, `name`. |
| GET `/api/garages/{garageId}/availability` | `capacity`, `activeVisits`, `freeLots`, `occupiedLots`, `unknownLots`, `isFull`, paikkalista. |
| GET `/api/garages/{garageId}/visits/active` | VisitId, rekisteri, alku, nullable paikka. |
| GET `/api/garages/{garageId}/parkings/active` | ParkingId, paikka, nullable VisitId/rekisteri, Parking-alku. |
| POST `/api/garages/{garageId}/entries` | `{licenseNumber}` → Entry/Duplicate/RecoveredMissingExit, VisitId, palvelinaika. |
| POST `/api/garages/{garageId}/lots/{lotId}/entries` | `{visitId?}`, myös `{}`/`{visitId:null}` → Parking. |
| POST `/api/garages/{garageId}/lots/{lotId}/exit` | Erillinen vapautushavainto, nullable ParkingId ja missingParkingObservation. |
| POST `/api/garages/{garageId}/exits` | `{licenseNumber}` → tallennettu kuitti, ajat, duration, numeerinen cost, EUR, gateOpened. |

Enumit ovat JSON-merkkijonoja, kestot .NET TimeSpan -merkkijonoja ja ajat UTC-aikaleimoja. Palvelin normalisoi rekisterin Trim + invariant uppercase; tyhjä/yli 20 merkkiä hylätään. Pyyntö-DTO:t eivät anna asiakkaalle StartAt/EndAt/Cost-auktoriteettia. Virheet ovat turvallisia ProblemDetails-vastauksia: `code`, `gateOpened=false`, tarvittaessa `requiresCustomerService=true`. Syöte 400, puuttuva/toisen hallin tunniste 404, tilakonflikti 409; tuntematon virhe geneerinen INTERNAL_ERROR/500 ilman SQL-/stack trace -vuotoa.

### Hinnoittelu ja invariantit

- ParkingFeeCalculator summaa decimal-eurot alkavina tick-tarkkoina yksikköinä. Ensimmäiset 3 h: 0,50 €/alkava 10 min; sen jälkeen 0,30 €. Nolla = 0 €, 3 h = 9,00 €, 3 h + 1 tick = 9,30 €. TimeProvider määrää palvelinajat; ulosajossa Cost/EndAt/ClosedAt tallennetaan kerran. Hinnasto on ajon aikana kiinteä.
- Visit ja Parking ovat itsenäisiä. VisitId on valinnainen; annettu yhteys validoidaan samassa transaktiossa aktiiviseksi saman hallin käynniksi ilman toista aktiivista Parkingia. Ulosajo/keskeytys **ei vapauta paikkaa**. Vain LeaveLot sulkee Parkingin ja vapauttaa paikan. Paikanvaihto ei nollaa Visitin laskutusta.
- Saatavuus perustuu Free/Occupied/Unknown-paikkatiloihin, ei Visit-määrään. Täysi halli ei estä sisäänajohavaintoa. Occupied/Unknown ei ole valittavissa, mutta erillinen vapautushavainto toimii myös ilman luotettavaa käyntiyhteyttä.
- ParkingWriteTransaction aloittaa PostgreSQL ReadCommitted -transaktion: parametrisoitu hallirivin FOR UPDATE ennen paikan lukkoa/tarkistuksia. Monipaikkakomentojen mahdollinen järjestys on id-järjestys. Hallilukko serialisoi saman hallin kirjoitukset; partial Active -uniikki-indeksit/FK/lifecycle ovat viimeinen suoja. Tunnettu unique-konflikti muunnetaan 409:ksi transaktion purkamisen jälkeen, muut DB-viat eivät muutu domain-konflikteiksi.
- UI hakee tilanteen uudelleen onnistumisesta ja konfliktista; reload käyttää API:a, ei localStorage-totuutta. Uusi ulosajoyritys tyhjentää vanhan kuitin.

### Duplicate-heuristiikan oletus ja riski

`Parking:DuplicateEntryWindow` / `Parking__DuplicateEntryWindow`: TimeSpan, oletus `00:02:00`. Negatiivinen/virheellinen arvo hylätään; nolla sallii vain saman hetken toiston. Ikä lasketaan alkuperäisestä StartAt:sta; toistot eivät siirrä rajaa.

Ikä <= raja palauttaa saman Visitin Duplicatena. Ikä > raja sulkee vanhan Interrupted-tilaan (MissingExitBeforeNewEntry, ClosedAt=nyt, EndAt/Cost=null) ja avaa uuden. Vanhan sulkeminen tallennetaan **ennen** uuden lisäystä saman transaktion sisällä. Vanha Parking/historiallinen VisitId/paikkatila säilyvät. Yli rajan virhehavainto tai väärinkäyttö voi aloittaa laskutuksen uudelleen: tämä ei ole luotettava idempotenssi. Pysyvä request-id ja asiakaspalvelukorjaus/ohitus ovat jatkokehitystä.

Puuttuva/toistettu ulosajo: ACTIVE_VISIT_NOT_FOUND, portti kiinni, asiakaspalvelutarve, ei arvattua hintaa tai vanhan kuitin palautusta. UI ei keksi yhteystietoa.

### Adminin näyttöraja

Admin näyttää API-määrät ja kaikki aktiiviset Parkingit, myös tunnistamattomat ja suljettuun Visitiin liittyvät. Puuttuva rekisteri = ”Ei tiedossa”; erillisen aktiivisen Visitin null-paikka = ”Paikka ei tiedossa”, ei sijaintipäätelmä. Historiallista rekisteriä ei yhdistetä uuteen käyntiin.

Kulunut aika on Parking.startAt:n ja selaimen viimeisen onnistuneen päivityshetken erotus, suuntaa-antava, **ei laskutusperuste**. Ei live-ajastinta/hintaa. Kaikkien hakujen päättymistä odotetaan ennen uuden tilanteen julkaisua tai rinnakkaiseston vapautusta; virheessä vanha tilanne piilotetaan ja retry tarjotaan. Erilliset GET-resurssit eivät muodosta backendin atomista kokonaiskuvaa.

## Rajaus, luovutus ja seuraavat askeleet

Toteutettu: normaali polku, itsenäiset paikkahavainnot, hinnoittelu/kuitti, Duplicate/recovery/virhepolut, turvalliset konfliktit, API-reload, lukeva admin ja kertynyt paikallinen testiharness. PARK-03 lisää vain palvelun 3 h -rajan tallennustestin ja koko MVP:n loppuhyväksyntä-/luovutusdokumentit.

Pois rajattu: auth, oikea maksaminen/portit/kamerat, varaukset, huoltotilat, monihalli-UI, ajonaikainen hinnasto/hintaversiot, varma kameraviestien idempotenssi, manuaalinen laskutus/portinohitus, automaattinen Unknown-sovitus, reaaliaikapäivitys ja tuotantodeploymentin uudistus.

Milestone/59 sisältää PARK-01 (`2715700`, PR #1), PARK-02 (`4a98c71`, PR #2) ja PARK-04 (`b0986bf`, PR #3). PARK-03/item 1119 on tällä hetkellä toteutus/submissionissa, **ei vielä integroitu**: PullPotential finalisoi sen vasta submissionin jälkeen. Milestone on InProgress, ei julkaistu release. Nykyrevision kolmen verification-avaimen lopputulos ja pysyvä checklist/submission ovat platformissa; aiempien itemien näyttö ei korvaa niitä.

GitHub Actions julkaisee imagen `ghcr.io/<owner>/<repository>` semanttisesta tagista, esimerkiksi `v1.2.3` tai `v1.2.3-rc.1`. Virheellinen versiotagi pysäyttää työn ennen julkaisua. Docker-tagit ovat versio ilman v-etuliitettä, `sha-<12 merkkiä>` ja vain vakaalle versiolle `latest`. SemVerin mahdollinen `+build` tallennetaan Docker-tagissa `_build`-muodossa; build-metadatatagi ei muuta latestia. Julkaisu käyttää GitHubin GITHUB_TOKENia ja packages:write-oikeutta. Imagea ei ole vielä julkaistu eikä workflowta ajettu GitHubissa.

## V2-suunnitelma

MVP:n jälkeinen hallien ylläpito, ruudukkopohjat ja kartta: [SPEC-V2.md](SPEC-V2.md) ja [BACKLOG-V2.md](BACKLOG-V2.md). Suunnitelma ei tarkoita, että ominaisuudet on toteutettu tai työitemit luotu PullPotentialiin.

## V3-suunnitelma

Autokuvasta tunnistettu rekisteri kirjaa sisäänajon ilman manuaalista hyväksyntää; simulaatiotapahtumille voi valita ajan. [SPEC-V3.md](SPEC-V3.md) ja [BACKLOG-V3.md](BACKLOG-V3.md). Kyse on suunnitelmasta; v3-työitemeitä tai toteutusta ei luotu tällä toimeksiannolla.
