# Pohjan validointi 14.10.2026

## Suoritettu

- .NET Release build, publish ja lukittujen NuGet-riippuvuuksien restore.
- Microsoftin sisäänrakennettu build-time OpenAPI-generointi myös virheellisellä ConnectionStrings__Parking-arvolla ja startup-migraatioiden ollessa asetuksissa päällä. Generointi ei avannut tietokantaa tai HTTP-palvelinta.
- EF InitialCreate-migraation luonti, idempotentti PostgreSQL SQL-skripti ja tarkistus, ettei mallissa ole migraatiosta puuttuvia muutoksia.
- Offline EF-malliprobe: jsonb:n juuritaulukko, TimeSpan round-trip, hinnaston sisäisen muutoksen seuranta, yksi halli/viisi paikkaa, nullable VisitId, aktiivisuuden osittainen yksikäsitteisyysindeksi ja historian poistamisen estävät viiteavaimet.
- Nuxt lint, typecheck ja staattinen tuotantobuild.
- Julkaistun .NET-hostin HTTP-smoketesti staattisella frontendillä: aloitussivu, SPA-fallback, health, ajonaikainen OpenAPI sekä puuttuvan API-, OpenAPI- ja asset-polun 404.
- Nuxt-kehityspalvelimen suhteellinen /api-proxy samalle backendille. HTTP-testissä startup-migraatiot oli tarkoituksella pois, eikä tietokantaa käytetty.
- Compose-konfiguraation syntaksitarkistus (`docker compose config --quiet`), ilman konttien käynnistämistä.
- SemVer-julkaisumetadatan hyväksytyt vakaat/prerelease/build-versiot ja virheellisten versioiden hylkäys. Mitään ei julkaistu.

Offline-probe ja paikalliset HTTP-testiajurit ovat ignoroidussa artifacts-hakemistossa; ne eivät ole pysäköinnin toiminnallinen testiharness. Ylläpidettävät sovellustestit kuuluvat PARK-01-työpakettiin.

## Avoin validointi

- Docker-daemon ei ollut käytettävissä. Docker-imagea, Compose-käynnistystä tai oikeaa PostgreSQL-migraatiota/uudelleenkäynnistystä ei ole ajettu. Kontin tietokantayhteyttä tai SQL-rajoitteiden toimintaa kilpailussa ei väitetä validoiduksi.
- GitHub Actionsin julkaisu ja GHCR-oikeudet vaativat oikean tagiajon.
- Frontendin visuaalista/selaimen hyväksyntää ei tehty; HTTP-smoke ei korvaa sitä.
- npm audit: 11 high-havaintoa, jotka kulkevat braces/micromatch/globby- ja node-forge/listhen/Nitro-ketjujen kautta. Nuxt ja Vue päivitettiin korjattuihin versioihin (4.6.1 ja 3.5.44); audit fix ei poistanut jäljelle jääviä transitiivisia havaintoja. Tarkista upstream-korjaukset ennen kehityspalvelimen altistamista verkkoon. Docker-runtime sisältää vain generoituja staattisia tiedostoja, ei näitä Node-palvelinriippuvuuksia. Tämä ei ole väite koko frontendin haavoittumattomuudesta.
- Nuxt generate onnistui, mutta antoi Nitro cache-driver -resoluutioon liittyvän varoituksen. Staattinen output ja HTTP-proxy toimivat paikallisessa smoketestissä; Docker-build on vielä ajamatta.

## Ensimmäinen oikean tietokannan tarkistus

1. Aseta paikallinen .env ja käynnistä `docker compose up --build -d`.
2. Tarkista `/api/health`, aloitussivu ja kontin lokit. Health on liveness, ei DB-readiness.
3. Tarkista tietokannasta yksi parking_garages-rivi, viisi parking_lots-riviä ja jsonb-hinnaston kaksi porrasta.
4. Käynnistä app uudelleen ja varmista, että tiedot säilyvät eikä seed tuplaannu.
5. Tarkista myöhemmin oikealla PostgreSQL:llä normaali käynti ja atominen paikan käyttö/vapautus, aktiivisuuden osittaiset indeksit sekä erillisillä DbContext-yhteyksillä koordinoidut kilpailut. Nämä ovat ihmisen myöhempää ympäristönäyttöä, eivät agentin submission-portti.

## PARK-01:n paikallinen hyväksyntä 14.10.2026

Käyttäjän päivitetty rajaus hyväksyy agenttityön palveluriippumattomalla näytöllä. Se ei muuta tuotannon PostgreSQL-tietolähdettä eikä väitä pohjan avoimia ympäristötarkistuksia suoritetuiksi.

### Suoritettu

- `dotnet test MiettisPark.slnx -c Release`: 54 xUnit-testiä läpäisi, 0 skipped. Tuotannon hinnan kaikki SPEC-rajat ja virheelliset hinnastot; normaalin käynnin/paikanvaihdon testikello, nollakesto, tallennettu kuitti ja toisto; tuore DbContext/API-reload; täydelle hallille sisäänajo; tunnistamattomat paikkahavainnot; VisitId/Occupied/Unknown/404/409/normalisoinnin virheet; tunnettu DB-konflikti testituplalla ja geneerinen sisäisen virheen HTTP-vastaus.
- Sama xUnit-ajo tarkisti offline Npgsql-mallin, enumien tallennuksen, nullable VisitId:n, Restrict-FK:t, lifecycle- ja aikarajoitteet, partial unique -indeksit, seedin/jsonb/TimeSpan-roundtripin ja mutable-hinnaston seurannan. Migraation idempotentti PostgreSQL-SQL generoitiin muistissa; `HasPendingModelChanges` oli false. Ei tietokantayhteyttä.
- `npm run check`: puhdas npm ci, lint, typecheck, 7 API-stubattua DOM-testiä sekä staattinen Nuxt build läpäisivät. Testit kattavat sisäänajon, valinnaisen käyntiyhteyden, paikanvaihdon, palvelimen kuitin, itsenäisen paikkakäytön, Occupied/Unknown-estot, konfliktin refreshin, API-remount/reload-jatkamisen, täyden hallin sisäänajon, rinnakkaislähetyksen eston, asiakaspalveluohjeen sekä tyhjän/virheellisen latauksen. Ei selainajuria tai palvelinprosessia.
- Check-runnerin paikallinen erillinen CLI-probe simuloi jokaista viittä epäonnistuvaa vaihetta: ajo pysähtyi kyseiseen vaiheeseen exit 23:lla; onnistuva probe ajoi kaikki viisi vaihetta client-cwd:ssä. Probe-tiedostot poistettiin.
- `dotnet format ... whitespace` rajattiin muutettuihin backend-/testitiedostoihin; `git diff --check` ei löytänyt whitespace-virheitä.
- Backend Release build ja sisäänrakennettu offline-OpenAPI-generointi onnistuvat paikallisesti. Lopullisen revision build/test/frontend-checks tallennetaan PullPotentialin RunVerification-avaimille `backend-build`, `backend-tests`, `frontend-checks`; platform evidence on auktoritatiivinen tulos, ei tämän tekstin omistajaväite.

### Toteutuksen aikana korjatut epäonnistumiset ja varoitukset

- Ensimmäinen HTTP-testifiksuuri deserialisoi string-enumin ilman enumikonvertteria; se korjattiin testifiksuurissa, ei tuotannon JSON-sopimusta muuttamalla. Ensimmäinen offline-mallitesti luki konvention kautta valitun enumikonvertterin väärästä metadata-API:sta; testi käyttää nyt toteutunutta type mapping -konvertteria. Molemmat testit läpäisevät lopullisessa ajossa; epäonnistumisia ei ohitettu.
- Kehitystyökalun ympäristöstä tuli paikallisessa buildissa puuttuvan `Microsoft.WebTools.ApiEndpointDiscovery` hosting-startup-assemblyn lokiviesti. Buildin/OpenAPI-generoinnin exit oli 0; tämä ei ollut PostgreSQL-ajo tai tuotannon käynnistysvalidointi.
- DOM-harnessin alkuperäinen Vitest 4.0.18 toi kriittisen UI-palvelin-advisoryn ja moderate-havainnon. Uusi testiriippuvuus päivitettiin lukittuun 4.1.11-versioon (GHSA-5xrq-8626-4rwp / GHSA-82fw-gwwq-j7x9); puhdas check läpäisi. Testit käyttävät vain `vitest run`, eivät avointa Vitest UI -palvelinta. Lopullinen npm-asennus ilmoitti edelleen 11 high-havaintoa pohjan Nuxtin transitiivisissa ketjuissa, ei critical/moderate-havaintoja. Niitä ei korjattu stackin versiovaihdolla tässä itemissä. Tämä ei ole kattava turvallisuusarvio.
- Nuxt generate antoi edelleen pohjasta tunnetun Nitro cache-driver -resoluutiovaroituksen sekä npm riippuvuuksien deprecation/install-script-varoituksia. Staattinen build onnistui; ei väitettä Docker-runtimen tarkistamisesta.

### Myöhemmät ihmisen ympäristötarkistukset — ajamatta

- Oikean PostgreSQL:n normaali käynti, SQL-lifecycle/FK/partial unique -rajoitteiden runtime-toiminta, transaktion rollback ja halli-ennen-paikkaa-lukitusten todellinen kilpailukäyttäytyminen. Koordinoi erillisten yhteyksien kilpailut samalle paikalle, samalle Visitille, samalle rekisterille sekä eri paikkojen null-VisitId-havainnoille.
- Oikean PostgreSQL:n migraation soveltaminen ja restart-pysyvyys; sovelluksen restart ei tuplaa seediä tai menetä tallennettua kuittia.
- Docker-image/Compose-käynnistys, tietokantayhteys ja yhden originin SPA/API/reitin refresh.
- Oikean selaimen näppäimistö-/visuaalinen hyväksyntä. DOM-stubit eivät todista selainta, CSS-ulkoasua tai verkkoyhteyttä oikeaan backendiin. Karttapalvelua ei tarvita tämän itemin UI:ssa.
- GitHub Actions/tagijulkaisu ja GHCR-oikeudet pysyvät foundationin avoimina tarkistuksina; mitään ei julkaistu.

Nämä ajamattomat tarkistukset eivät ole submission-portteja eivätkä aiheuta clarificationia. Tuotannon transaktioita tai tietokantainvariantteja ei korvattu testituplilla. PARK-01:n testitupla ei simuloinut rollbackia/SQL-rajoitteita. PARK-02:n alla kuvattu paikallinen malli täydentää tätä, mutta siitäkään ei väitetä PostgreSQL-semanttiikkaa.
## PARK-02:n paikallinen hyväksyntä 14.10.2026

- `dotnet test MiettisPark.slnx -c Release`: 97 testiä läpäisi, 0 failed / 0 skipped. Aito ParkingService ja prosessin sisäinen API testikellolla: oletus/poikkeava/nollaraja, 0/raja−1 tick/raja/raja+1 tick, alkuperäinen StartAt ja ikkunan siirtymättömyys, Interrupted-lifecycle, paikkahavaintojen riippumattomuus, vanhan kuitin muuttumattomuus ja erillinen uusi käynti. Negatiivinen ja vääränmuotoinen konfiguraatio hylättiin startupissa.
- Lisäysvaiheen ja commitin injektoitu virhe palautti aiemmin tallennetun Interrupted-sulkemisen paikallisessa tilannekuvamallissa. Tallennusinterceptor tarkisti sulkeminen-ennen-lisäystä-järjestyksen. Kolmen nimetyn unique-indeksin SaveChanges-poikkeamat testattiin rollback/disposen kautta 409:ksi; tuntematon DB-virhe jäi geneeriseksi 500:ksi. API ei palauttanut SQL:ää, ajuriviestiä, stack tracea, rekisteriä, hintaa tai vanhaa kuittia virheessä. Tuotantoon ei lisätty body-/rekisterilokitusta.
- Koordinoidut paikalliset kilpailut erillisillä DbContexteilla: sama paikka, sama Visit eri paikoille, sama normalisoitu rekisteri uutena ja recoveryssa, eri rekisterit täydellä hallilla yli kapasiteetin sekä eri paikkojen null VisitId:t. Tarkistettiin hallitut häviäjän koodit ja tuoreesta contextista lopputilat. TestCoordinator rendezvous käynnistää kaksi odottavaa komentoa; testituplan semafori serialisoi ja tilannekuva palauttaa tiedot epäonnistumisessa. Tämä ei käytä tai todista PostgreSQL:n semantiikkaa eikä tuotannossa ole muistilukkoa.
- Palvelu-/API-matriisi tarkisti 404/409-tulokset ja datan muuttumattomuuden: puuttuva/toisen hallin paikka ja Visit, Completed/Interrupted Visit, Occupied/Unknown ja jo pysäköity Visit.
- `npm --prefix src/client test`: 11 API-stubattua DOM-testiä läpäisi. Entry/Duplicate/recovery/asiakaspalveluvirhe ovat erillisiä saavutettavia tekstejä; kaikki kolme resurssia päivittyvät onnistumisesta ja konfliktista; recovery säilyttää paikan ja poistaa suljetun Visitin valinnoista. Vanha kuitti poistuu uudella ulosajoyrityksellä, lähetysesto ja API-remount/reload säilyvät. Ei oikeaa selainta, karttapalvelua tai verkkopalvelinta.
- Offline Npgsql-malli ja migraatio-SQL tarkistettiin samassa backend-testiajossa: Active-uniikkius, ei-null VisitId -filtteri, nullable FK/Restrict, Interrupted/null EndAt/Cost, lifecycle sekä `HasPendingModelChanges=false`. Tuotannon lähdekoodista tarkistettiin ReadCommitted, parametrisoitu halli-ennen-paikkaa FOR UPDATE ja transaktion purku ennen konfliktimuunnosta. Skeemamuutosta ei tarvittu.
- Lopullisen revision `backend-build`, `backend-tests` ja `frontend-checks` ajetaan PullPotentialin RunVerificationilla; sen tulos on auktoritatiivinen. `frontend-checks` tekee puhtaan npm ci/lint/typecheck/11 DOM-testiä/staattisen buildin. Microsoftin sisäänrakennettu offline OpenAPI päivitetään ennen revision verifiointia ilman DB:tä/migraatioita/kuuntelevaa palvelinta.

Ensimmäinen paikallinen `dotnet test --no-restore` ei löytänyt tämän worktreen assets-tiedostoa (NETSDK1004). Tavallinen restorea sisältävä testiajo korjasi puutteen; mitään testiä ei skipattu. Täydennetyn DOM-testin ensimmäinen versio oletti paikkalomakkeen säilyvän välilehden vaihdossa, vaikka komponentti unmountataan; testi korjattiin ja avoimen lomakkeen stale-valinta testattiin suoraan LotCardin prop-päivityksellä. Tuotannon watcher tyhjentää suljetun Visitin valinnan. Muotoilu tehtiin rajatuille C#-tiedostoille. Pohjasta tunnetut npm-audit/deprecation/Nitro-varoitukset eivät ole uusi turvallisuushyväksyntä; ne säilyvät yllä dokumentoituina.

### Riskit ja myöhempi ihmisen tarkistus — edelleen ajamatta

DuplicateEntryWindow on konfiguroitava heuristiikka, ei varma idempotenssi tai väärinkäytön tunnistus. Yli rajan havainto voi katkaista laskutettavan käynnin ja aloittaa uuden; vanhalle ei keksitä laskua. Pysyvä request-id ja asiakaspalvelun korjaus/ohitus puuttuvat tarkoituksellisesti. Asetus ja luovutus PARK-04/03:lle: README.

Oikean PostgreSQL:n synkronoidut kilpailut erillisillä yhteyksillä (myös recovery), lukitusjärjestys/odotus, partial unique -indeksien runtime/null-käytös ja rollback lisäys-/commit-virheessä; migraation soveltaminen ja restart-pysyvyys; Docker/Compose/yksi origin/SPA-refresh; oikea selain/näppäimistö/visuaalinen hyväksyntä ovat ajamatta. Paikallinen tilannekuvarollback ei todista DB-moottorin rollbackia. Nämä ovat myöhempiä ihmisen tarkistuksia, eivät submission-portteja eikä niitä ole merkitty läpäistyiksi.

## PARK-04:n paikallinen hyväksyntä 14.10.2026

- `dotnet test MiettisPark.slnx -c Release`: 99 passed, 0 failed / 0 skipped. Kertyneet palvelu-/API-/hinnoittelu-/invariantti-/paikalliset kilpailutestit mukana. Uusi AdminReadContractTests käyttää aitoa service/API-putkea TestServerissä ja testikellolla: sekä Completed- että Interrupted-Visitin aktiivinen Parking, null VisitId:n pysäköinti, saman rekisterin uusi yhteydetön Active Visit, eri Visit/Parking-alkuajat ja suorat availability-määrät. Kahden GET-kierroksen jälkeen tuoreesta DbContextista verrattu koko scalar-data/hinnasto säilyi muuttumattomana.
- Samassa backend-ajossa offline Npgsql-malli, idempotentti PostgreSQL-migraatio-SQL ja `HasPendingModelChanges=false` läpäisivät. Ei tietokantayhteyttä. Tuotannon PostgreSQL/EF/transaktiot eivät muuttuneet.
- `npm --prefix src/client test`: 26 passed (15 admin + 11 nykyistä porttisimulaation DOM-regressiota). Vitest/jsdom ja API-stubit/testikello, ei kuuntelevaa palvelinta, ulkoista palvelua tai selainajuria. Admin-testit kattavat DTO-määrät ilman listakorvausta, tuntemattomat/historialliset yhteydet, erilliset käynnit, Parking-alkuajan ja kiinteän päivityshetken keston, tulevan/virheellisen ajan, lataukset/tyhjät tilat, kaikkien neljän alkuhaun virheet, kolmen refresh-haun virheet/retryt, rinnakkaiseston myös yhden haun epäonnistuessa muiden ollessa kesken, atomisen UI-julkaisun ja vanhan tilanteen piilotuksen. Semanttiset linkit, caption/scope/fokusoitava vieritys ja todellisen HttpGarageApi-adapterin neljä suhteellista GET-kutsua ilman kirjoitusbodya testattu.
- Frontend lint ja typecheck läpäisivät. Staattinen Nuxt-build tuotti myös `/admin`-reitin; tämä ei ole saman Docker-imagen HTTP-/selainvalidointi.
- `node scripts/openapi.mjs` onnistui ilman DB:tä tai kuuntelevaa palvelinta; generoitu tracked OpenAPI ei muuttunut, koska backend-sopimus on ennallaan. Generointi suoritettiin ennen lopullisen revision platform-verificationia.
- Lopulliselle revisionille suoritetaan platform RunVerification avaimilla `backend-build`, `backend-tests`, `frontend-checks`. Viimeinen ajaa puhtaan npm ci/lint/typecheck/26 DOM-testin/staattisen buildin. Durable platform evidence ja submission ovat auktoritatiivinen lopputulos; nämä paikalliset ajot eivät korvaa sitä.

Ensimmäinen DOM-ajo epäonnistui testin tarkistaessa DOM:n ennen mountin reaktiivista päivitystä; testi odottaa nyt flushPromisesia ja kattaa aidon odottavan alkuhaun. Typecheck paljasti `get(...).exists()`-testifiksuurin väärän käytön; se vaihdettiin `find(...).exists()`-kutsuksi. Lopulliset testit/tyypitys läpäisevät, mitään ei skipattu. Alussa puuttuva Vitest korjaantui tavallisella npm ci:llä. Pohjasta tunnetut 11 high npm-audit-havaintoa, install/deprecation- ja Nitro cache-driver -varoitukset sekä offline-generoinnin ApiEndpointDiscovery-lokiviesti säilyvät; onnistunut komento ei ole turvallisuus- tai runtime-hyväksyntä.

### Myöhempi ihmisen tarkistus — ajamatta, ei submission-portti

- Oikean selaimen `/` ↔ `/admin`, näppäimistön fokus/järjestys, kapean näytön taulukoiden vieritys ja visuaalinen ulkoasu.
- Olemassa olevasta Dockerfilestä sama image: suora `/admin`-URL, refresh ja paluu `/`-reitille; frontend/API samassa originissa ilman runtime-Nodea.
- Oikea PostgreSQL: lukitus/kilpailut/rajoitteet/rollback, migraation soveltaminen ja restart-pysyvyys. Testiin rajattu InMemory/serialisointi/tilannekuvarollback ei todista DB-moottorin semantiikkaa.

Luovutus PARK-03: README kuvaa nykyiset neljä GET-sopimusta, Visit/Parking-riippumattomuuden ja selaimen ajan näyttörajan. Testikehikko on valmis; loppuhyväksyntä käyttää kertyneitä komentoja ja erottaa nämä ajamattomat ympäristötarkistukset paikallisesta näytöstä. Ei releasea, tagia tai image-pushia.
## PARK-03: koko MVP:n rajattu loppuhyväksyntä 14.10.2026

Nykyinen toimeksianto: PARK-01 → PARK-02 → PARK-04 → PARK-03, mukaan lukien `/admin`. Alla paikallinen näyttö ja ajamattomat ympäristötarkistukset erotetaan. Vanhojen SPEC/BACKLOG/STARTING-POINT-dokumenttien ulkoiset portit/adminin milestone-rajaus eivät ohita käyttäjän 14.10.2026 hyväksymää nykyistä rajausta. Tuotannon PostgreSQL ja liiketoimintasäännöt säilyvät.

### Toteutunut paikallinen ajo ja lopullisen revision evidence

Repojuuresta PowerShellillä (Linuxissa polkujen erotin `/`):

```powershell
dotnet test MiettisPark.slnx -c Release --filter FullyQualifiedName~ServiceTests
dotnet test MiettisPark.slnx -c Release
node scripts\openapi.mjs
npm run check
```

| Suoritettu workspace-komento | Todellinen tulos |
| --- | --- |
| Kohdennettu ServiceTests | 16 passed, 0 failed, 0 skipped. |
| Koko dotnet test Release | 101 passed, 0 failed, 0 skipped; sisältää hinnan/service/API/konflikti/kilpailu/admin/mallitestit. |
| node scripts\openapi.mjs | Exit 0, Release-build 0 warning/error; generoitu tracked OpenAPI ennallaan. Ei DB-yhteyttä/startup-migraatiota/kuuntelevaa palvelinta. |
| npm run check | Exit 0: puhdas npm ci, lint, typecheck, 26 DOM-testiä (11 portti + 15 admin), staattinen Nuxt-build myös /admin. |

Nämä ajot eivät vielä ole tämän tekstin lopullisen revision platform-näyttö. Dokumentit viimeistellään ennen revision jäädytystä; **lopullinen nykyrevision** evidence tallennetaan erikseen RunVerificationilla:

- `backend-build`: `dotnet build MiettisPark.slnx -c Release`
- `backend-tests`: `dotnet test MiettisPark.slnx -c Release`
- `frontend-checks`: `npm run check` (puhtaan snapshotin npm ci → lint → typecheck → DOM → build)

Platformin durable evidence/report ja submission-historia yksilöivät revision ja auktoritatiivisen lopputuloksen; aiempien itemien ajot tai omistajan dokumenttiväite eivät korvaa niitä. Myöhempi lähdemuutos vaatii uuden revision evidence-ajot. Checklist merkitään valmiiksi vasta lopputulosten jälkeen.

Uusi ThreeHourBoundaryPersistsReceiptUsingServerClock testaa aitoa ParkingServiceä ilman paikkahavaintoa: 3 h = 9,00 €, 3 h + 1 tick = 9,30 €, palvelinajat/duration, gateOpened/EUR sekä tuoreesta DbContextista tallennetut Completed/Cost/StartAt/EndAt/ClosedAt. PricingTests säilyttää kaikki SPEC:n hintarajat. Tuotannon DTO/API/hinnoittelu/malli/transaktiot eivät muuttuneet eikä julkista kellonmuokkausreittiä lisätty.

Ensimmäinen workspace-komento ei ajanut mitään PowerShellin hylättyä &&-erottimen; korjattu komento onnistui. Työkalun git branch --all -lukuyritykset palauttivat execution failed eikä niistä saatu näyttöä; git log milestone/59 onnistui. Uudet testit/koko testistö eivät epäonnistuneet eikä skippejä ollut. npm ci ilmoitti edelleen pohjan 11 high-audit-havaintoa ja deprecation/install-script-varoituksia. Staattinen build antoi tunnetun Nitro cache-driver -varoituksen, offline-generointi ympäristön puuttuvan ApiEndpointDiscovery-assemblyn lokiviestin; exitit olivat 0. Näitä ei korjattu riippuvuuksien avoimena päivitysurakkana eikä onnistumista väitetä turvallisuus-/runtime-hyväksynnäksi.

### Hyväksyntäpolku → nykyinen testi → näyttöraja

Backend-tiedostot ovat tests\MiettisPark.Api.Tests; DOM-tiedostot src\client\app\pages. Lyhyet kuvaukset viittaavat tiedostojen testinimiin.

| Polku | Nykyinen palvelu/API/DOM-testi | Todistettava raja |
| --- | --- | --- |
| Normaali käynti, paikanvaihto, tallennettu kuitti | ServiceTests.NormalVisitMovesLotsAndPersistsReceiptWithServerTimes; ApiTests.HttpPipelineRunsNormalFlowAndReloadWithNumericReceipt; index.test.ts: supports gate entry, linked parking, moving lots, and server receipt | Aito palvelu/testikello, tallennus tuoreesta contextista, HTTP/JSON ja stubattu UI; ei oikeaa DB:tä/selainta. |
| Kolmen tunnin raja ja muut SPEC-rajat | ServiceTests.ThreeHourBoundaryPersistsReceiptUsingServerClock (0/1 tick); PricingTests.CalculatesSpecificationBoundaries, RejectsNegativeDuration, RejectsInvalidConfigurations | 9,00/9,30 palvelimen kuitti ja tallennus; tarkka decimal/tick-kaava. |
| API-reload ja jatkaminen | ServiceTests.FreshContextCanContinueActiveVisitAndParking; ApiTests.HttpPipelineRunsNormalFlowAndReloadWithNumericReceipt; index.test.ts: re-fetches all data on remount | Uusi context/client/mount käyttää API-tietoa; ei todista prosessirestartin DB-pysyvyyttä. |
| Täysi halli ja silti sisäänajo | ServiceTests.IndependentObservationsAllowSeveralUnknownCarsAndFullGarageEntry; ConcurrencyTests.DifferentRegistrationsCanExceedCapacityWhenFull; index.test.ts: records entry in a full garage | Paikkatilat, ei Visit-määrä, määräävät isFullin; sisäänajo sallittu. |
| Occupied/Unknown ja Visit-yhteyden estot | ServiceTests.NonFreeLotsRejectEntryAfterGarageThenLotLock; RecoveryApiTests.ServiceAndApiRejectInvalidLotCommandsWithoutMutation; index.test.ts: independent parking/only Free ja conflicting lot selection | Domain/API 404/409, UI-estot ja konfliktin refresh; ei DB-lukon runtimea. |
| Tunnistamaton pysäköinti/vapautus | ApiTests.EmptyAndNullVisitBodiesAllowIndependentParking; RecoveryTests.NullVisitIsNormalWhenReleasingAnIndependentParking; ConcurrencyTests.IndependentUnknownCarsBothPersistOnDifferentLots; index.test.ts independent parking | Null VisitId on normaali, useat eri paikat sallittu; SQL-null-indeksin runtime jää erikseen. |
| Duplicate/recovery ja historiallisen paikan säilyminen | RecoveryTests.DuplicateUsesOriginalStartAndNeverChangesParking, RecoveryClosesBeforeInsertAndPreservesHistoricalParking; RecoveryApiTests.ApiUsesConfiguredWindowAndOriginalStart; index.test.ts duplicate/recovery | Raja 2 min / +1 tick, sulkemisjärjestys ja itsenäisyys; rollback/kilpailu vain paikallisessa mallissa. |
| Puuttuva/toistettu ulosajo, ei uutta veloitusta | RecoveryApiTests.MissingAndRepeatedExitNeverReturnCostOrHistoricalReceipt; RecoveryTests.CompletedVisitAndRepeatedExitNeverChangeParkingOrStoredReceipt; index.test.ts 409/customer service ja clears an earlier receipt | Turvallinen asiakaspalveluvirhe, vanha Cost säilyy, UI ei näytä vanhaa kuittia uutena. |
| Lukeva admin, Ei tiedossa ja erilliset käynnit | AdminReadContractTests.ReadsPreserveIndependentUnknownAndHistoricalObservations (Completed/Interrupted); admin.test.ts API counts/rows, read-only adapter, refresh/error/retry/semantics | GET ei muuta tallennettua dataa; Parking-alku/kesto eikä Visit-alku; DOM ei todista ulkoasua/selainta. |
| EF/PostgreSQL-sopimus ja laskutusauktoriteetti | ModelTests.NpgsqlModelAndMigrationPreserveOfflineDatabaseContract; ApiTests normaali polku ylimääräisellä asiakkaan StartAt:lla; GarageContracts.cs ja generoitu OpenAPI tarkistettu | Npgsql-malli/idempotentti SQL/no pending changes; nullable VisitId/FK/lifecycle/Active-indeksit. LicenseRequest sisältää vain LicenseNumberin, LotEntryRequest VisitId:n; ei asiakkaan aikaa/hintaa. |

Tuotannossa säilyvät ReadCommitted, parametrisoitu halli-ennen-paikkaa FOR UPDATE ja osittaiset uniikki-indeksit. Recovery tallentaa sulkemisen ennen uuden Active-rivin lisäystä samaan transaktioon. InMemory/serialisointi/tilannekuvarollback ei todista PostgreSQL-lukitusta, erillisyystasoa, rajoitteita tai rollbackia. TestServer ei avaa verkkopalvelinta; DOM käyttää API-stubeja. Karttapalvelua ei käytetä nykyisessä sovelluksessa eikä testeissä.

### Myöhemmän ihmisen runbook — kaikki alla AJAMATTA, ei agenttityön portti

Käytä erillistä paikallista testidataa, ei oikeita rekistereitä/henkilötietoja. Kirjaa ajopäivä, commit/image, selain, ympäristö ja todelliset tulokset/poikkeamat; älä merkitse läpäistyksi pelkästä ohjeesta. Älä julkaise/tagaa/pushaa imagea tämän runbookin perusteella.

#### 1. Olemassa oleva image + erillinen PostgreSQL

1. Repojuuresta `Copy-Item .env.example .env`, aseta paikallinen POSTGRES_PASSWORD (ei versionhallintaan), sitten `docker compose up --build -d`. Tämä rakentaa olemassa olevan Dockerfilen imagen; ei uutta deploymentia. DB on erillinen postgres:17-palvelu.
2. Aja `docker compose ps` ja `docker compose logs app`. App käynnistyy migraatioiden jälkeen ilman runtime-Nodea. Avaa http://localhost:8080 (tai APP_PORT), http://localhost:8080/admin sekä `/api/health`, `/api/garages` ja `/api/garages/1/availability` samassa originissa. Health on vain liveness; garage-GET todentaa DB-polun.
3. Saatavuuden capacity=5 ja alkutilan vapaita=5, hallilista yksi. Tarkista seedi myös DB:stä käyttäen .env:n omaa käyttäjä-/DB-nimeä: `docker compose exec database psql -U miettis_park -d miettis_park -c "SELECT id, parking_fee_configuration FROM parking_garages; SELECT id, number, current_status FROM parking_lots ORDER BY number;"`. Odotus yksi halli, viisi paikkaa, hinnastossa 0.50/0.30 ja TimeSpan 10 min/3 h.
4. Avaa suoraan `/admin` uuteen välilehteen ja tee hard refresh sekä `/`- että `/admin`-reitillä. SPA avautuu, suhteelliset API-haut onnistuvat, ei erillistä Node-palvelua. Tarkista myös ettei puuttuva `/api/...` muutu HTML-200-vastaukseksi.

#### 2. Selaimen pakollinen polku ja itsenäiset havainnot

1. Syötä testirekisteri TEST-001 ja kirjaa sisäänajo. Valitse vapaa paikka 1 ja yhdistä siihen käynti. Vapauta paikka erillisellä toiminnolla, valitse paikka 2 samalle käynnille. Käynnin alku säilyy.
2. Reload `/`: aktiivinen käynti ja paikka tulevat API:sta. Jatka ulosajoon; kuitti näyttää rekisterin, palvelinajat, keston ja eurohinnan. Paikka 2 pysyy Occupied-tilassa ja Parking aktiivisena, kunnes simuloit vapautushavainnon. Toistettu ulosajo: portti kiinni/asiakaspalvelu, ei uutta kuittia eikä aiemman Costin muutosta.
3. Täytä kaikki viisi paikkaa **ilman käyntiyhteyttä**. Täysi halli on tieto ja uusi rekisterin sisäänajo onnistuu silti. Occupied-paikka ei tarjoa valintaa. Vapauta paikkoja niiden omalla komennolla.
4. Unknown-tilan erillinen fixture, vain omassa testikannassa ja vapaalle paikalle: `docker compose exec database psql -U miettis_park -d miettis_park -c "UPDATE parking_lots SET current_status='Unknown' WHERE number=5 AND current_status='Free';"`. Päivitä UI: Unknown ei ole valittavissa, mutta vapautushavainto palauttaa Free-tilan myös ilman Parkingia. Palauta fixture tällä sovelluksen komennolla; älä tee tuotantodatan korjausta SQL:llä.
5. Kirjaa TEST-DUP sisään ja heti sama rekisteri uudelleen: Duplicate, sama alku/VisitId. Kytke halutessasi paikka. Yli oletuksen 2 minuutin jälkeen uusi havainto: recovery-varoitus, vanha Interrupted ilman EndAt/Costia, uusi käynti/uusi laskutusalku; vanha paikka/historiallinen yhteys säilyy. Tarvittaessa erillisessä testiajossa aseta Parking__DuplicateEntryWindow lyhyemmäksi; palauta oletus jälkeenpäin. Täsmällinen 3 h / +1 tick testataan yllä testikellolla, ei selaimen odottamisella tai julkisella kellonmuokkaus-API:lla.
6. Ulosajo rekisterillä TEST-MISSING ilman sisäänajoa: ACTIVE_VISIT_NOT_FOUND/asiakaspalvelu, portti kiinni, ei arvattua hintaa tai keksittyä yhteystietoa. Kahdessa välilehdessä jo käytetyn paikan kirjoitusyritys palauttaa hallitun konfliktin ja refreshin; DOM-painikkeiden esto ei korvaa backend-tarkistusta.
7. Tarkista labelit, Tab/Shift+Tab/Enter/Space, näkyvä fokus ja status/alert ilman pelkkään väriin luottamista; tarkista kapea/leveä näyttö ja kuittitiedot. Kirjaa oikean selaimen visuaalinen/näppäimistötulos erikseen DOM-näytöstä.

#### 3. Admin ja restart-pysyvyys

1. Jätä aktiiviseksi tunnistamaton Parking, suljettuun Visitiin liittyvä Parking sekä uuden aktiivisen Visitin puuttuva paikkayhteys. Avaa `/admin` suoraan/refresh: kaikki Parkingit näkyvät, tuntematon rekisteri ”Ei tiedossa”, historiallinen rekisteri ei yhdisty uuteen Visitiin. Erillisen käyntitaulukon null-paikka ”Paikka ei tiedossa”.
2. Vertaa määrät availability-JSONiin. Vertaa taulukon paikka/alku active parkings -JSONiin: kulunut aika alkaa Parking.startAt:sta ja päivittyy onnistuneessa refreshissä. Päivityshetki ja ei-laskutusperuste-huomautus näkyvät. Tarkista lataus/virhe/retry, `/`-paluu, fokus/caption/otsikot sekä kapean näytön vaakavieritys näppäimistöllä.
3. Ennen restartia tallenna testirekisterien Visit-rivien id/status/start_at/end_at/closed_at/cost sekä Parking-/paikkatilat psql-lukukyselyllä. Säilytä myös aiemman kuitin tulos, koska API ei tarjoa historiakuittihakua.
4. Aja `docker compose restart app` (älä poista DB-volumea). Tarkista lokit/GETit ja molempien SPA-reittien refresh. Aiempi aktiivinen Visit/Parking voidaan jatkaa; tallennettu Completed-kuitti Cost/EndAt/ClosedAt on DB:ssä ennallaan ja seed edelleen yksi halli/viisi paikkaa. Toista tarvittaessa `docker compose down` ja `docker compose up -d` ilman `-v`:tä; tietojen pitää säilyä. Restart-pysyvyys ei ole nykyisten InMemory-testien näyttöä.

#### 4. PostgreSQL-lukot, kilpailut, rajoitteet ja rollback — erillinen tarkistus

Käytä todellista PostgreSQL:ää ja erillisiä DB-yhteyksiä/DbContexteja. Koordinoi kaksi kirjoitusta yhteiseen aloitusesteeseen, älä suorita niitä peräkkäin tai luota sleep-ajoitukseen. Toista nykyisen ConcurrencyTests-matriisin tapaukset oikealla tuotannon transaktiotehtaalla: sama paikka, sama Visit eri paikoille, sama normalisoitu rekisteri uutena ja recoveryssa, eri rekisterit täydellä hallilla sekä eri paikkojen null VisitId:t. Tarkista molemmat vastaukset ja lopputila tuoreesta yhteydestä: yksi Active per paikka/non-null Visit, yksi Active per garage/rekisteri; tunnettu häviäjä hallittu 409, sisäänajossa normaalisti Entry/recovery + Duplicate, eri null-paikat molemmat onnistuvat.

Pidä ensimmäisen yhteyden hallilukko avoimena ja tarkista toisesta odotus/pg_locks; paikkalukon pitää tulla hallin jälkeen. Vahvista partial Active -indeksien null-käytös, FK/lifecycle-rajoitteiden runtime ja rollback injektoidussa recoveryn lisäys-/commit-virheessä oikean DB:n päällä: vanhan sulkeminen ei jää voimaan epäonnistuneesta transaktiosta. Tämä vaatii erillisen ihmisen testiajurin/testiin rajatun virhesauman, ei tuotannon julkista debug-API:a. Raportoi puuttuva runtime-näyttö avoimeksi. Olemassa oleva paikallinen semafori/tilannekuvarollback ei todista mitään näistä DB-moottorin ominaisuuksista.

### Luovutus ja integraation todellinen tila

Platformin milestone 59 on InProgress, branch milestone/59. Sen git log sisältää PARK-01 `2715700` (PR #1), PARK-02 `4a98c71` (PR #2), PARK-04 `b0986bf` (PR #3) ja pohjan `a82ca21`. PARK-03/item 1119 on tämän dokumentin kirjoitushetkellä toteutuksessa: sen integraatiota ei ole vielä tehty tai väitetty tehdyksi. Lead toimittaa lopullisen revision kolme verification-avainta ja todennetun checklistin SubmitImplementationiin; PullPotential hoitaa commitit/PR:n/integraation. Ihmisen milestone-release/tagi ja GHCR-oikeuksien julkaisuajo ovat erillisiä, eivät tämän tehtävän toimia.

Jatkajan sopimukset: kahdeksan nykyistä API-reittiä/DTO:t; Visit/Parking itsenäisiä ja VisitId nullable; halli-ennen-paikkaa/transaktio sekä recoveryn sulkeminen-ennen-lisäystä; laskutus vain palvelimen TimeProvider/decimal/ticks. Tarkistuskomennot yllä. Avoinna ovat tämän runbookin oikea selain/kontti/PostgreSQL/restart, pohjan tunnetut riippuvuushavainnot, mahdollinen pysyvä request-id/asiakaspalvelukorjaus ja henkilötietojen säilytys/suojaus ennen oikeaa käyttöä. Ei uutta backlogia tai väitettä tuotantovalmiudesta.

## PARK-V2-01: paikallinen toteutusnäyttö 14.10.2026

Lähtöworktree oli puhdas MVP, HEAD `2c84353` (Merge pull request #7 from miettis/main), edellinen `b0a377b`. Git-log ja todelliset ParkingService/API/DTO/admin/harness-tiedostot luettiin. `a82ca21`-ancestryä ei saatu työkalulla varmennettua (merge-base-pyyntö palautti execution failed); tätä ei esitetä integraatiohistorian näyttönä. Ei haaran vaihtoa, mergeä tai julkaisua.

### Suoritettu paikallisesti ja lopullisen revision näyttö

- Backendin koko Release-testistö: **152 passed, 0 failed, 0 skipped**. GarageManagementTests käyttää aitoa serviceä ja TestServer-API:a: create/update/reload, kahden hallin eri hinnasto, risti-ID, kenttä-/ruutu-/versiovalidaatiot, numerointi/vaihto/siirto, explicit retirement ja numeron uudelleenkäyttö, Occupied/Unknown/Free+Active Parking -poistokiellot, stale ja molemmat poisto-vs-käyttöönoton lopputilat. Paikallinen koordinoitu erillisten contextien kilpailu sallii vain toisen komennon onnistumisen. SaveChanges-vaiheiden 1–3 ja commitin injektoidut virheet palauttavat koko scalar-/JSON-/versio-/aktiivisuusdatan tuoreesta contextista. Historiassa Completed/Interrupted Visitit, päättyneet Parkingit sekä Active Parking säilyvät; myös VisitId=null. MVP:n kertyneet regressiot ovat mukana.
- Offline Npgsql-malli ja idempotentti migraatio-SQL samassa ajossa: jsonb:n rakenteellinen seuranta, concurrency token, aktiivisuuden osittainen numerouniikkius, Active Parking -indeksit, Restrict-FK:t ja `HasPendingModelChanges=false`. SQL sisältää deterministisen Number/Id-backfillin ja >2500-guardin eikä seed-resettiä, vanhojen numeroiden/tilojen/hinnaston tai historian ylikirjoitusta. Backfillin 0/5/50/51/2500-rajojen referenssilaskenta ja SQL-lausekkeet tarkistetaan offline; tätä ei väitetä SQL:n suorittamiseksi PostgreSQL:ssä.
- Frontendin **46 DOM-testiä**: MVP:n 26 regressiota sekä hallilista/luonti/muokkaus, luonnos/peruutus, näppäimistö/pointer-veto ja pointercancel/osumatestaus, pienennyksen esto ja ajoratavahvistus, 50×50, numero/siirto, palvelimen ID:t, konfliktin luonnoksen säilyminen, reload/poistumisvaroitus, 404 ja kartan mount/unmount/virhe/retry/pistevalinta sekä kesken importin unmount. Vitest/jsdom, API- ja karttaloader-stubit; ei ulkoisia verkkopyyntöjä, palvelinprosessia tai selainajuria.
- Lint/typecheck ja staattinen Nuxt-build on ajettu, myös MapLibren erillinen worker ja oikea dynaaminen kirjastochunk rakentuvat. Adminin parent-sivut siirrettiin index-sivuiksi, jotta lapsireitit eivät peity.
- Offline `node scripts\openapi.mjs` päivittää tracked-sopimuksen ennen lopullisen revision evidenceä ilman DB:tä tai kuuntelevaa palvelinta. Lopullinen auktoritatiivinen näyttö on PullPotentialin **nykyrevision** RunVerification: `backend-build` = `dotnet build MiettisPark.slnx -c Release`, `backend-tests` = `dotnet test MiettisPark.slnx -c Release`, `frontend-checks` = `npm run check` (clean ci/lint/typecheck/DOM/build). Lähteet ja dokumentit jäädytetään ennen näitä ajoja; pysyvä checklist kuitataan vasta onnistumisista.

Toteutuksen aikana korjattiin delegoidun UI:n 30-ruudun raja, hiljainen paikkojen leikkaus, puuttuva oikea karttapisteen valinta, parent-reittien lapsinäkymät ja pointer/reload-reunapolut. Testifiksuurien readonly-event-option ja väärät apurinimet korjattiin; epäonnistuneita testejä ei ohitettu. Windowsin tekstikoodauksesta syntyneet UI-merkkivirheet korjattiin UTF-8:aan. Pohjan npm-audit-havainnot/deprecationit ja Nitro cache-driver -varoitus säilyvät; MapLibren laiska chunk aiheuttaa kokovaroituksen. Ne eivät muuta onnistunutta buildia turvallisuus- tai selainhyväksynnäksi.

### Myöhemmin ihmisen tarkistettavaa — ajamatta, ei submission-portti

1. Oikean PostgreSQL:n migraatio MVP-datalla: Completed/Interrupted Visitit, päättyneet ja aktiiviset Parkingit, Occupied/Unknown-paikat sekä muuttumaton oma hinnasto; vertaile ID:t, numerot, tilat, viitteet ja deterministinen pohja ennen/jälkeen. Tarkista 0/5/50/51/2500-rajat ja >2500-virhe oikealla moottorilla.
2. PostgreSQL:n todelliset FOR UPDATE / ReadCommitted -kilpailut: stale-save, poisto-vs-enter molemmissa järjestyksissä, numerovaihdon välitön uniikki-indeksi, FK:t sekä rollback eri save/commit-vaiheissa. Testikoordinaattorin semafori ja tilannekuvarollback **eivät todista** PostgreSQL:n semantiikkaa. Tuotantoon ei lisätty muistilukkoa tai InMemory-provideria.
3. Docker/yksi origin: suora `/admin/garages/{id}/layout`, refresh, suhteellinen API ja worker/CSS-assetit. Restartin jälkeen pohja/version/ID:t/historia säilyvät ilman seed-resettiä.
4. Oikea selain/WebGL/OpenFreeMap: renderöinti, attribuutio ja lähdelinkit, verkon katkeaminen/retry, touch-vetomaalaus, näppäimistöfokus ja responsiivinen ulkoasu. DOM-stubi ei todista näitä.

Luovutus PARK-V2-02: README dokumentoi layout DTO/versionin, IsActive-ehdon, lukitus-/numerointisopimuksen, commit-kohdat ja jaetun GarageMapin props/select/worker-käytön. Julkinen hallikartta, uusi simulaatioreitti ja SSE ovat toteuttamatta tässä itemissä tarkoituksella. Ei releasea tai deployta.

## PARK-V2-02 – paikallinen toteutusnäyttö

Lähtöhaara sisälsi PARK-V2-01:n commitin `6d4bcdb` (#8); worktree oli aluksi puhdas. Toteutus lisää `/`-hallikartan/listan, `/garages/{id}`-simulaation ja commitin jälkeisen SSE-invalidoinnin. Ei mallimuutosta.

xUnit käyttää oikeita tuotantopalveluja, testikelloa ja testiin rajattua EF InMemory/snapshot-transaktiotuplaa sekä prosessinsisäistä TestServeriä. Julkaisu tarkistetaan commitin/disposen jälkeen; save-/commit-rollback, invalidit ID:t, layout-version konflikti ja Visitin riippumattomuus säilyvät. Publisher-testit kattavat fanoutin ja 64-viestin ylivuodon. Stream-testit kattavat alkukehyksen, anonyymin payloadin, headerit, testikellon heartbeatin, hitaan kirjoituksen aikarajan ja request-peruutuksen siivouksen. EF/Npgsql-malli-, migraatio-SQL- ja HasPendingModelChanges-testit säilyvät mukana. Tupla ei todista PostgreSQL:n lukitus-/kilpailusemantiikkaa.

Vitest/jsdom käyttää karttaloader-, EventSource- ja Promise-tuplia. Mukana kartan elinkaari/virhe/retry/marker-päivitykset/koordinaatit, ruudukon tilat, MVP-simulaation regressiot, initial-fetch-race/duplikaatit, reitinvaihto/myöhäinen kuitti, reconnect/resync-virhe, viimeisen datan ja aktiivisen syötteen/valinnan säilyminen sekä editorin muuttumaton luonnos/expectedVersion.

Offline OpenAPI: `node scripts\openapi.mjs`. Platform-evidence: `backend-build` = `dotnet build MiettisPark.slnx -c Release`, `backend-tests` = `dotnet test MiettisPark.slnx -c Release`, `frontend-checks` = `npm run check` (clean ci, lint, typecheck, DOM, staattinen build). Tarkat nykyrevision exitit/raportit tallentuvat PullPotentialin verification-evidenceen; aiempien itemien raportit eivät korvaa niitä.

**Ajamatta, myöhemmälle ihmiselle:** oikea PostgreSQL-migraatio/lukitus/kilpailu/rollback, Docker/SPA-refresh/restart-pysyvyys, oikea selain/WebGL/OpenFreeMap sekä kahden selaimen SSE tuotantoproxyn läpi. Yhden prosessin muistijulkaisija ei tarjoa hajautettua fanoutia eikä kestävää replayta/outboxia. Proxyn buffering off, no-transform ja heartbeatia pidempi idle timeout on tarkistettava ympäristössä. Ulkoisia tarkistuksia ei väitetä hyväksytyiksi. Pohjan tunnetut npm-audit/build-varoitukset säilyvät erillisinä havaintoina.

Viimeisin paikallinen ajo: backend 161 läpäistyä / 0 epäonnistunutta / 0 ohitettua; frontend 62 DOM/client-testiä läpäisty, lint ja typecheck läpäisty. Pakollinen clean-build-näyttö kirjataan lisäksi nykyrevision platform-raportteihin.

## PARK-V2-03 — rajattu paikallinen hyväksyntä ja luovutus (14.10.2026)

### Lähtö, muutokset ja näytön auktoriteetti

Worktree oli puhdas. `git log -15 --oneline` näytti HEADin `0e8bdf8` (PARK-V2-02, PR #9), `6d4bcdb` (PARK-V2-01, PR #8), `dff550f` (MVP, PR #5) ja `a82ca21` (pohja). Palvelut/API/DTO/harness sekä hallieditori/kartta/simulaatio/SSE ovat lähtökoodissa. Projektitoimeksiannon mukaan MVP ja riippuvuus 1121 ovat Done. Ei haaran vaihtoa/mergeä; git-log todistaa saatavilla olevan historian, epäonnistunut ancestry-komento ei ole lisänäyttöä. V2-03 on kirjoitushetkellä Implementation: integraatio luovutetaan PullPotentialille submissionissa eikä väitetä jo tehdyksi.

Ainoa uusi koodi on `V2AcceptanceTests.TwoCreatedGaragesPersistLayoutsIndependentOccupancyAndClockReceiptsAcrossApiReload`. Se ajaa oikeat GarageManagementService/ParkingService/controllerit prosessinsisäisellä TestServerillä. Tuore client tarkoittaa uutta API-lukua ja tuore DI-scope uutta DbContextia saman testivaraston sisällä, **ei** käyttöjärjestelmäprosessin tai PostgreSQL:n restartia. Testikello, EF InMemory ja testikohtainen snapshot-transaktio ovat paikallisia tuplia. Tuotannon Npgsql/PostgreSQL, ReadCommitted ja halli-ennen-paikkaa FOR UPDATE säilyvät muuttumattomina; ei uutta stackia/skeemaa/riippuvuutta tai yleistä hardeningia.

### Vaatimus → testi → näyttöraja

Kaikki tiedostot sijaitsevat `tests\MiettisPark.Api.Tests`- tai `src\client\app`-hakemistossa. Alla olevan kertyneen matriisin testit ajettiin tässä itemissä, eivät vain aiemmissa itemeissä.

| Vaatimus | Nykyinen automatisoitu testi | Näyttöraja |
| --- | --- | --- |
| Kaksi uutta hallia, osoite/piste, 0/0, Road/paikat/tallennus, ID/version, käyttö molemmilla VisitId-vaihtoehdoilla, 3 h + tick -kuitti, API-reload | Uusi `V2AcceptanceTests.TwoCreatedGaragesPersistLayoutsIndependentOccupancyAndClockReceiptsAcrossApiReload` | Aito API/service ja tuore DbContext; ei kartan maalausta tai prosessin restartia. Hallien paikanumerot 1/2 saavat eri ID:t; risti-hallin lot-ID hylätään. Ulosajo ei muuta Occupied/Active Parkingia. |
| Tick-/decimal-hinnat, 3 h, Duplicate/Interrupted, täysi halli, valinnainen Visit ja hallirajat | `PricingTests`, `ServiceTests`, `RecoveryTests`, `RecoveryApiTests`, `ApiTests`, `AdminReadContractTests`; `GarageManagementTests.CreateUpdateReloadAndSeparateGaragePricingPreserveMvp` | Testikello ja aidot tuotantopalvelut; ei oikeaa maksamista/porttia tai DB-runtimea. |
| Pistevalinta, maalaus/veto, tallennus/peruutus, siirron ID, poistot/pienennyssuoja, virheessä säilyvä luonnos ja kanonisen ID:n uudelleenkäyttö | `pages/garageAdmin.test.ts` (20 tapausta), mm. pisteen/keskipisteen valinta, exact removed IDs, optimistic version, reload-peruutus ja save-virheiden matriisi | Vitest/jsdom, karttaloader/API-stubit; ei oikeaa WebGL:ää, tile-palvelua tai selainajuria. |
| Suora kartta/listalinkki halliin, Free/Occupied/Unknown-tekstit ja värit, 0/0 ja karttavirheen listavaihtoehto | `components/GarageMap.test.ts`, `components/GarageLayoutView.test.ts`; `services/garageEvents.test.ts` list stays usable -tapaus | DOM-ankkuri ja navigate-tapahtuma tarkistetaan, ei oikean selaimen navigointia/visuaalista renderöintiä. Karttaloaderin virhestub säilyttää listan. |
| Simulaatio/paikanvaihto/kuitti sekä API-remount ja molempien hallien komentoreitit | `pages/index.test.ts` normal flow/remount/full garage; `services/garageEvents.test.ts` uses each route ID | API-stubin DOM/client-kattavuus täydentää erillistä aitoa API-testiä, ei teeskenneltyä selain-E2E:tä. |
| SSE commit/dispose, rollbackin hiljaisuus, rajattu puskuri, heartbeat, kirjoitusaikaraja, peruutussiivous ja payload | `GarageEventsTests` (publisher/service/API/clock/cleanup) | Paikallinen stream ja injektoitu kello; ei kahden selaimen/proxyn hyväksyntää. |
| Yksi EventSource, alkuhaun invalidointi, duplikaatti/stale, reconnect, reitinvaihto ja syötteen/valinnan/luonnoksen expectedVersion-suoja | `services/garageEvents.test.ts` (11 tapausta) | EventSource/Promise/API-tuplat; invalidointi ja uusi snapshot, ei replay/outbox/hajautettu fanout. |
| Stale-save, poisto/käyttöönotto, rollback save 1–3/commit, siirto ja historian säilyminen | `GarageManagementTests` stale/race/EverySaveStage/ExplicitRetirement/SwapsAndMoves; `ConcurrencyTests`, `RecoveryTests` | Tuplan koordinaatio/snapshot ei todista PostgreSQL:n lukkoja, indeksejä tai rollback-semanttiikkaa. |
| Offline Npgsql-malli, idempotentti SQL, deterministic backfill, Restrict-FK/partial unique/IsActive/HasPendingModelChanges=false | `ModelTests.NpgsqlModelAndMigrationPreserveOfflineDatabaseContract`, `OfflineBackfillArithmeticMatchesSqlBoundaries` | SQL tuotettu/tarkistettu ja 0/5/50/51/2500 laskenta verrattu; SQL:ää ei sovellettu oikeaan tietokantaan. Ei historian/hinnaston resettiä. |
| Offline OpenAPI | `node scripts\openapi.mjs`, Microsoftin build-generointi | Ei DB-yhteyttä/startup-migraatioita/kuuntelevaa palvelinta. Tracked-sopimus pysyi samana; uusi testi ei muuta API:a. |

### Tässä itemissä suoritetut komennot ja havainnot

Repojuuressa PowerShellillä:

```powershell
dotnet test MiettisPark.slnx -c Release --filter FullyQualifiedName~V2AcceptanceTests
dotnet test MiettisPark.slnx -c Release
npm run check
# Vain paikallisen työkalun injektoiman hosting-startupin poisto tästä prosessista:
$env:ASPNETCORE_HOSTINGSTARTUPASSEMBLIES = ''
node scripts\openapi.mjs
```

- Uusi kohdennettu testi: **1 passed / 0 failed / 0 skipped**, exit 0. Koko backend: **162 passed / 0 failed / 0 skipped**, exit 0, sisältäen ModelTests ja concurrency/historian.
- `npm run check`: clean npm ci → lint → typecheck → **62 passed / 0 failed / 0 skipped** kuudessa Vitest/jsdom-tiedostossa → staattinen Nuxt-build, exit 0. MapLibre-chunk ja paikallinen worker rakentuvat. Ei ulkoisia kartta-/API-/EventSource-palveluja.
- Eksplisiittinen offline OpenAPI: exit 0, build 0 warning / 0 error, ei tracked-kuvauksen muutosta. Ei DB-asetuksia tai kuuntelevaa sovellusta.
- Ensimmäinen git-lukukomento ei ajanut mitään PowerShellin `&&`-parserivirheen vuoksi; korjattiin puolipisteisiin. Erillinen metadata/ancestry-komento palautti execution failed; sallittu git-log luettiin sen sijaan. Ei epäonnistunutta testiä, skipiä tai tuotannon regressiokorjausta.
- Ensimmäisen kohdennetun buildin offline metadata-ajossa ympäristön `Microsoft.WebTools.ApiEndpointDiscovery`-hosting-startup-assembly puuttui ja lokitti poikkeaman; generointi ja testi silti exit 0. Eksplisiittinen offline-ajo onnistui ilman ympäristöinjektiota, ei sovelluksen startup-muutosta.
- Npm ilmoitti pohjan **11 high audit -havaintoa**, whatwg-encoding/glob-deprecationit sekä esbuild/unrs-resolver allow-scripts -huomautuksen. Build ilmoitti MapLibre-chunkin >500 kB sekä Nitro cache-driver -resoluutiovaroituksen ja SPA:n `ssr:false`-prerender-huomautuksen. Niitä ei piilotettu, korjattu scopea laajentamalla tai tulkittu turvallisuus-/runtime-hyväksynnäksi.

**Lopullinen nykyrevision evidence:** dokumentit ja testilähde jäädytetään ennen RunVerification-ajoja: `backend-build` = `dotnet build MiettisPark.slnx -c Release`; `backend-tests` = `dotnet test MiettisPark.slnx -c Release`; `frontend-checks` = `npm run check`. Kaikkien kolmen tulee olla exit 0 samalla platform revisionilla; todelliset revision/exit/raporttipolut löytyvät itemin verification-evidence-historiasta. Yllä ovat toteutuneet workspace-ajot; V2-01:n 152/46 ja V2-02:n 161/62 sekä niiden raportit ovat historiaa, eivät tämän revision todiste. Uusi lähdemuutos vaatii uuden lopullisen evidence-ajon.

### AJAMATTA — v2:n myöhempi ihmisen ympäristörunbook

**Kaikki seuraavat ovat suorittamatta. Ne eivät ole agentin aloitus-, checklist- tai submission-portteja.** Aja vain omassa testikannassa/testiympäristössä. Ei testitulosta tai ympäristön saatavuutta keksitä.

1. **Yksi Docker-image + erillinen PostgreSQL:** kopioi `.env.example` → `.env`, aseta oma paikallinen salasana (ei versionhallintaan), aja `docker compose up --build -d`. Tarkista `docker compose ps`/lokit, frontend ja suhteellinen `/api/garages` samasta originista. PostgreSQL on erillinen palvelu; runtime ei tarvitse Nodea. MVP-seed on vain yksi lähtöhalli, uusia halleja ei lasketa seed-duplikaateiksi.
2. **Kahden hallin koko selainpolku:** luo `/admin/garages/new`-sivulla kaksi hallia eri nimellä/osoitteella ja valitse molemmille karttapiste; osoitteen/pisteen vastaavuus on ylläpitäjän vastuulla, ei geokoodausta. Tarkista ensin tyhjä 0/0, maalaa Road/paikat/Empty, tallenna ja lataa uudelleen. Avaa `/`-karttamerkistä ja listalinkistä suoraan oikea `/garages/{id}`. Tarkista nimet, osoitteet, luvut/värit/tekstit; käytä paikkaa VisitId:llä ja ilman, vaihda paikkaa, aja ulos ja tarkista kuitti. Täysi halli ei estä sisäänajoa; ulosajo ei vapauta paikkaa. Toista erikseen molemmille halleille ja tarkista hallirajojen eristys.
3. **Editorin reunat:** maalaa ja Peruuta ilman API-muutosta; siirrä olemassa oleva paikka tyhjään ruutuun (ID/numero/historia säilyvät), muuta numeroa ja tallenna. Vapaan paikan nimenomainen poisto vähentää kapasiteettia mutta säilyttää historian; Occupied/Unknown/Active Parking -poisto estyy. Pienennys ei leikkaa paikkaa hiljaa; Roadin leikkaus vahvistetaan. Kahden editorin stale-save antaa 409 ja säilyttää luonnoksen. Tyhjä pohja ja luonnoksen poistumisvaroitus/reload-peruutus tarkistetaan.
4. **Unknown, karttavirhe ja oikea renderöinti:** aseta tarvittaessa omassa testikannassa valitulle vapaan hallipaikan ID:lle Unknown-fixture (ei tuotantodata), päivitä. Unknown on harmaa + teksti ja erillinen luku, ei Occupied/Free; vapautushavainto toimii. Estä selaimessa vain OpenFreeMap-verkkopyynnöt: virhe/retry näkyy mutta lista ja simulaatio toimivat; puuttuva sijainti on listalla ilman merkkiä. Tarkista WebGL, Liberty-style, paikallinen worker/CSS, OpenFreeMap/OpenMapTiles/OpenStreetMap-attribuutiot ja lähdelinkit, fokus/Enter/Tab sekä kapea näyttö. Ei avainta nykyiselle stylelle, ei offline-/saatavuuslupausta; config/ehdot READMEssä.
5. **Suorat reitit ja refresh:** avaa uuden välilehden suorina URL:eina `/admin/garages/{id}/layout` ja `/garages/{id}`, hard refresh molemmilla sekä `/` ja `/admin`. Sama image palvelee SPA:n ja API:n, API-404 ei saa muuttua HTML-200:ksi. Testaa myös puuttuvan hallin ymmärrettävä 404.
6. **Restart-pysyvyys:** tallenna ennen restartia hallien sijainti/osoite/pohja/version, paikkojen ID/numero/IsActive/tila, Visit/Parking-historia ja kuitin Cost/EndAt/ClosedAt DB-lukukyselyillä. Aja `docker compose restart app` ja refresh; vertaa tuoreista DB/API-luvuista. Aktiivisen käytön jatkaminen ja Completed-kuitti eivät saa muuttua. Tarvittaessa down/up **ilman `-v`:tä**; älä poista datavolumea. Paikallinen API-remount ei todista tätä.
7. **Oikea PostgreSQL:** sovella idempotentit migraatiot MVP-fixtureen, jossa ovat aktiivinen/historiallinen Parking, Completed/Interrupted Visit, Occupied/Unknown ja oma hinnasto. Vertaa ID:t/historia/hinnasto ennen/jälkeen ja deterministinen Number/Id-backfill sekä 0/5/50/51/2500/>2500-rajat. Koordinoi erillisten yhteyksien FOR UPDATE/ReadCommitted-ajot yhteisellä esteellä: halli ennen paikkoja, stale-save, poisto-vs-enter molemmissa järjestyksissä, numerovaihto, sama paikka/non-null Visit/normalisoitu rekisteri, null VisitId eri paikoilla. Tarkista partial unique/Restrict-FK/lifecycle sekä save/commit-rollback oikealla moottorilla. Käytä testiajurin virhesaumaa, ei julkista debug-API:a. Semafori/snapshot-tupla ei ole näiden todiste.
8. **Kahden oikean selaimen SSE tuotantoproxyn läpi:** avaa molemmissa `/` ja sama halli; toisessa simuloi occupancy sekä ylläpidossa layout/garage-muutos. Varmista kartan/listan/ruudukon invalidointi ilman delta-virhettä, Visit-muutos ei kasvata Occupiedia. Katkaise stream/verkkoyhteys, tee muutoksia toisessa selaimessa, palauta: reconnect hakee kokonaisen snapshotin, viimeinen data/syöte/valinta säilyvät ja expectedVersion ei rebasetu luonnoksessa. Tarkista yksi EventSource/tabi, heartbeat, siivous, buffering off/no-transform ja yli 15 s idle-timeout. Julkaisija on yhden prosessin muistissa, ei usean replikan fanout/replay/outbox.

### Luovutus, integraatio ja release-raja

README sisältää nykyiset UI/API-reitit, GarageDto/LayoutDto/ProblemDetails-sopimuksen, expectedVersion/removedLotIds/IsActive-, halli-ennen-paikkaa- ja Visit/Parking-invariantit, MapLibre/OpenFreeMap-configin/workerin/ehdot sekä toistettavat komennot. Historiallisen MVP:n `/`-simulaatio, monihalli/admin-rajaukset ja SSE:n poissulku eivät kuvaa nykyistä v2:ta. AI-USAGE kirjaa tämän itemin todellisen Lead-työn ilman uutta delegointia tai keksittyä ihmisen hyväksyntää.

Lead tarkistaa lopullisen diffin/palautteen ja kaikki pysyvän checklistin vaatimukset nykyrevision näytön perusteella; submission ja Git/milestone-integraatio kuuluvat PullPotentialille. Toteutunutta V2-03-mergeä ei väitetä etukäteen. Ihmisen lopullinen hyväksyntä, milestone-release, SemVer-tagi ja image-julkaisu ovat paikallisesta valmistumisesta erillisiä; ei manuaalista committia/pushia/tagia/image-pushia/deployta tai automaattista releasea.