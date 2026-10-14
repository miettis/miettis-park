# Miettis Park

.NET-, PostgreSQL- ja Vue/Nuxt-pohjainen pysäköintihallisimulaatio rekrytoinnin ennakkotehtävään. Nykyinen v2 sisältää hallikartan `/`, hallikohtaisen porttisimulaation `/garages/{id}` sekä lukevan hallinnan ja halliylläpidon `/admin`-osiossa.

- [SPEC.md](SPEC.md): alkuperäinen toiminnallinen ja tekninen sopimus.
- [STARTING-POINT.md](STARTING-POINT.md) ja [BACKLOG.md](BACKLOG.md): lähtöpohjan ja työpakettien historiallinen suunnitelma.
- [AI-USAGE.md](AI-USAGE.md): toteutunut AI-/ihmistyö.
- [VALIDATION.md](VALIDATION.md): paikallinen näyttö, testimatriisi ja myöhemmin ajettava ihmisen ympäristötarkistus.
- [Käyttöliittymäluonnokset](ui-mocks/index.html): erillinen prototyyppi esimerkkidatalla, ei sovelluksen tietolähde.

MVP:n toteutusjärjestys oli PARK-01 → PARK-02 → PARK-04 → PARK-03, myös admin. V2:n järjestys on PARK-V2-01 → PARK-V2-02 → PARK-V2-03; PARK-V2-01/02 toteuttivat ominaisuudet ja PARK-V2-03 kokoaa paikallisen hyväksyntänäytön ja luovutuksen. Se korvaa BACKLOGin vanhan adminin myöhempää milestonea koskevan rajauksen. Käyttäjän 14.10.2026 hyväksymä palveluriippumaton agenttihyväksyntä korvaa dokumenttien vanhat ulkoiset valmistumisportit; ei SPEC:n liiketoimintasääntöjä. Oikea selain, Docker ja PostgreSQL tarkistetaan myöhemmin ihmisen toimesta. Paikallinen hyväksyntä ei tarkoita ympäristöhyväksyntää tai tuotantovalmiutta.

## Toteutus ja arkkitehtuuri

`src\MiettisPark.Api`: .NET 10, ohut `GaragesController`, scoped `ParkingService`, EF Core/Npgsql ja PostgreSQL 17. Pohjassa `a82ca21` ovat malli, InitialCreate-migraatio ja seed: yksi halli, paikat 1–5 ja kaksi hintaporrasta. Hinnasto on tyypitetty jsonb-taulukko, kestot TimeSpan-arvoja, ajat UTC DateTimeOffset -arvoja ja raha decimal-euroja. Tuotannossa ei ole rinnakkaista muistivarastoa.

`src\client`: staattinen Nuxt/Vue SPA. `/` näyttää hallikartan ja listan. `/garages/{id}` simuloi porttia ja paikkahavaintoja sekä näyttää palvelimen kuitin. `/admin` näyttää saatavuuden, kaikki aktiiviset pysäköinnit ja erilliset hallikäynnit vain GET-kutsuilla. Suhteellinen `/api` kulkee kehityksessä Nuxt-proxyn kautta; tuotannossa .NET palvelee API:n ja staattisen frontendin samasta originista. Runtime ei tarvitse Node-palvelinta. `/api/health` on liveness, ei DB-readiness.

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

Tunnisteet ovat `int`; Contracts\GarageContracts.cs on DTO-sopimus. MVP-reitit (v2:n lisäykset jäljempänä):

| Metodi/reitti | Sopimus |
| --- | --- |
| GET `/api/garages` | `id`, `name`, osoite/piste sekä aktiivisten paikkojen saatavuusyhteenveto. |
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

## MVP:n historiallinen rajaus ja luovutus

Toteutettu: normaali polku, itsenäiset paikkahavainnot, hinnoittelu/kuitti, Duplicate/recovery/virhepolut, turvalliset konfliktit, API-reload, lukeva admin ja kertynyt paikallinen testiharness. PARK-03 lisäsi palvelun 3 h -rajan tallennustestin ja koko MVP:n loppuhyväksyntä-/luovutusdokumentit.

Pois rajattu: auth, oikea maksaminen/portit/kamerat, varaukset, huoltotilat, ajonaikainen hinnasto/hintaversiot, varma kameraviestien idempotenssi, manuaalinen laskutus/portinohitus, automaattinen Unknown-sovitus ja tuotantodeploymentin uudistus. Reaaliaikapäivitys oli pois MVP:stä, mutta SSE kuuluu nykyiseen v2:een.

Milestone/59 sisältää PARK-01 (`2715700`, PR #1), PARK-02 (`4a98c71`, PR #2) ja PARK-04 (`b0986bf`, PR #3). MVP-itemit 1116–1119 ja milestone 59 ovat nykyisen projektitoimeksiannon mukaan Done. Tässä itemissä ei tehty mergeä tai julkaisua. Nykyrevision kolmen verification-avaimen lopputulos ja pysyvä checklist/submission ovat platformissa; aiempien itemien näyttö ei korvaa niitä.

GitHub Actions julkaisee imagen `ghcr.io/<owner>/<repository>` semanttisesta tagista, esimerkiksi `v1.2.3` tai `v1.2.3-rc.1`. Virheellinen versiotagi pysäyttää työn ennen julkaisua. Docker-tagit ovat versio ilman v-etuliitettä, `sha-<12 merkkiä>` ja vain vakaalle versiolle `latest`. SemVerin mahdollinen `+build` tallennetaan Docker-tagissa `_build`-muodossa; build-metadatatagi ei muuta latestia. Julkaisu käyttää GitHubin GITHUB_TOKENia ja packages:write-oikeutta. Imagea ei ole vielä julkaistu eikä workflowta ajettu GitHubissa.

## V2-sopimus ja toteutus

MVP:n jälkeinen hallien ylläpito, ruudukkopohjat ja kartta: [SPEC-V2.md](SPEC-V2.md) ja [BACKLOG-V2.md](BACKLOG-V2.md). PARK-V2-01:n ja PARK-V2-02:n toteutukset kuvataan alla; molemmat sisältyvät nykyiseen lähtörevisioniin. SPEC.md/BACKLOG.md/STARTING-POINT.md kuvaavat historiallista MVP-/pohjascopea: vanha `/`-simulaatio siirtyi `/garages/{id}`-reitille, monihalli ja kirjoittava halliylläpito lisättiin v2:ssa ja SSE korvasi pelkän manuaalisen päivityksen rajauksen. MVP:n valinnainen admin toteutettiin jo MVP:ssä nykyisen toimeksiannon mukaisesti. SPEC-V2.md täydentää ja näissä kohdissa korvaa MVP:n rajauksen; Visit/Parking- ja hinnoittelu-invariantit eivät muuttuneet.


## PARK-V2-01: hallit ja pohja

`/admin` säilyttää lukevat taulukot ja linkittää ylläpitoon. `/admin/garages` näyttää kaikkien hallien listan, `/admin/garages/new` luo hallin, `/admin/garages/{id}` muokkaa perustietoja ja `/admin/garages/{id}/layout` on 1–50 × 1–50 -editori. Ei authia tai rooleja. Uusi halli tarvitsee nimen, osoitteen ja pisteen; vanhan hallin puuttuva osoite/piste saa jäädä puuttumaan. Karttavirhe ei estä hallilistaa tai nykyistä porttisimulaatiota, mutta ei myöskään ohita uuden hallin pistevaatimusta.

| Lisäreitti | Sopimus |
| --- | --- |
| POST `/api/garages` | `{name,address,latitude,longitude}` → 201 GarageDto; oma MVP-oletushinnasto, tyhjä 1×1-pohja, capacity=0. |
| PUT `/api/garages/{id}` | Samat perustiedot → GarageDto; ei muuta hinnastoa, pohjaa tai käyttötiloja. |
| GET `/api/garages/{id}/layout` | Kanoninen LayoutDto, hetkellinen poistokelpoisuus. |
| PUT `/api/garages/{id}/layout` | Täysi pohja, odotettu versio ja nimenomaiset poistot → uusi kanoninen LayoutDto. |

GarageDto laajenee yhteensopivasti kentillä `address`, `latitude`, `longitude`, `capacity`, `freeLots`, `occupiedLots`, `unknownLots`, `isFull`. Puuttuva piste on null, ei (0,0). Occupied ja Unknown lasketaan erikseen vain aktiivisista paikoista; Visit-määrä ei ole paikkaluku. isFull tarkoittaa freeLots==0, myös tyhjässä hallissa.

LayoutDto: `{garageId,width,height,version,cells:[{x,y,type,lotId}],lots:[{id,number,currentStatus,canRemove}]}`. Koordinaatit alkavat nollasta; type on `Empty`, `Road` tai `ParkingLot`. JSON-pohjassa ei säilytetä tilan tai numeron rinnakkaiskopiota. GET lukee version/pohjan/paikat yhdellä SQL-lauseella. PUT: `{expectedVersion,width,height,cells:[{x,y,type,lotId?,number?}],removedLotIds:[]}`. Jokainen ruutu annetaan kerran; Empty/Road ei sisällä ID:tä/numeroa, vanhalla paikalla ovat molemmat, uudella lotId=null ja number=null automaattiin tai positiivinen käsin valittu numero. removedLotIds vastaa täsmälleen pois jätettyjä aktiivisia paikkoja. Pelkkä pienennys/omission ei poista paikkaa. Onnistunut vastaus sisältää palvelimen luomat oikeat ID:t ja kasvatetun version.

Muokkaus on paikallinen luonnos: ajorata, pysäköintipaikka, pyyhekumi, klikkaus/näppäimistö ja pointer-veto. Ei alueentäyttöä. Siirto menee tyhjään ruutuun; numeroa voi muuttaa. Paikkojen yli ei pienennetä; ulkopuolisen ajoradan hävitys vahvistetaan. Peruuta palauttaa tallennetun pohjan, poistuminen/reload varoittaa ja tallennusvirhe säilyttää luonnoksen. Muutokset eivät lähetä kirjoitusta ennen Tallenna-toimintoa.

### Invariantit ja luovutus PARK-V2-02:lle

- `ParkingLot.IsActive` on nykykapasiteetin, listojen ja paikkakomentojen eksplisiittinen ehto. Ei globaalia query filteriä: historia ja FK-suhteet pysyvät luettavina. Eläköinti ei fyysisesti poista riviä. Eläköity ID → LOT_NOT_FOUND; vanhan numeron voi käyttää uudelleen eri ID:llä.
- Vain Free ja ilman Active Parkingia voidaan eläköidä. Occupied, Unknown ja ristiriitainen Free+Active Parking → LOT_REMOVAL_CONFLICT/409. Siirto/numeron muutos säilyttävät ID:n, tilan ja historian myös käytössä olevalla paikalla.
- ReadCommitted, hallin FOR UPDATE ensin ja aktiiviset paikat ID-järjestyksessä; sama sopimus simulaatiolle. Versio/poistoehdot tarkistetaan lukon jälkeen. Numerovaihto käyttää vapaita positiivisia väliaikaisnumeroita saman transaktion sisällä; osittainen uniikki-indeksi pysyy voimassa. Automaatti valitsee pienimmät vapaat positiiviset numerot lopullisesta aktiivijoukosta y/x-järjestyksessä. Ei tuotannon muistilukkoa.
- `GarageManagementService.WriteAsync` ja `ParkingService.WriteAsync` palauttavat vasta commitin jälkeen: seuraavan itemin SSE julkaistaan vasta onnistuneen commitin jälkeen. PARK-V2-01:ssä ei vielä ollut SSE-julkaisijaa; nykyinen PARK-V2-02-kytkentä julkaisee commitin ja disposen jälkeen.
- Virheet: INVALID_GARAGE/INVALID_LAYOUT/LOT_NUMBER_CONFLICT 400, GARAGE_NOT_FOUND/LOT_NOT_FOUND 404 (myös risti-ID), LAYOUT_VERSION_CONFLICT/LOT_REMOVAL_CONFLICT 409. Tuntematon tallennusvika ei muutu onnistumiseksi.
- Migraatio `GarageLayouts` säilyttää nykyiset ID:t, numerot, tilat, hinnastot ja Visit/Parkingit. Number/Id-järjestyksen pohja: enintään 50 paikkaa/rivi, tyhjä halli 1×1. Yli 2500 lähtöpaikkaa aiheuttaa selkeän virheen, ei typistystä tai reset-seediä. Puuttuvia perustietoja ei keksitä. Todellinen PostgreSQL-migraatioajo on vielä ajamatta.

### Jaettu karttakomponentti ja verkkoriippuvuus

`GarageMap.vue` käyttää lukittua **MapLibre GL JS 6.14.0** -kirjastoa, ei mock-karttaa. Props: `latitude`, `longitude` (nullable), `label` (hallin nimi), `selectable` (oletus false); `select` antaa `{latitude,longitude}` klikkauksesta tai näppäimistöllä käytettävästä näkyvän keskipisteen valinnasta. Puuttuva piste ei tee oletusmerkkiä. Lataus-, WebGL-/verkkovirhe ja retry näkyvät; unmount poistaa kuuntelijat, markkerin ja map-resurssit myös kesken laiskan importin. Hallilista on responsiivinen kartasta riippumaton vaihtoehto.

Style on `https://tiles.openfreemap.org/styles/liberty` ([OpenFreeMap quick start](https://openfreemap.org/quick_start/), tarkistettu 14.10.2026). `services/mapLibre.ts` tuo paikallisesti bundlatun workerin Viten `?worker&url`-tuonnilla ja asettaa `setWorkerUrl`-arvon quick startin mukaisesti. Nuxt lataa kirjaston CSS:n. Omaa provider-frameworkia tai API-avainta ei tarvita. Kartta tarvitsee verkosta stylen, tiilet, fontit ja spritet; niille ei luvata offline-toimintaa tai saatavuutta. Testit injektoivat loader-stubin eivätkä hae karttaa verkosta.

MapLibren lähdeattribuutiota ei poisteta: **OpenFreeMap**, **© OpenMapTiles**, **Data from OpenStreetMap**, lähteiden linkit säilyttäen. Käyttöehdot ja attribuutiot: [OpenFreeMap](https://openfreemap.org/), [Terms of Service](https://openfreemap.org/tos/), [MapLibre Map API](https://maplibre.org/maplibre-gl-js/docs/API/classes/Map/). Viralliset sivut luettiin HTTP:llä, koska Context7-työkalua ei ollut tarjolla. Palvelu on as-is/as-available, voi muuttua tai päättyä eikä anna saatavuus-/tarkkuustakuuta; integroijalla tulee olla ehtojen mukainen ikä ja organisaation valtuutus. Noudata lähteiden lisenssejä, laillista käyttöä ja palvelun kuormitusrajoja; offline-keräilyä ei ole toteutettu. Ulkoiset pyynnöt/CDN voivat kulkea Cloudflaren kautta. Asetuksissa ei ole salaisuuksia.

## PARK-V2-02: hallikartta, simulaatio ja SSE

Etusivu `/` näyttää hallit yhteisellä `GarageMap`-komponentilla ja riippumattomana responsiivisena listana. Suora `/garages/{id}` lukee tallennetun `LayoutDto`-pohjan ja live-paikkatilat; virheellinen ID ei valitse ensimmäistä hallia. Sisäänajo, valinnainen Visit-yhteys, paikan käyttö/vapautus/vaihto ja palvelimen kuitti säilyvät. Visit ja Parking ovat itsenäisiä. Kapasiteetti ja `3/5 varattu · 1 tuntematon` lasketaan aktiivisista paikoista, ei käynneistä. Myös `0/0 varattu` on sallittu. Pohja ja hallin paikallinen yhteenveto käyttävät samaa layout-vastausta.

`GET /api/garages/events` on yhden tabin yhteinen EventSource: `text/event-stream`, tapahtuma `garage-changed`, JSON `{"garageId":1,"kind":"occupancy"}`. Muut lajit ovat `layout` ja `garage`. Ei rekisteriä, käyntitunnisteita tai henkilötietoja. Komennot käyttävät edelleen HTTP:tä. Julkaisu tapahtuu vasta commitin ja transaktion disposen jälkeen. Visit-muutos invalidioi käyntitiedot muuttamatta paikkalaskuria. Save/commit-virhe, rollback tai 409 ei julkaise.

Kuuntelu alkaa ennen alkuhaun valmistumista. Invalidointi, ei laskuridelta: hallilista sekä avoimen hallin pohja/käynnit/pysäköinnit haetaan uudelleen. Generation ja pending-uusintakierros hylkäävät vanhat vastauserät ja yhdistävät purskeet. Reitinvaihto erottaa vanhan hallin valinnat ja kuitit. Poistuminen peruu snapshot-haut ja kuuntelun. Katko säilyttää viimeisen datan; jokainen open/reconnect hakee kokonaisen snapshotin, epäonnistuminen näkyy vanhentumisena ja Päivitä toimii ilman streamia. Erilliset REST-haut eivät muodosta yhtä PostgreSQL-transaktiosnapshotia.

Taustapäivitys säilyttää rekisterikentän, olemassa olevan paikkavalinnan ja kuitin. Editorin live-päivitys koskee vain samojen ID:iden tilaa/poistokelpoisuusmetatietoa: luonnoksen ruudut, numerot ja expectedVersion eivät rebasetu. Muuttuneesta versiosta ilmoitetaan; luonnoksen hylkäävä reload vahvistetaan ja palvelimen 409 säilyy. Perustietolomaketta ei alusteta uudelleen listan SSE-päivityksestä.

### Streamin käyttörajat ja proxy

Julkaisija on yhden backend-prosessin muistissa, ei hajautettu fanout, outbox tai kestävä replay. Katkon/restartin jälkeen korjaus on uusi snapshot; Last-Event-ID-historiaa ei luvata. Tilaajakohtainen puskuri on 64 invalidointia. Julkaisu ei odota clienttiä: ylivuoto katkaisee tilauksen ja pakottaa reconnect/resyncin. Alkukommentti flushataan heti, heartbeat on 15 sekuntia ja yksittäisen kirjoituksen aikaraja 15 sekuntia. Peruutus poistaa tilauksen ja odotukset.

Vastauksessa `Cache-Control: no-cache, no-transform` ja `X-Accel-Buffering: no`. Tuotantoproxyssa poista SSE-reitin puskurointi ja pakkaava muunnos (esimerkiksi nginx `proxy_buffering off;`), salli pitkä stream ja aseta idle/read-timeout selvästi yli 15 sekunnin heartbeat-välin. Proxy-/kahden oikean selaimen toiminta on vielä ihmistarkistus.

Kartta käyttää **MapLibre GL JS 6.14.0** ja styleä `https://tiles.openfreemap.org/styles/liberty`. Kartta/editori jakavat loaderin ja komponentin. Markkerin ankkuri avaa hallin suoraan myös näppäimistöllä; occupancy-päivitys ei luo karttaa uudelleen tai resetoi zoomia. Puuttuva sijainti näkyy listalla, ei (0,0)-merkkinä; oikeat nollakoordinaatit ovat sallittuja. Karttavirhe/retry ei poista listaa tai estä simulaatiota. Tiilet, style, fontit ja WebGL tarvitsevat verkon/selaintuen; offline-karttaa tai saatavuuslupausta ei anneta. Nykyinen julkinen style ei tarvitse avainta. Attribuutiot: OpenFreeMap, OpenMapTiles ja OpenStreetMap. Lähteet/ehdot: [OpenFreeMap quick start](https://openfreemap.org/quick_start/), [ehdot](https://openfreemap.org/tos/), [MapLibre Map](https://maplibre.org/maplibre-gl-js/docs/API/classes/Map/) ja [Marker](https://maplibre.org/maplibre-gl-js/docs/API/classes/Marker/). Uudet marker/fitBounds-signatuurit tarkistettiin asennetun lukitun paketin tyypeistä; Context7 ei ollut käytettävissä.

### PARK-V2-03-luovutus

Säilytä GarageDto/LayoutDto, IsActive-historiasuoja, halli-ennen-paikkaa-lukitus ja hinnasto; mallimuutosta ei tehty. SSE/resync/luonnossuojaus yllä. Komennot repojuuressa: `node scripts\openapi.mjs`, `dotnet build MiettisPark.slnx -c Release`, `dotnet test MiettisPark.slnx -c Release`, `npm run check`. Oikea PostgreSQL-migraatio/kilpailut, Docker/SPA-refresh/restart, WebGL/ulkoinen kartta sekä kahden selaimen/proxyn SSE tarkistetaan myöhemmin. Ei authia, uutta infraa, releasea, mergeä tai deployta.

## PARK-V2-03: hyväksyntä ja lopullinen luovutus

Lähtöworktree oli puhdas, HEAD `0e8bdf8` (PARK-V2-02, PR #9); historiassa `6d4bcdb` (PARK-V2-01, PR #8), `dff550f` (MVP, PR #5) ja `a82ca21` (pohja). V2-03 lisää vain kokoavan `V2AcceptanceTests`-API-testin ja hyväksyntä-/luovutusdokumentit. Ei tuotantokoodin, mallin, providerin, riippuvuuksien tai hinnoittelun muutosta. Integraatio tapahtuu vasta PullPotentialin submission/finalisoinnin kautta; tämän itemin milestone-mergeä tai releasea ei väitetä tehdyksi.

Paikallinen kertynyt näyttö: **162 backend-testiä, 0 epäonnistunutta/ohitettua**, **62 Vitest/jsdom-testiä**, clean npm ci/lint/typecheck/staattinen build sekä Microsoftin offline OpenAPI. Vaatimus–testi–näyttöraja-matriisi ja AJAMATTA-ympäristörunbook ovat [VALIDATION.md](VALIDATION.md):ssä. Lopullisen saman revision kolme pakollista RunVerification-raporttia ovat platformissa; nämä workspace-luvut eivät korvaa niitä.

Jatkajan sopimukset: nykyiset ylläpito-/simulaatioreitit ja DTO:t yllä; Layout PUT käyttää expectedVersionia ja täsmällisiä removedLotIds-arvoja. IsActive säilyttää historian, siirto ei vaihda ID:tä, Visitin sulkeminen ei vapauta Parkingia. ReadCommitted/halli-ennen-paikkaa ja decimal/tick/TimeProvider säilyvät. SSE on yhden prosessin commitin jälkeinen invalidointi; reconnect hakee snapshotin ja editoriluonnos säilyttää odotetun version. MapLibre 6.14.0/OpenFreeMap, paikallinen worker ja avaimeton Liberty-style säilyvät. Tarkistuskomennot: `node scripts\openapi.mjs`, `dotnet build MiettisPark.slnx -c Release`, `dotnet test MiettisPark.slnx -c Release`, `npm run check`.

Oikeat selain/WebGL/kartta-, Docker/PostgreSQL/restart- ja kahden selaimen proxy-SSE-ajot ovat **AJAMATTA**, myöhemmälle ihmiselle; eivät agentin submission-portteja. Ihmisen lopullinen ympäristöhyväksyntä, milestone-release, SemVer-tagi ja image-julkaisu ovat erillisiä. Ei manuaalista Git-finalisointia, julkaisua tai deployta. Tunnetut npm-audit/build-varoitukset on kirjattu, ei yleistä upgrade-urakkaa tai turvallisuushyväksyntäväitettä.