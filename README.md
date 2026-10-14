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

## PARK-02: uusintasisäänajo ja poikkeamat

`Parking:DuplicateEntryWindow` (ympäristömuuttuja `Parking__DuplicateEntryWindow`) on .NETin TimeSpan-merkkijono, oletus `00:02:00`. Esimerkiksi `00:00:30` muuttaa rajan 30 sekuntiin. Negatiivinen tai virheellisen muotoinen asetus hylätään käynnistyksessä; nolla sallii Duplicaten vain samalla palvelinhetkellä. Ikä lasketaan palvelimen nyt-ajasta alkuperäiseen `Visit.StartAt`:iin tick-tarkkuudella. Toisto ei siirrä ikkunaa.

- Ikä <= raja: `Duplicate`, sama VisitId ja StartAt, ei paikkamuutoksia.
- Ikä > raja: `RecoveredMissingExit`, vanha Visit `Interrupted`, `ClosureReason=MissingExitBeforeNewEntry`, `ClosedAt=nyt`, `EndAt/Cost=null`. Uusi Active Visit alkaa nyt. Vanhan sulkemisen `SaveChangesAsync` suoritetaan ennen uuden rivin lisäystä saman hallilukitun ReadCommitted-transaktion sisällä; epäonnistuminen rollbackaa molemmat. EF:n INSERT/UPDATE-järjestystä ei arvata.
- Vanhan Visitin Parking, sen historiallinen VisitId ja paikkatila eivät muutu tai siirry uudelle Visitille. Vain paikan oma vapautushavainto sulkee Parkingin. Tämä toimii myös null VisitId:n ja Completed/Interrupted Visitin yhteydessä.
- Puuttuva/toistettu ulosajo palauttaa 409 `ACTIVE_VISIT_NOT_FOUND`, `gateOpened=false`, `requiresCustomerService=true`, ei hintaa tai historiallista kuittia. Aiemman kuitin tiedot eivät muutu.

UI erottaa Entry-onnistumisen, Duplicate-informaation, recoveryn saavutettavan varoituksen ja virheen. Onnistuminen ja 409 hakevat saatavuuden, aktiiviset käynnit ja pysäköinnit API:sta uudelleen; suljettu Visit ei jää valittavaksi. Uusi ulosajoyritys poistaa edellisen kuitin, joten epäonnistuminen ei näytä vanhaa onnistumista uutena.

**Heuristiikan rajoitus:** yli rajan tuleva porttihavainto voi aloittaa laskutuksen uudelleen myös virheellisesti tai väärinkäytössä. Tämä ei ole varma häiriön-/väärinkäytöntunnistin. Pysyvää request-id-idempotenssia, asiakaspalvelun manuaalista korjausta/laskutusta tai portin ohitusta ei ole; UI ohjaa asiakaspalveluun keksimättä yhteystietoa. Ei authia, oikeaa maksamista tai varauksia.

### Luovutus PARK-04 / PARK-03

Kahdeksan reittiä ja DTO:t eivät muuttuneet; `RecoveredMissingExit` on nyt toteutettu. Lukeva admin käyttää samoja availability/active visits/active parkings -vastauksia: suljettuun Visitiin liittyvä Parking voi edelleen olla aktiivinen. Null rekisteri tai paikkayhteys tarkoittaa tuntematonta tietoa, ei sijaintipäätelmää. MVP-järjestys PARK-01 → PARK-02 → PARK-04 → PARK-03.

Samalla `dotnet test` -komennolla ajettavat palvelu-/TestServer-testit käyttävät testikelloa. Testiin rajattu transaktiotupla tarjoaa yhteisen serialisoinnin ja tilannekuvan palautuksen; kilpailijat kohtaavat rendezvous-pisteessä ennen lukkoa, erillisillä DbContexteilla, ilman sleep-ajoitusta. Se todistaa palvelun reagoinnin tähän malliin, **ei PostgreSQL:n** lukkoja, uniikkiusrajoitteita tai rollbackia. Offline Npgsql-malli ja idempotentti migraatio-SQL tarkistetaan testeissä; skeemaa ei muutettu.

Oikean PostgreSQL:n synkronoidut kilpailut, halli-ennen-paikkaa-lukitus, indeksien runtime, recoveryn rollback, migraatiot ja restart-pysyvyys sekä Docker/yhden originin SPA-refresh ja oikea selain/visuaalinen hyväksyntä ovat edelleen **ajamatta**, myöhempiä ihmisen tarkistuksia. Ne eivät ole paikallisen agenttihyväksynnän portteja. Komennot ja näyttöraja ovat yllä ja [VALIDATION.md](VALIDATION.md):ssä.

## PARK-04: lukeva hallintanäkymä ja PARK-03-luovutus

Reitti `/admin` avautuu porttisimulaation Hallinta-linkistä; paluulinkki vie `/`-reitille. Näkymä valitsee ensimmäisen hallin kuten nykyinen yhden hallin UI. Se käyttää vain nykyisiä suhteellisia GET-reittejä `/api/garages`, `/{garageId}/availability`, `/{garageId}/parkings/active` ja `/{garageId}/visits/active`. Ei kirjoituksia, authia, hinnaston muokkausta tai portinohitusta; backend, skeema ja laskutus eivät muutu.

Saatavuuden capacity/freeLots/occupiedLots/unknownLots/activeVisits/isFull tulevat suoraan availability-DTO:sta. Kaikki aktiiviset pysäköinnit näkyvät, myös null VisitId:n havainnot ja suljettuun Visitiin edelleen liittyvä Parking. Rekisteri on tällöin API:n historiallinen lisätieto, ei yhteys saman rekisterin uuteen käyntiin. Puuttuva rekisteri on ”Ei tiedossa”. Erillisen hallikäyntitaulukon null-paikka on ”Paikka ei tiedossa”; siitä ei päätellä auton fyysistä sijaintia.

Kulunut aika = selaimen viimeisin onnistunut päivityshetki − **Parking.startAt**, ei Visit.startAt. Näyttö on suuntaa-antava, ei laskutusperuste tai palvelimen kuitin korvike. Tuleva alku rajataan nollaan (”Alle minuutti”), virheellinen aika on ”Ei tiedossa”. Päivityshetki näytetään; ei live-ajastinta, hintalaskentaa tai kellon lähettämistä API:lle.

Manuaalinen päivitys hakee kaikki kolme tilanneresurssia. Promise.allSettled odottaa myös virheessä kaikkien hakujen päättymistä ennen rinnakkaiseston vapautusta; uudet tiedot julkaistaan vain kaikkien onnistuttua. Päivityksen ja virheen aikana vanha tilanne piilotetaan, lataus/alert ja uudelleenyritys ovat näkyvissä. Erilliset endpointit eivät muodosta backendin atomista tilannekuvaa. Tyhjät halli-/pysäköinti-/käyntilistat erotetaan. Natiivit linkit/painikkeet, fokustyylit, taulukoiden otsikot/captionit sekä fokusoitavat vaakavieritysalueet tukevat näppäimistökäyttöä.

**PARK-03:** aja repojuuresta `dotnet build MiettisPark.slnx -c Release`, `dotnet test MiettisPark.slnx -c Release`, `npm run check` ja ennen lopullista verificationia `node scripts/openapi.mjs`. API/DTO/invariantit ovat ennallaan; adminin DOM-testit ja lukusopimustesti kuuluvat nykyisiin runnereihin. Paikalliset tulokset ja näyttörajat: VALIDATION.md. Oikean selaimen visuaalinen/näppäimistötarkistus, samasta Docker-imagesta suora `/admin` ja refresh, yhden originin API sekä PostgreSQL-lukitus/kilpailu/migraatio/rollback/restart-pysyvyys ovat ajamatta, myöhempiä ihmisen tarkistuksia, eivät agenttityön portteja.