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